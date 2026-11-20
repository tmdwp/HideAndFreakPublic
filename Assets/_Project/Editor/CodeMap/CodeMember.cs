using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GuardVsMannequin.Editor.CodeMap
{
    internal enum CodeMemberKind
    {
        Field,
        Property,
        Event,
        Constructor,
        Method,
    }

    /// <summary>
    /// 같은 멤버라도 제네릭 인스턴스·조회한 타입마다 다른 MemberInfo 객체가 나오므로, 메타데이터 토큰으로 같은 멤버를 하나의 노드로 묶기 위한 래퍼.
    /// </summary>
    internal sealed class CodeMember : IEquatable<CodeMember>
    {
        private string attributeSummary;

        private CodeMember(MemberInfo member, CodeMemberKind kind)
        {
            Member = member;
            Kind = kind;
        }

        public MemberInfo Member { get; }
        public CodeMemberKind Kind { get; }
        public Type DeclaringType => Member.DeclaringType;

        public string Name => Kind == CodeMemberKind.Constructor ? CodeTypeNames.Friendly(DeclaringType) : Member.Name;

        public bool IsEngineCallback => Kind == CodeMemberKind.Method && EngineCallbackNames.Contains(Member.Name);

        public bool HasBody => Kind != CodeMemberKind.Field;

        /// <summary>그래프 노드처럼 좁은 곳에 쓰는 이름.</summary>
        public string ShortLabel
        {
            get
            {
                switch (Kind)
                {
                    case CodeMemberKind.Method:
                        return Name + "()";
                    case CodeMemberKind.Constructor:
                        return "new " + Name + "()";
                    default:
                        return Name;
                }
            }
        }

        /// <summary>선언 모양 그대로의 한 줄 요약.</summary>
        public string Signature
        {
            get
            {
                string modifiers = Access + (IsStatic ? " static" : string.Empty);
                switch (Member)
                {
                    case FieldInfo field:
                        return $"{modifiers} {CodeTypeNames.Friendly(field.FieldType)} {Name}";
                    case PropertyInfo property:
                        string accessors = (property.CanRead ? "get; " : string.Empty) + (property.CanWrite ? "set; " : string.Empty);
                        return $"{modifiers} {CodeTypeNames.Friendly(property.PropertyType)} {Name} {{ {accessors}}}";
                    case EventInfo eventInfo:
                        return $"{modifiers} event {CodeTypeNames.Friendly(eventInfo.EventHandlerType)} {Name}";
                    case ConstructorInfo constructor:
                        return $"{modifiers} {Name}({Parameters(constructor)})";
                    case MethodInfo method:
                        string genericArguments = method.IsGenericMethod
                            ? "<" + string.Join(", ", method.GetGenericArguments().Select(CodeTypeNames.Friendly)) + ">"
                            : string.Empty;
                        return $"{modifiers} {CodeTypeNames.Friendly(method.ReturnType)} {Name}{genericArguments}({Parameters(method)})";
                    default:
                        return Name;
                }
            }
        }

        public string Access
        {
            get
            {
                switch (Member)
                {
                    case FieldInfo field:
                        return AccessOf(field.IsPublic, field.IsFamily, field.IsAssembly, field.IsFamilyOrAssembly, field.IsFamilyAndAssembly);
                    case MethodBase method:
                        return AccessOf(method);
                    default:
                        MethodBase accessor = AccessorOf(Member);
                        return accessor == null ? "?" : AccessOf(accessor);
                }
            }
        }

        public bool IsStatic
        {
            get
            {
                switch (Member)
                {
                    case FieldInfo field:
                        return field.IsStatic;
                    case MethodBase method:
                        return method.IsStatic;
                    default:
                        MethodBase accessor = AccessorOf(Member);
                        return accessor != null && accessor.IsStatic;
                }
            }
        }

        /// <summary>[ServerRpc]·[SerializeField] 같은 특성은 동작을 크게 바꾸므로 툴팁에 함께 보여 준다.</summary>
        public string AttributeSummary
        {
            get
            {
                if (attributeSummary != null)
                    return attributeSummary;

                try
                {
                    IEnumerable<string> names = Member.GetCustomAttributesData()
                        .Select(data => data.AttributeType)
                        .Where(type => type.Namespace != "System.Runtime.CompilerServices" && type.Namespace != "System.Diagnostics")
                        .Select(type => "[" + RemoveAttributeSuffix(type.Name) + "]");
                    attributeSummary = string.Join(" ", names);
                }
                catch (Exception)
                {
                    // 참조 어셈블리가 빠진 특성은 읽다가 예외가 나므로, 표시만 포기한다.
                    attributeSummary = string.Empty;
                }

                return attributeSummary;
            }
        }

        public string Tooltip
        {
            get
            {
                string attributes = AttributeSummary;
                string header = string.IsNullOrEmpty(attributes) ? string.Empty : attributes + "\n";
                return header + Signature.Trim() + "\n소속: " + CodeTypeNames.Qualified(DeclaringType);
            }
        }

        public static CodeMember Create(MemberInfo member)
        {
            return new CodeMember(member, KindOf(member));
        }

        public bool Equals(CodeMember other)
        {
            return other != null
                && Member.MetadataToken == other.Member.MetadataToken
                && Member.Module == other.Member.Module;
        }

        public override bool Equals(object obj) => Equals(obj as CodeMember);

        public override int GetHashCode() => Member.MetadataToken ^ Member.Module.GetHashCode();

        public override string ToString() => CodeTypeNames.Friendly(DeclaringType) + "." + ShortLabel;

        private static CodeMemberKind KindOf(MemberInfo member)
        {
            switch (member)
            {
                case FieldInfo _:
                    return CodeMemberKind.Field;
                case PropertyInfo _:
                    return CodeMemberKind.Property;
                case EventInfo _:
                    return CodeMemberKind.Event;
                case ConstructorInfo _:
                    return CodeMemberKind.Constructor;
                default:
                    return CodeMemberKind.Method;
            }
        }

        private static MethodBase AccessorOf(MemberInfo member)
        {
            switch (member)
            {
                case PropertyInfo property:
                    return property.GetGetMethod(true) ?? property.GetSetMethod(true);
                case EventInfo eventInfo:
                    return eventInfo.GetAddMethod(true);
                default:
                    return null;
            }
        }

        private static string AccessOf(MethodBase method)
        {
            return AccessOf(method.IsPublic, method.IsFamily, method.IsAssembly, method.IsFamilyOrAssembly, method.IsFamilyAndAssembly);
        }

        private static string AccessOf(bool isPublic, bool isFamily, bool isAssembly, bool isFamilyOrAssembly, bool isFamilyAndAssembly)
        {
            if (isPublic)
                return "public";
            if (isFamilyOrAssembly)
                return "protected internal";
            if (isFamilyAndAssembly)
                return "private protected";
            if (isFamily)
                return "protected";
            if (isAssembly)
                return "internal";
            return "private";
        }

        private static string Parameters(MethodBase method)
        {
            return string.Join(", ", method.GetParameters().Select(parameter =>
            {
                string prefix = string.Empty;
                if (parameter.ParameterType.IsByRef)
                    prefix = parameter.IsOut ? "out " : parameter.IsIn ? "in " : "ref ";
                return prefix + CodeTypeNames.Friendly(parameter.ParameterType) + " " + parameter.Name;
            }));
        }

        private static string RemoveAttributeSuffix(string name)
        {
            const string suffix = "Attribute";
            return name.EndsWith(suffix, StringComparison.Ordinal) ? name.Substring(0, name.Length - suffix.Length) : name;
        }
    }
}
