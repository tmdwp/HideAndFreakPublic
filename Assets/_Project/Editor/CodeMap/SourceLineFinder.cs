using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace GuardVsMannequin.Editor.CodeMap
{
    /// <summary>
    /// 에디터용 어셈블리에는 줄 번호 정보가 없어, 소스 텍스트에서 선언 줄을 찾아 코드 열기 위치를 정한다.
    /// 호출 줄(obj.Foo(), return Foo())을 선언으로 잘못 고르지 않는 것이 핵심이다.
    /// </summary>
    internal static class SourceLineFinder
    {
        private static readonly Regex declarationPrefix = new Regex(@"^[\w\[\]<>,\.\?\s]+$", RegexOptions.Compiled);
        private static readonly Regex attributeBlock = new Regex(@"\[[^\]]*\]", RegexOptions.Compiled);

        private static readonly string[] statementKeywords =
        {
            "return", "new", "await", "yield", "throw", "else", "case", "in", "is", "as",
            "using", "nameof", "typeof", "out", "ref", "goto", "when", "var",
        };

        /// <summary>member 선언이 있는 1부터 시작하는 줄 번호. 못 찾으면 0.</summary>
        public static int FindMember(string text, CodeMember member)
        {
            Regex pattern = BuildPattern(member);
            if (pattern == null)
                return 0;

            int expectedParameters = member.Member is System.Reflection.MethodBase method ? method.GetParameters().Length : -1;
            string[] lines = text.Split('\n');
            int firstMatchLine = 0;
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index];
                if (IsCommentLine(line))
                    continue;

                Match match = pattern.Match(line);
                if (!match.Success || !LooksLikeDeclaration(line.Substring(0, match.Index), member.Kind))
                    continue;

                if (expectedParameters < 0)
                    return index + 1;
                if (firstMatchLine == 0)
                    firstMatchLine = index + 1;
                // 오버로드가 여러 개면 매개변수 개수가 같은 줄을 고른다.
                if (CountParameters(line, match.Index + match.Length) == expectedParameters)
                    return index + 1;
            }

            return firstMatchLine;
        }

        /// <summary>타입 선언 줄. 멤버 줄을 못 찾았을 때 최소한 클래스 위치로 열기 위해 쓴다.</summary>
        public static int FindType(string text, Type type)
        {
            Regex pattern = TypeDeclarationPattern(type);
            string[] lines = text.Split('\n');
            for (int index = 0; index < lines.Length; index++)
            {
                if (!IsCommentLine(lines[index]) && pattern.IsMatch(lines[index]))
                    return index + 1;
            }

            return 0;
        }

        public static Regex TypeDeclarationPattern(Type type)
        {
            string name = Regex.Escape(CodeTypeNames.StripArity(type.Name));
            return new Regex(@"\b(class|struct|interface|enum|record)\s+" + name + @"\b");
        }

        private static Regex BuildPattern(CodeMember member)
        {
            string name = Regex.Escape(member.Kind == CodeMemberKind.Constructor
                ? CodeTypeNames.StripArity(member.DeclaringType.Name)
                : member.Member.Name);

            switch (member.Kind)
            {
                case CodeMemberKind.Method:
                case CodeMemberKind.Constructor:
                    return new Regex(@"\b" + name + @"\s*(<[^()]*>)?\s*\(");
                case CodeMemberKind.Field:
                    return new Regex(@"\b" + name + @"\s*(=(?![=>])|;|,)");
                case CodeMemberKind.Property:
                    if (member.Member.Name == "Item")
                        return new Regex(@"\bthis\s*\[");
                    return new Regex(@"\b" + name + @"\s*(\{|=>|$)");
                case CodeMemberKind.Event:
                    return new Regex(@"\bevent\b.*\b" + name + @"\b");
                default:
                    return null;
            }
        }

        private static bool LooksLikeDeclaration(string prefix, CodeMemberKind kind)
        {
            if (kind == CodeMemberKind.Event)
                return true;

            // obj.Foo(, obj?.Foo( 처럼 이름 바로 앞이 공백이 아니면 호출·접근이다.
            if (prefix.Length > 0 && !char.IsWhiteSpace(prefix[prefix.Length - 1]))
                return false;

            string cleaned = attributeBlock.Replace(prefix, " ").Trim();
            if (cleaned.Length == 0)
                return kind == CodeMemberKind.Constructor;
            if (!declarationPrefix.IsMatch(cleaned))
                return false;

            string lastWord = cleaned.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Last();
            return !statementKeywords.Contains(lastWord);
        }

        /// <summary>여는 괄호 뒤부터 같은 줄의 닫는 괄호까지 최상위 쉼표를 센다. 줄을 넘기면 -1.</summary>
        private static int CountParameters(string line, int startAfterParenthesis)
        {
            int depth = 0;
            int commas = 0;
            bool hasContent = false;
            for (int index = startAfterParenthesis; index < line.Length; index++)
            {
                char character = line[index];
                switch (character)
                {
                    case '(':
                    case '<':
                    case '[':
                        depth++;
                        break;
                    case ')' when depth == 0:
                        return hasContent ? commas + 1 : 0;
                    case ')':
                    case '>':
                    case ']':
                        depth--;
                        break;
                    case ',' when depth == 0:
                        commas++;
                        break;
                }

                if (!char.IsWhiteSpace(character))
                    hasContent = true;
            }

            return -1;
        }

        private static bool IsCommentLine(string line)
        {
            string trimmed = line.TrimStart();
            return trimmed.StartsWith("//", StringComparison.Ordinal)
                || trimmed.StartsWith("*", StringComparison.Ordinal)
                || trimmed.StartsWith("/*", StringComparison.Ordinal);
        }
    }
}
