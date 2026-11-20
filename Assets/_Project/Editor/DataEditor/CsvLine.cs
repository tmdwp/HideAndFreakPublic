using System.Collections.Generic;

namespace GuardVsMannequin.Editor
{
    /// <summary>주석과 빈 줄도 지우지 않고 다시 쓰기 위해 데이터 줄과 구분하는 줄 종류.</summary>
    internal enum CsvLineKind
    {
        Blank,
        Comment,
        Header,
        Row,
    }

    /// <summary>
    /// 편집기로 저장해도 손대지 않은 줄의 따옴표·공백이 바뀌어 diff가 커지지 않도록, 원문을 기억했다가 값이 그대로면 원문을 쓰는 CSV 한 줄.
    /// </summary>
    internal sealed class CsvLine
    {
        private const string CommentPrefix = "#";

        private readonly string sourceText;
        private readonly string[] sourceValues;

        public CsvLineKind Kind { get; }

        /// <summary>머리글·데이터 줄의 칸 값. 주석·빈 줄은 null.</summary>
        public List<string> Values { get; }

        /// <summary>주석·빈 줄의 원문.</summary>
        public string Text { get; private set; }

        public bool IsData => Kind == CsvLineKind.Header || Kind == CsvLineKind.Row;

        private CsvLine(CsvLineKind kind, string sourceText, List<string> values)
        {
            Kind = kind;
            Text = values == null ? sourceText : null;
            Values = values;
            this.sourceText = sourceText;
            sourceValues = values?.ToArray();
        }

        public static CsvLine FromBlank(string text) => new CsvLine(CsvLineKind.Blank, text, null);

        public static CsvLine FromComment(string text) => new CsvLine(CsvLineKind.Comment, text, null);

        public static CsvLine FromData(CsvLineKind kind, string text, IEnumerable<string> values) => new CsvLine(kind, text, new List<string>(values));

        /// <summary>원문이 없는 새 줄이라 저장할 때 항상 값으로 다시 쓴다.</summary>
        public static CsvLine CreateRow(int columnCount)
        {
            var values = new List<string>(columnCount);
            for (int i = 0; i < columnCount; i++)
                values.Add(string.Empty);
            return new CsvLine(CsvLineKind.Row, null, values);
        }

        /// <summary>구역 이름처럼 보여 주기 위해 앞의 # 과 공백을 뗀 주석 내용.</summary>
        public string CommentBody
        {
            get => Text == null ? string.Empty : Text.TrimStart().TrimStart('#').TrimStart();
            set => Text = $"{CommentPrefix} {value}";
        }

        /// <summary>짧은 줄도 열 개수만큼 있는 것처럼 다루도록 없는 칸은 빈 문자열로 돌려준다.</summary>
        public string Get(int column)
        {
            return Values != null && column >= 0 && column < Values.Count ? Values[column] ?? string.Empty : string.Empty;
        }

        /// <summary>짧은 줄의 뒤쪽 칸에 써도 되도록 모자란 칸을 채운 뒤 넣는다.</summary>
        public void Set(int column, string value)
        {
            if (Values == null || column < 0)
                return;
            while (Values.Count <= column)
                Values.Add(string.Empty);
            Values[column] = value ?? string.Empty;
        }

        /// <summary>줄 복제는 원문을 이어받지 않아야 저장 시 새 값으로 쓰이므로 값만 복사한다.</summary>
        public CsvLine CloneRow()
        {
            return new CsvLine(CsvLineKind.Row, null, new List<string>(Values ?? new List<string>()));
        }

        public string ToText()
        {
            if (!IsData)
                return Text ?? string.Empty;
            if (sourceText != null && IsUnchanged())
                return sourceText;
            return CsvLineFormatter.Format(Values);
        }

        private bool IsUnchanged()
        {
            if (sourceValues == null || sourceValues.Length != Values.Count)
                return false;
            for (int i = 0; i < sourceValues.Length; i++)
            {
                if (sourceValues[i] != Values[i])
                    return false;
            }
            return true;
        }
    }
}
