using System;
using System.Collections.Generic;
using System.Linq;

namespace GuardVsMannequin.Editor.CodeMap
{
    /// <summary>리플렉션 이름(List`1, Int32)은 코드에서 쓰는 이름과 달라 읽기 어려워, C# 표기로 바꾼다.</summary>
    internal static class CodeTypeNames
    {
        private static readonly Dictionary<Type, string> aliases = new Dictionary<Type, string>
        {
            { typeof(void), "void" },
            { typeof(bool), "bool" },
            { typeof(byte), "byte" },
            { typeof(sbyte), "sbyte" },
            { typeof(char), "char" },
            { typeof(short), "short" },
            { typeof(ushort), "ushort" },
            { typeof(int), "int" },
            { typeof(uint), "uint" },
            { typeof(long), "long" },
            { typeof(ulong), "ulong" },
            { typeof(float), "float" },
            { typeof(double), "double" },
            { typeof(decimal), "decimal" },
            { typeof(string), "string" },
            { typeof(object), "object" },
        };

        /// <summary>코드에 적는 모양의 타입 이름.</summary>
        public static string Friendly(Type type)
        {
            if (type == null)
                return "?";
            if (type.IsByRef)
                return Friendly(type.GetElementType());
            if (type.IsPointer)
                return Friendly(type.GetElementType()) + "*";
            if (type.IsArray)
                return Friendly(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
            if (aliases.TryGetValue(type, out string alias))
                return alias;

            Type nullableValue = Nullable.GetUnderlyingType(type);
            if (nullableValue != null)
                return Friendly(nullableValue) + "?";

            string name = StripArity(type.Name);
            if (!type.IsGenericType)
                return name;
            return name + "<" + string.Join(", ", type.GetGenericArguments().Select(Friendly)) + ">";
        }

        /// <summary>중첩 타입은 바깥 타입까지 붙여야 어느 클래스 소속인지 구분된다.</summary>
        public static string Qualified(Type type)
        {
            if (type == null)
                return "?";
            if (type.IsNested && type.DeclaringType != null && !type.IsGenericParameter)
                return Qualified(type.DeclaringType) + "." + Friendly(type);
            return Friendly(type);
        }

        /// <summary>소스 파일에서 선언을 찾을 때 쓰는, 제네릭 개수 표시(`1)를 뺀 이름.</summary>
        public static string StripArity(string name)
        {
            int arityIndex = name.IndexOf('`');
            return arityIndex < 0 ? name : name.Substring(0, arityIndex);
        }
    }
}
