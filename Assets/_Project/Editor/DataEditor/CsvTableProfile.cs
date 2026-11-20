using System.Collections.Generic;

namespace GuardVsMannequin.Editor
{
    /// <summary>편집기 창이 파일 종류를 몰라도 되도록, 파일별 설명·검사 규칙·열 너비를 한곳에 묶는다.</summary>
    internal sealed class CsvTableProfile
    {
        private const float FallbackColumnWidth = 160f;

        private readonly Dictionary<string, float> columnWidths;
        private readonly float otherColumnWidth;

        public string AssetPath { get; }
        public string DisplayName { get; }
        public string Summary { get; }
        public IReadOnlyList<ICsvTableRule> Rules { get; }

        /// <summary>로더가 열 위치·머리글 이름에 기대는 파일은 열 구조를 못 바꾸게 한다.</summary>
        public bool AllowColumnEdit { get; }

        public CsvTableProfile(string assetPath, string displayName, string summary, IReadOnlyList<ICsvTableRule> rules,
            bool allowColumnEdit, Dictionary<string, float> columnWidths = null, float otherColumnWidth = FallbackColumnWidth)
        {
            AssetPath = assetPath;
            DisplayName = displayName;
            Summary = summary;
            Rules = rules;
            AllowColumnEdit = allowColumnEdit;
            this.columnWidths = columnWidths ?? new Dictionary<string, float>();
            this.otherColumnWidth = otherColumnWidth;
        }

        public float GetDefaultWidth(string columnName)
        {
            return columnWidths.TryGetValue(columnName ?? string.Empty, out float width) ? width : otherColumnWidth;
        }

        public List<CsvIssue> Validate(CsvTableDocument document)
        {
            var issues = new List<CsvIssue>();
            if (document.Header == null)
            {
                issues.Add(new CsvIssue(CsvIssueLevel.Error, null, -1, "머리글 줄이 없습니다. 주석이 아닌 첫 줄이 열 이름이어야 합니다."));
                return issues;
            }

            foreach (ICsvTableRule rule in Rules)
                rule.Check(document, issues);
            return issues;
        }
    }
}
