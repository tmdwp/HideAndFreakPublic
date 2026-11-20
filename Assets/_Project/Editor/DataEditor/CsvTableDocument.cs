using System.Collections.Generic;
using System.IO;
using System.Text;
using GuardVsMannequin.Core;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 편집기로 저장해도 주석·빈 줄·BOM·줄바꿈 방식이 그대로 남도록 CSV 파일 전체를 줄 단위로 들고 있는 문서.
    /// </summary>
    internal sealed class CsvTableDocument
    {
        private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

        private readonly string newLine;
        private readonly bool endsWithNewLine;

        public string AssetPath { get; }
        public bool HasBom { get; }
        public List<CsvLine> Lines { get; }

        private CsvTableDocument(string assetPath, bool hasBom, string newLine, bool endsWithNewLine, List<CsvLine> lines)
        {
            AssetPath = assetPath;
            HasBom = hasBom;
            this.newLine = newLine;
            this.endsWithNewLine = endsWithNewLine;
            Lines = lines;
        }

        public CsvLine Header => Lines.Find(line => line.Kind == CsvLineKind.Header);

        public int HeaderIndex => Lines.FindIndex(line => line.Kind == CsvLineKind.Header);

        public int ColumnCount => Header?.Values.Count ?? 0;

        public IEnumerable<CsvLine> Rows
        {
            get
            {
                foreach (CsvLine line in Lines)
                {
                    if (line.Kind == CsvLineKind.Row)
                        yield return line;
                }
            }
        }

        /// <summary>엑셀이 붙이는 BOM 여부를 기억해 저장할 때 그대로 되살리기 위해 바이트로 읽는다.</summary>
        public static CsvTableDocument Load(string assetPath)
        {
            byte[] bytes = File.ReadAllBytes(assetPath);
            bool hasBom = bytes.Length >= 3 && bytes[0] == Utf8Bom[0] && bytes[1] == Utf8Bom[1] && bytes[2] == Utf8Bom[2];
            int offset = hasBom ? 3 : 0;
            string text = new UTF8Encoding(false).GetString(bytes, offset, bytes.Length - offset);
            return Parse(assetPath, text, hasBom);
        }

        /// <summary>되돌리기·도메인 재로드 뒤에도 같은 문서를 만들도록 텍스트에서 줄 목록을 다시 만든다.</summary>
        public static CsvTableDocument Parse(string assetPath, string text, bool hasBom)
        {
            text ??= string.Empty;
            string newLine = text.Contains("\r\n") ? "\r\n" : "\n";
            string normalized = text.Replace("\r\n", "\n");
            bool endsWithNewLine = normalized.EndsWith("\n");
            if (endsWithNewLine)
                normalized = normalized.Substring(0, normalized.Length - 1);

            var lines = new List<CsvLine>();
            bool hasHeader = false;
            foreach (string rawLine in normalized.Split('\n'))
                lines.Add(ParseLine(rawLine, ref hasHeader));
            return new CsvTableDocument(assetPath, hasBom, newLine, endsWithNewLine, lines);
        }

        /// <summary>게임의 CsvParser와 같은 기준(빈 줄, # 주석, 첫 데이터 줄 = 머리글)으로 줄 종류를 나눈다.</summary>
        private static CsvLine ParseLine(string rawLine, ref bool hasHeader)
        {
            string trimmed = rawLine.Trim();
            if (trimmed.Length == 0)
                return CsvLine.FromBlank(rawLine);
            if (trimmed.StartsWith("#"))
                return CsvLine.FromComment(rawLine);

            List<string[]> parsed = CsvParser.Parse(rawLine);
            string[] values = parsed.Count > 0 ? parsed[0] : new string[0];
            CsvLineKind kind = hasHeader ? CsvLineKind.Row : CsvLineKind.Header;
            hasHeader = true;
            return CsvLine.FromData(kind, rawLine, values);
        }

        public string ToText()
        {
            var builder = new StringBuilder();
            for (int i = 0; i < Lines.Count; i++)
            {
                if (i > 0)
                    builder.Append(newLine);
                builder.Append(Lines[i].ToText());
            }
            if (endsWithNewLine)
                builder.Append(newLine);
            return builder.ToString();
        }

        public void Save(string text)
        {
            File.WriteAllText(AssetPath, text, new UTF8Encoding(HasBom));
        }

        public int LineNumberOf(CsvLine line) => Lines.IndexOf(line) + 1;

        public string ColumnName(int column) => Header?.Get(column) ?? string.Empty;

        public int IndexOfColumn(string name) => Header?.Values.IndexOf(name) ?? -1;

        /// <summary>
        /// 기준 줄 바로 아래(구역 제목이면 그 구역 맨 위)에 넣고, 기준이 없으면 파일 끝 빈 줄 앞에 넣어 마지막 구역에 붙인다.
        /// </summary>
        public CsvLine InsertRowAfter(CsvLine anchor)
        {
            return Insert(CsvLine.CreateRow(ColumnCount), anchor);
        }

        public CsvLine DuplicateRow(CsvLine source)
        {
            return Insert(source.CloneRow(), source);
        }

        /// <summary>구역 사이 빈 줄 관례를 지키도록 빈 줄과 # 제목을 함께 넣는다.</summary>
        public CsvLine AddSection(string title, CsvLine anchor)
        {
            CsvLine comment = CsvLine.FromComment($"# {title}");
            int index = FindInsertIndex(anchor);
            Lines.Insert(index, CsvLine.FromBlank(string.Empty));
            Lines.Insert(index + 1, comment);
            return comment;
        }

        private CsvLine Insert(CsvLine line, CsvLine anchor)
        {
            Lines.Insert(FindInsertIndex(anchor), line);
            return line;
        }

        private int FindInsertIndex(CsvLine anchor)
        {
            int anchorIndex = anchor == null ? -1 : Lines.IndexOf(anchor);
            if (anchorIndex >= 0)
                return anchorIndex + 1;

            int index = Lines.Count;
            while (index > HeaderIndex + 1 && Lines[index - 1].Kind == CsvLineKind.Blank)
                index--;
            return index;
        }

        /// <summary>머리글이 사라지면 열 이름을 잃으므로 머리글은 지우지 않는다.</summary>
        public bool Remove(CsvLine line)
        {
            return line.Kind != CsvLineKind.Header && Lines.Remove(line);
        }

        /// <summary>
        /// 눈에 보이지 않는 빈 줄과 바꾸면 버튼을 눌러도 안 움직인 것처럼 보이므로 빈 줄은 건너뛰고 옆의 보이는 줄과 자리를 바꾼다.
        /// </summary>
        public bool Move(CsvLine line, int direction)
        {
            int index = Lines.IndexOf(line);
            if (index < 0 || line.Kind == CsvLineKind.Header || direction == 0)
                return false;

            int step = direction > 0 ? 1 : -1;
            int target = index + step;
            while (target >= 0 && target < Lines.Count && Lines[target].Kind == CsvLineKind.Blank)
                target += step;
            if (target <= HeaderIndex || target >= Lines.Count)
                return false;

            Lines.RemoveAt(index);
            Lines.Insert(target, line);
            return true;
        }

        /// <summary>새 언어처럼 열을 늘릴 때 데이터 줄의 칸 위치가 밀리지 않도록 모든 줄에 같은 위치로 빈 칸을 끼운다.</summary>
        public void InsertColumn(int column, string name)
        {
            foreach (CsvLine line in Lines)
            {
                if (!line.IsData)
                    continue;
                if (line.Kind == CsvLineKind.Header)
                {
                    line.Set(column - 1, line.Get(column - 1));
                    line.Values.Insert(column, name);
                }
                else if (line.Values.Count >= column)
                {
                    line.Values.Insert(column, string.Empty);
                }
            }
        }

        public void RemoveColumn(int column)
        {
            foreach (CsvLine line in Lines)
            {
                if (line.IsData && line.Values.Count > column)
                    line.Values.RemoveAt(column);
            }
        }
    }
}
