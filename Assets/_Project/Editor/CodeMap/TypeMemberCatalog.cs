using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace GuardVsMannequin.Editor.CodeMap
{
    /// <summary>한 타입이 직접 선언한 멤버 묶음.</summary>
    internal sealed class TypeMemberGroup
    {
        public TypeMemberGroup(Type type, IReadOnlyList<CodeMember> members)
        {
            Type = type;
            Members = members;
        }

        public Type Type { get; }
        public IReadOnlyList<CodeMember> Members { get; }
    }

    /// <summary>
    /// 컴파일러·FishNet 코드 생성이 끼워 넣은 멤버(람다, 상태 머신, RpcWriter___ 등)를 빼고, 사람이 작성한 멤버만 목록으로 만든다.
    /// </summary>
    internal static class TypeMemberCatalog
    {
        public const BindingFlags DeclaredAll =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        private const BindingFlags DeclaredPublic =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // FishNet이 컴파일 후 끼워 넣는 멤버는 이름에 밑줄 세 개가 들어간다.
        private const string WovenMarker = "___";

        /// <summary>자기 타입부터 includeBase가 허락하는 부모까지, 타입별로 묶어 돌려준다.</summary>
        public static List<TypeMemberGroup> GetHierarchy(Type type, Func<Type, bool> includeBase, bool includeNonPublic)
        {
            var groups = new List<TypeMemberGroup>();
            for (Type current = type; current != null; current = current.BaseType)
            {
                if (current != type && !includeBase(current))
                    break;
                groups.Add(GetDeclared(current, includeNonPublic));
            }

            return groups;
        }

        /// <summary>목록에 보여 줄, 직접 선언한 멤버. 순서는 종류별로 묶되 같은 종류 안에서는 선언 순서를 유지한다.</summary>
        public static TypeMemberGroup GetDeclared(Type type, bool includeNonPublic)
        {
            BindingFlags flags = includeNonPublic ? DeclaredAll : DeclaredPublic;
            var members = new List<CodeMember>();

            EventInfo[] events = type.GetEvents(flags);
            // 필드처럼 선언한 이벤트는 같은 이름의 숨은 필드가 따로 생겨 목록에 두 번 나오므로 뺀다.
            var eventNames = new HashSet<string>(events.Select(eventInfo => eventInfo.Name));

            members.AddRange(type.GetFields(flags).Where(field => !IsGenerated(field) && !eventNames.Contains(field.Name)).Select(CodeMember.Create));
            members.AddRange(type.GetProperties(flags).Where(property => !IsGenerated(property)).Select(CodeMember.Create));
            members.AddRange(events.Where(eventInfo => !IsGenerated(eventInfo)).Select(CodeMember.Create));
            // MonoBehaviour 생성자는 직접 쓰지 않아 목록만 길게 만들므로 숨긴다(필드 초기화 사용처로는 여전히 나온다).
            if (!typeof(UnityEngine.Object).IsAssignableFrom(type))
                members.AddRange(type.GetConstructors(flags).Where(constructor => !constructor.IsStatic && !IsGenerated(constructor)).Select(CodeMember.Create));
            members.AddRange(type.GetMethods(flags).Where(method => !method.IsSpecialName && !IsGenerated(method)).Select(CodeMember.Create));

            return new TypeMemberGroup(type, members.OrderBy(member => (int)member.Kind).ToList());
        }

        /// <summary>사용처를 찾을 때 본문을 읽을 멤버. 필드 초기화가 들어 있는 생성자도 포함한다.</summary>
        public static IEnumerable<CodeMember> GetBodyMembers(Type type)
        {
            foreach (PropertyInfo property in type.GetProperties(DeclaredAll))
            {
                if (!IsGenerated(property))
                    yield return CodeMember.Create(property);
            }

            foreach (EventInfo eventInfo in type.GetEvents(DeclaredAll))
            {
                if (!IsGenerated(eventInfo))
                    yield return CodeMember.Create(eventInfo);
            }

            foreach (ConstructorInfo constructor in type.GetConstructors(DeclaredAll))
                yield return CodeMember.Create(constructor);

            foreach (MethodInfo method in type.GetMethods(DeclaredAll))
            {
                if (!method.IsSpecialName && !IsGenerated(method))
                    yield return CodeMember.Create(method);
            }
        }

        /// <summary>내 코드 어셈블리를 훑을 때 쓸, 사람이 작성한 타입.</summary>
        public static IEnumerable<Type> GetAuthoredTypes(Assembly assembly)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                // 일부 타입만 못 읽어도 나머지로 사용처를 찾을 수 있게 한다.
                types = exception.Types.Where(type => type != null).ToArray();
            }

            return types.Where(type => !IsGeneratedType(type));
        }

        public static bool IsGenerated(MemberInfo member)
        {
            if (member.Name.IndexOf('<') >= 0 || IsWoven(member.Name))
                return true;
            // 자동 프로퍼티 접근자도 CompilerGenerated가 붙지만 사람이 선언한 프로퍼티의 일부이므로 제외하지 않는다.
            bool isAccessor = member is MethodBase method && method.IsSpecialName;
            if (!isAccessor && SafeIsDefined(member, typeof(CompilerGeneratedAttribute)))
                return true;
            return IsGeneratedType(member.DeclaringType);
        }

        public static bool IsGeneratedType(Type type)
        {
            for (Type current = type; current != null; current = current.DeclaringType)
            {
                if (current.Name.IndexOf('<') >= 0 || SafeIsDefined(current, typeof(CompilerGeneratedAttribute)))
                    return true;
            }

            return false;
        }

        public static bool IsWoven(string memberName) => memberName.IndexOf(WovenMarker, StringComparison.Ordinal) >= 0;

        private static bool SafeIsDefined(MemberInfo member, Type attributeType)
        {
            try
            {
                return member.IsDefined(attributeType, false);
            }
            catch (Exception)
            {
                // 참조가 빠진 특성을 읽다 예외가 나도 목록 전체가 깨지지 않게 한다.
                return false;
            }
        }
    }
}
