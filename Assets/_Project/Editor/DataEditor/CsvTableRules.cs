using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace GuardVsMannequin.Editor
{
    internal enum CsvIssueLevel
    {
        Info,
        Warning,
        Error,
    }

    /// <summary>게임이 조용히 무시하거나 기본값으로 물러나는 줄을 저장 전에 보여 주기 위한 검사 결과.</summary>
    internal sealed class CsvIssue
    {
        public CsvIssueLevel Level { get; }

        /// <summary>파일 전체 문제면 null.</summary>
        public CsvLine Line { get; }

        /// <summary>줄 전체 문제면 -1.</summary>
        public int Column { get; }

        public string Message { get; }
        public string FixLabel { get; }
        public Action<CsvTableDocument> Fix { get; }

        public CsvIssue(CsvIssueLevel level, CsvLine line, int column, string message, string fixLabel = null, Action<CsvTableDocument> fix = null)
        {
            Level = level;
            Line = line;
            Column = column;
            Message = message;
            FixLabel = fixLabel;
            Fix = fix;
        }
    }

    /// <summary>파일마다 필요한 검사만 골라 조합하도록 검사 하나를 한 규칙으로 나눈다.</summary>
    internal interface ICsvTableRule
    {
        void Check(CsvTableDocument document, List<CsvIssue> issues);
    }

    /// <summary>코드가 알고 있는 키와 줄을 추가할 때 넣을 기본 칸 값.</summary>
    internal readonly struct CsvKnownKey
    {
        public readonly string Key;

        /// <summary>null이면 기본값을 몰라 자동 추가를 제공하지 않는다.</summary>
        public readonly string[] DefaultValues;

        public CsvKnownKey(string key, string[] defaultValues)
        {
            Key = key;
            DefaultValues = defaultValues;
        }
    }

    /// <summary>모든 로더가 첫 칸을 키로 찾으므로, 비었거나 겹친 키는 어느 한 줄이 쓰이지 않는다는 뜻이라 오류로 본다.</summary>
    internal sealed class KeyColumnRule : ICsvTableRule
    {
        public void Check(CsvTableDocument document, List<CsvIssue> issues)
        {
            var firstLineByKey = new Dictionary<string, CsvLine>();
            foreach (CsvLine row in document.Rows)
            {
                string key = row.Get(0);
                if (key.Length == 0)
                {
                    issues.Add(new CsvIssue(CsvIssueLevel.Error, row, 0, "첫 칸(키)이 비어 있어 게임이 이 줄을 쓰지 못합니다."));
                }
                else if (firstLineByKey.TryGetValue(key, out CsvLine first))
                {
                    issues.Add(new CsvIssue(CsvIssueLevel.Error, row, 0, $"'{key}'가 {document.LineNumberOf(first)}번째 줄과 겹칩니다. 두 줄 중 하나만 쓰입니다."));
                }
                else
                {
                    firstLineByKey.Add(key, row);
                }
            }
        }
    }

    /// <summary>머리글보다 긴 줄은 쉼표를 따옴표 없이 넣은 실수인 경우가 많아 알린다.</summary>
    internal sealed class ExtraCellsRule : ICsvTableRule
    {
        public void Check(CsvTableDocument document, List<CsvIssue> issues)
        {
            int columnCount = document.ColumnCount;
            foreach (CsvLine row in document.Rows)
            {
                List<string> extra = row.Values.Skip(columnCount).Where(value => value.Length > 0).ToList();
                if (extra.Count > 0)
                    issues.Add(new CsvIssue(CsvIssueLevel.Warning, row, -1, $"머리글보다 칸이 많습니다 (넘친 값: {string.Join(", ", extra)}). 쉼표가 든 문장은 따옴표로 감싸야 합니다."));
            }
        }
    }

    /// <summary>게임은 '.' 소수점 숫자만 읽고 나머지는 기본값으로 물러나므로 숫자 열을 미리 확인한다.</summary>
    internal sealed class NumberColumnRule : ICsvTableRule
    {
        private readonly string columnName;
        private readonly bool mustBePositive;

        public NumberColumnRule(string columnName, bool mustBePositive = false)
        {
            this.columnName = columnName;
            this.mustBePositive = mustBePositive;
        }

        public void Check(CsvTableDocument document, List<CsvIssue> issues)
        {
            int column = document.IndexOfColumn(columnName);
            if (column < 0)
            {
                issues.Add(new CsvIssue(CsvIssueLevel.Error, null, -1, $"'{columnName}' 열이 없습니다. 머리글을 확인하세요."));
                return;
            }

            foreach (CsvLine row in document.Rows)
            {
                if (!TryParse(row.Get(column), out float value))
                    issues.Add(new CsvIssue(CsvIssueLevel.Error, row, column, $"{columnName} 값 '{row.Get(column)}'이 숫자가 아닙니다. (소수점은 '.')"));
                else if (mustBePositive && value <= 0f)
                    issues.Add(new CsvIssue(CsvIssueLevel.Error, row, column, $"{columnName}은 0보다 커야 합니다."));
            }
        }

        public static bool TryParse(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }

    /// <summary>범위가 뒤집힌 줄은 룸 설정 로더가 통째로 거부하므로 저장 전에 알린다.</summary>
    internal sealed class MinMaxRule : ICsvTableRule
    {
        private readonly string minColumnName;
        private readonly string maxColumnName;

        public MinMaxRule(string minColumnName, string maxColumnName)
        {
            this.minColumnName = minColumnName;
            this.maxColumnName = maxColumnName;
        }

        public void Check(CsvTableDocument document, List<CsvIssue> issues)
        {
            int minColumn = document.IndexOfColumn(minColumnName);
            int maxColumn = document.IndexOfColumn(maxColumnName);
            if (minColumn < 0 || maxColumn < 0)
                return;

            foreach (CsvLine row in document.Rows)
            {
                if (NumberColumnRule.TryParse(row.Get(minColumn), out float min)
                    && NumberColumnRule.TryParse(row.Get(maxColumn), out float max)
                    && max < min)
                {
                    issues.Add(new CsvIssue(CsvIssueLevel.Error, row, maxColumn, $"{maxColumnName}({max})가 {minColumnName}({min})보다 작습니다. 게임이 이 줄을 버립니다."));
                }
            }
        }
    }

    /// <summary>
    /// 키 이름은 코드의 항목 이름이라 오타가 나면 게임이 줄을 무시하므로 코드와 대조하고, 코드에만 있는 항목은 추가 버튼과 함께 알린다.
    /// </summary>
    internal sealed class KnownKeyRule : ICsvTableRule
    {
        private readonly Func<IReadOnlyList<CsvKnownKey>> knownKeySource;
        private readonly string sourceName;
        private readonly string missingMessage;

        public KnownKeyRule(Func<IReadOnlyList<CsvKnownKey>> knownKeySource, string sourceName, string missingMessage)
        {
            this.knownKeySource = knownKeySource;
            this.sourceName = sourceName;
            this.missingMessage = missingMessage;
        }

        public void Check(CsvTableDocument document, List<CsvIssue> issues)
        {
            IReadOnlyList<CsvKnownKey> knownKeys = knownKeySource();
            var knownNames = new HashSet<string>(knownKeys.Select(known => known.Key));
            var lineByKey = new Dictionary<string, CsvLine>();

            foreach (CsvLine row in document.Rows)
            {
                string key = row.Get(0);
                if (key.Length == 0)
                    continue;
                if (!knownNames.Contains(key))
                    issues.Add(new CsvIssue(CsvIssueLevel.Error, row, 0, $"'{key}'는 {sourceName}에 없는 이름이라 게임이 무시합니다. 오타를 확인하세요."));
                else if (!lineByKey.ContainsKey(key))
                    lineByKey.Add(key, row);
            }

            for (int i = 0; i < knownKeys.Count; i++)
            {
                CsvKnownKey known = knownKeys[i];
                if (lineByKey.ContainsKey(known.Key))
                    continue;

                CsvLine anchor = FindPreviousPresentLine(knownKeys, i, lineByKey);
                string[] defaults = known.DefaultValues;
                Action<CsvTableDocument> fix = defaults == null ? null : doc => AddRow(doc, anchor, defaults);
                issues.Add(new CsvIssue(CsvIssueLevel.Info, null, -1, $"'{known.Key}': {missingMessage}", defaults == null ? null : "줄 추가", fix));
            }
        }

        /// <summary>코드에 적힌 순서대로 같은 구역에 들어가도록, 코드에서 바로 앞 항목이 있는 줄 아래에 넣는다.</summary>
        private static CsvLine FindPreviousPresentLine(IReadOnlyList<CsvKnownKey> knownKeys, int index, Dictionary<string, CsvLine> lineByKey)
        {
            for (int i = index - 1; i >= 0; i--)
            {
                if (lineByKey.TryGetValue(knownKeys[i].Key, out CsvLine line))
                    return line;
            }
            return null;
        }

        private static void AddRow(CsvTableDocument document, CsvLine anchor, string[] values)
        {
            CsvLine row = document.InsertRowAfter(document.Lines.Contains(anchor) ? anchor : null);
            for (int column = 0; column < values.Length; column++)
                row.Set(column, values[column]);
        }
    }

    /// <summary>단위·이름 문구처럼 다른 파일의 키를 가리키는 값은, 키가 없으면 화면에 키 이름이 그대로 보이므로 대조한다.</summary>
    internal sealed class KeyReferenceRule : ICsvTableRule
    {
        private readonly string sourceColumnName;
        private readonly string keyFormat;
        private readonly Func<HashSet<string>> keySource;
        private readonly string targetName;

        /// <param name="sourceColumnName">null이면 첫 칸(키)을 쓴다.</param>
        /// <param name="keyFormat">칸 값을 대상 키로 바꾸는 형식. 예: "setting.{0}"</param>
        public KeyReferenceRule(string sourceColumnName, string keyFormat, Func<HashSet<string>> keySource, string targetName)
        {
            this.sourceColumnName = sourceColumnName;
            this.keyFormat = keyFormat;
            this.keySource = keySource;
            this.targetName = targetName;
        }

        public void Check(CsvTableDocument document, List<CsvIssue> issues)
        {
            HashSet<string> keys = keySource();
            int column = sourceColumnName == null ? 0 : document.IndexOfColumn(sourceColumnName);
            if (keys == null || column < 0)
                return;

            foreach (CsvLine row in document.Rows)
            {
                string value = row.Get(column);
                if (value.Length == 0)
                    continue;
                string key = string.Format(keyFormat, value);
                if (!keys.Contains(key))
                    issues.Add(new CsvIssue(CsvIssueLevel.Warning, row, column, $"{targetName}에 '{key}' 문구가 없어 화면에 키 이름이 그대로 보입니다."));
            }
        }
    }

    /// <summary>
    /// 번역이 비면 첫 언어로 표시되고, {0} 같은 자리 표시가 언어마다 다르면 값이 빠지거나 오류가 나므로 언어 열끼리 비교한다.
    /// </summary>
    internal sealed class TranslationRule : ICsvTableRule
    {
        private const int BaseLanguageColumn = 1;
        private static readonly Regex PlaceholderPattern = new Regex(@"\{\d+\}");

        public void Check(CsvTableDocument document, List<CsvIssue> issues)
        {
            if (document.ColumnCount <= BaseLanguageColumn)
                return;

            string baseLanguage = document.ColumnName(BaseLanguageColumn);
            foreach (CsvLine row in document.Rows)
            {
                string baseText = row.Get(BaseLanguageColumn);
                if (baseText.Length == 0)
                    issues.Add(new CsvIssue(CsvIssueLevel.Warning, row, BaseLanguageColumn, $"기본 언어({baseLanguage}) 문장이 비어 있습니다."));

                HashSet<string> basePlaceholders = FindPlaceholders(baseText);
                for (int column = BaseLanguageColumn + 1; column < document.ColumnCount; column++)
                {
                    string language = document.ColumnName(column);
                    string text = row.Get(column);
                    if (text.Length == 0)
                    {
                        issues.Add(new CsvIssue(CsvIssueLevel.Info, row, column, $"{language} 번역이 없어 {baseLanguage} 문장이 표시됩니다."));
                    }
                    else if (baseText.Length > 0 && !FindPlaceholders(text).SetEquals(basePlaceholders))
                    {
                        issues.Add(new CsvIssue(CsvIssueLevel.Warning, row, column,
                            $"자리 표시가 {baseLanguage}와 다릅니다. ({baseLanguage}: {Describe(basePlaceholders)} / {language}: {Describe(FindPlaceholders(text))})"));
                    }
                }
            }
        }

        private static HashSet<string> FindPlaceholders(string text)
        {
            return new HashSet<string>(PlaceholderPattern.Matches(text).Cast<Match>().Select(match => match.Value));
        }

        private static string Describe(HashSet<string> placeholders)
        {
            return placeholders.Count == 0 ? "없음" : string.Join(" ", placeholders.OrderBy(value => value));
        }
    }
}
