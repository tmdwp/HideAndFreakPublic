using System.Collections.Generic;
using System.Text;

namespace GuardVsMannequin.Core
{
    /// <summary>사람이 엑셀·텍스트 편집기로 고치는 CSV를 읽도록 큰따옴표 묶음, \n 줄바꿈, # 메모 줄을 처리한다.</summary>
    public static class CsvParser
    {
        /// <summary>열 이름으로 찾는 쪽이 있어 머리글 행도 돌려주고, OS마다 다른 줄바꿈을 같게 맞춘다.</summary>
        public static List<string[]> Parse(string text)
        {
            var rows = new List<string[]>();
            if (string.IsNullOrEmpty(text))
                return rows;

            // 엑셀이 UTF-8 CSV에 붙이는 BOM 제거
            if (text[0] == '﻿')
                text = text.Substring(1);

            foreach (string rawLine in text.Replace("\r\n", "\n").Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                if (line.Trim().Length == 0 || line.TrimStart().StartsWith("#"))
                    continue;
                rows.Add(ParseLine(line));
            }
            return rows;
        }

        /// <summary>
        /// 문장에 쉼표가 자주 들어가 Split으로는 나눌 수 없으므로, 큰따옴표 안인지 추적하며 한 글자씩 나눈다.
        /// </summary>
        private static string[] ParseLine(string line)
        {
            var values = new List<string>();
            var currentValue = new StringBuilder();
            bool isInQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char character = line[i];
                if (isInQuotes)
                {
                    if (character == '"' && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentValue.Append('"');
                        i++;
                    }
                    else if (character == '"')
                    {
                        isInQuotes = false;
                    }
                    else
                    {
                        currentValue.Append(character);
                    }
                }
                else if (character == '"')
                {
                    isInQuotes = true;
                }
                else if (character == ',')
                {
                    values.Add(Unescape(currentValue.ToString()));
                    currentValue.Clear();
                }
                else
                {
                    currentValue.Append(character);
                }
            }

            values.Add(Unescape(currentValue.ToString()));
            return values.ToArray();
        }

        /// <summary>
        /// 엑셀에서 정렬용으로 넣은 공백이 값에 섞이지 않도록 지우고, 한 줄에 쓸 수 없는 줄바꿈을 \n 표기에서 되돌린다.
        /// </summary>
        private static string Unescape(string value)
        {
            return value.Trim().Replace("\\n", "\n");
        }
    }
}
