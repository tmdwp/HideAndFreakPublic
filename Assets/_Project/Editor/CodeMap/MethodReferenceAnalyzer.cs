using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace GuardVsMannequin.Editor.CodeMap
{
    /// <summary>
    /// 컴파일된 IL을 읽어 멤버가 쓰는 필드·프로퍼티·메소드를 찾는다.
    /// 소스 문자열 검색과 달리 같은 이름의 다른 멤버를 헷갈리지 않고, 람다·코루틴 본문까지 원래 메소드의 사용처로 합친다.
    /// </summary>
    internal sealed class MethodReferenceAnalyzer
    {
        private const string BackingFieldSuffix = ">k__BackingField";
        private const string RpcLogicPrefix = "RpcLogic___";

        private readonly Dictionary<CodeMember, IReadOnlyList<CodeReference>> outgoingCache = new Dictionary<CodeMember, IReadOnlyList<CodeReference>>();

        /// <summary>member 본문이 쓰는 멤버를 본문에 처음 나온 순서대로 돌려준다.</summary>
        public IReadOnlyList<CodeReference> GetOutgoing(CodeMember member)
        {
            if (outgoingCache.TryGetValue(member, out IReadOnlyList<CodeReference> cached))
                return cached;

            var collector = new ReferenceCollector(member);
            foreach (MethodBase body in BodiesOf(member))
                Collect(body, member.Member.Name, collector);

            IReadOnlyList<CodeReference> result = collector.ToList();
            outgoingCache[member] = result;
            return result;
        }

        /// <summary>candidates 중 target을 쓰는 멤버. 반대 방향 색인은 비용이 커서, 후보 범위를 받아 그때그때 찾는다.</summary>
        public List<CodeReference> FindIncoming(CodeMember target, IEnumerable<CodeMember> candidates)
        {
            var result = new List<CodeReference>();
            var seen = new HashSet<CodeMember>();
            foreach (CodeMember candidate in candidates)
            {
                if (!candidate.HasBody || !seen.Add(candidate) || candidate.Equals(target))
                    continue;

                CodeReference match = GetOutgoing(candidate).FirstOrDefault(reference => reference.Target.Equals(target));
                if (match != null)
                    result.Add(new CodeReference(candidate, match.Usage, match.Count));
            }

            return result;
        }

        private static IEnumerable<MethodBase> BodiesOf(CodeMember member)
        {
            switch (member.Member)
            {
                case MethodBase method:
                    yield return method;
                    break;
                case PropertyInfo property:
                    yield return property.GetGetMethod(true);
                    yield return property.GetSetMethod(true);
                    break;
                case EventInfo eventInfo:
                    yield return eventInfo.GetAddMethod(true);
                    yield return eventInfo.GetRemoveMethod(true);
                    break;
            }
        }

        private void Collect(MethodBase method, string rootName, ReferenceCollector collector)
        {
            if (method == null || !collector.Visit(method))
                return;

            // 코루틴·async 본문은 컴파일러가 만든 상태 머신의 MoveNext로 옮겨져 있다.
            Collect(FindStateMachineMoveNext(method), rootName, collector);
            foreach (MethodBase companion in FindWovenCompanions(method))
                Collect(companion, rootName, collector);

            byte[] il = ReadIl(method);
            if (il == null)
                return;

            var tokens = new List<IlMemberToken>();
            IlTokenReader.ReadMemberTokens(il, tokens);

            Type declaringType = method.DeclaringType;
            Type[] typeArguments = declaringType != null && declaringType.IsGenericType ? declaringType.GetGenericArguments() : null;
            Type[] methodArguments = method is MethodInfo && method.IsGenericMethod ? method.GetGenericArguments() : null;

            foreach (IlMemberToken token in tokens)
            {
                MemberInfo resolved = Resolve(method.Module, token.Token, typeArguments, methodArguments);
                if (resolved is FieldInfo field)
                    AddField(field, token.OpCode, collector);
                else if (resolved is MethodBase called)
                    AddMethod(called, rootName, collector);
            }
        }

        private void AddMethod(MethodBase called, string rootName, ReferenceCollector collector)
        {
            if (called.IsSpecialName && called is MethodInfo accessor && TryAddAccessor(accessor, collector))
                return;

            if (TypeMemberCatalog.IsWoven(called.Name))
            {
                // FishNet이 만든 메소드 중 원래 본문을 옮겨 담은 것만 따라가고, 네트워크 전송 코드는 숨긴다.
                if (IsCompanionName(called.Name, rootName))
                    Collect(called, rootName, collector);
                return;
            }

            if (TypeMemberCatalog.IsGenerated(called))
            {
                // 람다·로컬 함수는 따로 보여 주기보다 감싼 메소드가 하는 일로 합쳐야 흐름이 읽힌다.
                Collect(called, rootName, collector);
                return;
            }

            collector.Add(called, ReferenceUsage.Call);
        }

        private static bool TryAddAccessor(MethodInfo accessor, ReferenceCollector collector)
        {
            string name = accessor.Name;
            bool isGetter = name.StartsWith("get_", StringComparison.Ordinal);
            bool isSetter = name.StartsWith("set_", StringComparison.Ordinal);
            if (isGetter || isSetter)
            {
                PropertyInfo property = SafeDeclared(accessor.DeclaringType, type => type.GetProperties(TypeMemberCatalog.DeclaredAll))
                    .FirstOrDefault(candidate => IsSameMember(candidate.GetGetMethod(true), accessor) || IsSameMember(candidate.GetSetMethod(true), accessor));
                if (property == null)
                    return false;
                collector.Add(property, isGetter ? ReferenceUsage.Read : ReferenceUsage.Write);
                return true;
            }

            if (name.StartsWith("add_", StringComparison.Ordinal) || name.StartsWith("remove_", StringComparison.Ordinal))
            {
                EventInfo eventInfo = SafeDeclared(accessor.DeclaringType, type => type.GetEvents(TypeMemberCatalog.DeclaredAll))
                    .FirstOrDefault(candidate => IsSameMember(candidate.GetAddMethod(true), accessor) || IsSameMember(candidate.GetRemoveMethod(true), accessor));
                if (eventInfo == null)
                    return false;
                collector.Add(eventInfo, ReferenceUsage.Call);
                return true;
            }

            return false;
        }

        private static void AddField(FieldInfo field, OpCode opCode, ReferenceCollector collector)
        {
            // 상태 머신·클로저 필드는 지역 변수를 옮겨 담은 것이라 멤버 변수가 아니다.
            if (TypeMemberCatalog.IsGeneratedType(field.DeclaringType))
                return;

            // 주소 읽기(ldflda)는 구조체 메소드 호출에도 쓰여 쓰기로 단정할 수 없으므로 읽기로 본다.
            ReferenceUsage usage = opCode == OpCodes.Stfld || opCode == OpCodes.Stsfld ? ReferenceUsage.Write : ReferenceUsage.Read;

            string propertyName = BackingPropertyName(field.Name);
            if (propertyName != null)
            {
                PropertyInfo property = SafeDeclared(field.DeclaringType, type => type.GetProperties(TypeMemberCatalog.DeclaredAll))
                    .FirstOrDefault(candidate => candidate.Name == propertyName);
                if (property != null)
                    collector.Add(property, usage);
                return;
            }

            if (!TypeMemberCatalog.IsGenerated(field))
                collector.Add(field, usage);
        }

        private static MethodBase FindStateMachineMoveNext(MethodBase method)
        {
            try
            {
                StateMachineAttribute attribute = method.GetCustomAttribute<StateMachineAttribute>(false);
                return attribute?.StateMachineType?.GetMethod("MoveNext", TypeMemberCatalog.DeclaredAll);
            }
            catch (Exception)
            {
                // 특성을 못 읽으면 상태 머신 없이 바깥 본문만 분석한다.
                return null;
            }
        }

        /// <summary>FishNet은 RPC·Awake 본문을 RpcLogic___이름_해시, 이름___UserLogic 같은 메소드로 옮긴다.</summary>
        private static IEnumerable<MethodBase> FindWovenCompanions(MethodBase method)
        {
            if (method.DeclaringType == null || TypeMemberCatalog.IsGenerated(method))
                return Enumerable.Empty<MethodBase>();

            return SafeDeclared(method.DeclaringType, type => type.GetMethods(TypeMemberCatalog.DeclaredAll))
                .Where(candidate => IsCompanionName(candidate.Name, method.Name));
        }

        private static bool IsCompanionName(string wovenName, string rootName)
        {
            return wovenName.StartsWith(rootName + "___", StringComparison.Ordinal)
                || wovenName.StartsWith(RpcLogicPrefix + rootName + "_", StringComparison.Ordinal);
        }

        private static string BackingPropertyName(string fieldName)
        {
            if (fieldName.Length < 2 || fieldName[0] != '<' || !fieldName.EndsWith(BackingFieldSuffix, StringComparison.Ordinal))
                return null;
            return fieldName.Substring(1, fieldName.Length - 1 - BackingFieldSuffix.Length);
        }

        private static byte[] ReadIl(MethodBase method)
        {
            try
            {
                return method.GetMethodBody()?.GetILAsByteArray();
            }
            catch (Exception)
            {
                // 엔진 내부(extern) 메소드처럼 본문을 줄 수 없는 경우.
                return null;
            }
        }

        private static MemberInfo Resolve(Module module, int token, Type[] typeArguments, Type[] methodArguments)
        {
            try
            {
                return module.ResolveMember(token, typeArguments, methodArguments);
            }
            catch (Exception)
            {
                // 참조 어셈블리가 없거나 제네릭 문맥이 맞지 않는 토큰은 그 하나만 건너뛴다.
                return null;
            }
        }

        private static IEnumerable<T> SafeDeclared<T>(Type type, Func<Type, T[]> getMembers)
        {
            if (type == null)
                return Enumerable.Empty<T>();
            try
            {
                return getMembers(type);
            }
            catch (Exception)
            {
                return Enumerable.Empty<T>();
            }
        }

        private static bool IsSameMember(MemberInfo left, MemberInfo right)
        {
            return left != null && right != null && left.MetadataToken == right.MetadataToken && left.Module == right.Module;
        }

        /// <summary>한 멤버를 분석하는 동안 방문한 본문과, 본문에 나온 순서를 지킨 사용 목록.</summary>
        private sealed class ReferenceCollector
        {
            private readonly CodeMember owner;
            private readonly HashSet<MethodBase> visited = new HashSet<MethodBase>();
            private readonly Dictionary<CodeMember, CodeReference> referenceByTarget = new Dictionary<CodeMember, CodeReference>();
            private readonly List<CodeReference> orderedReferences = new List<CodeReference>();

            public ReferenceCollector(CodeMember owner)
            {
                this.owner = owner;
            }

            public bool Visit(MethodBase method) => visited.Add(method);

            public void Add(MemberInfo member, ReferenceUsage usage)
            {
                CodeMember target = CodeMember.Create(member);
                // 자동 프로퍼티가 자기 숨은 필드를 읽는 것처럼, 자기 자신을 가리키는 선은 정보가 없다.
                if (target.Equals(owner))
                    return;

                if (!referenceByTarget.TryGetValue(target, out CodeReference reference))
                {
                    reference = new CodeReference(target);
                    referenceByTarget.Add(target, reference);
                    orderedReferences.Add(reference);
                }

                reference.Add(usage);
            }

            public IReadOnlyList<CodeReference> ToList() => orderedReferences;
        }
    }
}
