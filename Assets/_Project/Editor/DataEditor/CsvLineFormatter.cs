using System.Collections.Generic;
using System.Text;

namespace GuardVsMannequin.Editor
{
    /// <summary>편집기가 저장한 파일을 게임의 CsvParser가 같은 값으로 다시 읽도록, 읽는 규칙의 반대로 한 줄을 쓴다.</summary>
    internal static class CsvLineFormatter
    {
        public static string Format(IReadOnlyList<string> values)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append(FormatValue(values[i] ?? string.Empty, i == 0));
            }
            return builder.ToString();
        }

        /// <summary>
        /// 한 줄에 실제 줄바꿈을 쓸 수 없어 \n 표기로 바꾸고, 쉼표·따옴표가 든 값과 첫 칸이 #이라 주석으로 오해받을 값은 큰따옴표로 감싼다.
        /// </summary>
        public static string FormatValue(string value, bool isFirstColumn)
        {
            string escaped = EscapeNewLines(value);
            bool needsQuotes = escaped.IndexOf(',') >= 0
                || escaped.IndexOf('"') >= 0
                || (isFirstColumn && escaped.TrimStart().StartsWith("#"));
            return needsQuotes ? "\"" + escaped.Replace("\"", "\"\"") + "\"" : escaped;
        }

        /// <summary>표의 한 줄 입력칸에서도 줄바꿈을 보고 고칠 수 있도록 파일과 같은 \n 표기로 바꾼다.</summary>
        public static string EscapeNewLines(string value)
        {
            return (value ?? string.Empty).Replace("\r\n", "\n").Replace("\n", "\\n");
        }

        public static string UnescapeNewLines(string value)
        {
            return (value ?? string.Empty).Replace("\\n", "\n");
        }
    }
}
