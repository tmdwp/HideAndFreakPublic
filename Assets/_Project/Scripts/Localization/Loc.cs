using System;
using System.Collections.Generic;
using GuardVsMannequin.Core;
using GuardVsMannequin.Settings;
using UnityEngine;

namespace GuardVsMannequin.Localization
{
    /// <summary>CSV에 열만 추가해도 새 언어가 되도록 화면 문장을 strings.csv에서 언어별로 제공한다.</summary>
    public static class Loc
    {
        private const string TablePath = "Localization/strings";
        private const string LanguageNameKey = "language.name";

        private static readonly Dictionary<string, Dictionary<string, string>> table = new();
        private static readonly List<string> languages = new();
        private static bool isLoaded;
        private static string currentLanguage;

        /// <summary>이미 표시된 문구들이 다시 그려지도록 알린다.</summary>
        public static event Action LanguageChanged;

        /// <summary>설정 화면이 CSV에 있는 언어만 고를 수 있도록 머리글 순서대로 준다.</summary>
        public static IReadOnlyList<string> Languages
        {
            get
            {
                EnsureLoaded();
                return languages;
            }
        }

        public static string CurrentLanguage
        {
            get
            {
                EnsureLoaded();
                return currentLanguage;
            }
        }

        /// <summary>
        /// 처음 쓰는 곳이 어디든 표가 먼저 읽히도록 조회할 때 확인한다.
        /// </summary>
        public static string Get(string key)
        {
            EnsureLoaded();
            return Lookup(key, currentLanguage);
        }

        /// <summary>
        /// 없는 키는 키 이름이 그대로 보이므로, 에디터 도구와 모드 검사가 빠진 문구를 찾을 수 있게 연다.
        /// </summary>
        public static bool HasKey(string key)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(key) && table.ContainsKey(key);
        }

        /// <summary>
        /// 번역자가 {0} 자리를 잘못 적어도 게임이 멈추지 않도록 예외 대신 원문을 보여 주고 경고한다.
        /// </summary>
        public static string Format(string key, params object[] args)
        {
            string pattern = Get(key);
            try
            {
                return string.Format(pattern, args);
            }
            catch (FormatException)
            {
                Debug.LogWarning($"[Loc] 형식이 맞지 않는 문장입니다: {key} = {pattern}");
                return pattern;
            }
        }

        /// <summary>다른 언어 사용자도 알아보도록 언어 목록에는 그 언어로 쓴 이름을 보여 준다.</summary>
        public static string GetLanguageName(string languageCode)
        {
            EnsureLoaded();
            return Lookup(LanguageNameKey, languageCode);
        }

        /// <summary>다음 실행에도 같은 언어로 시작하도록 설정에 저장한 뒤 알린다.</summary>
        public static void SetLanguage(string languageCode)
        {
            EnsureLoaded();
            if (!languages.Contains(languageCode) || languageCode == currentLanguage)
                return;

            currentLanguage = languageCode;
            GameOptions options = OptionsStore.Current;
            options.languageCode = languageCode;
            OptionsStore.SetOptions(options);
            LanguageChanged?.Invoke();
        }

        /// <summary>모드가 게임 문장을 덮어쓰지 못하도록 접두사로 시작하는 키만 받는다.</summary>
        /// <param name="csv">머리글: key, 언어 코드...</param>
        /// <param name="requiredPrefix">예: "mod.author.mymod."</param>
        public static int AddStrings(string csv, string requiredPrefix)
        {
            EnsureLoaded();
            List<string[]> rows = CsvParser.Parse(csv ?? string.Empty);
            if (rows.Count < 2 || rows[0].Length < 2)
                return 0;

            string[] header = rows[0];
            int addedCount = 0;
            for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                string[] row = rows[rowIndex];
                if (row.Length == 0 || string.IsNullOrEmpty(row[0]) || !row[0].StartsWith(requiredPrefix, StringComparison.Ordinal))
                    continue;

                var translations = new Dictionary<string, string>();
                for (int column = 1; column < header.Length && column < row.Length; column++)
                {
                    if (languages.Contains(header[column]) && !string.IsNullOrEmpty(row[column]))
                        translations[header[column]] = row[column];
                }
                table[row[0]] = translations;
                addedCount++;
            }
            return addedCount;
        }

        /// <summary>
        /// 에디터 도구가 CSV 수정 내용을 게임을 다시 켜지 않고 바로 반영할 수 있게 표를 다시 읽는다.
        /// </summary>
        public static void Reload()
        {
            isLoaded = false;
            table.Clear();
            languages.Clear();
            EnsureLoaded();
            LanguageChanged?.Invoke();
        }

        /// <summary>CSV를 매번 읽지 않도록 한 번만 불러오고, 파일이 깨져도 화면이 비지 않게 ko로 시작한다.</summary>
        private static void EnsureLoaded()
        {
            if (isLoaded)
                return;
            isLoaded = true;

            // 빌드 폴더의 GameData/Localization/strings.csv를 먼저 보고, 없거나 깨졌으면 내장 원본을 쓴다.
            if (!DataFiles.TryLoad<List<string[]>>(TablePath, ".csv", TryParseTable, out List<string[]> rows))
            {
                Debug.LogError($"[Loc] 문장 파일을 사용할 수 없습니다: {TablePath}.csv");
                languages.Add("ko");
                currentLanguage = "ko";
                return;
            }

            LoadTable(rows);
            currentLanguage = ChooseInitialLanguage();
        }

        /// <summary>
        /// 머리글(key, 언어...)과 문장 한 줄 이상이 없으면 쓸 수 없으므로 깨진 파일로 보고 내장 원본으로 물러나게 한다.
        /// </summary>
        private static bool TryParseTable(string csv, out List<string[]> rows)
        {
            rows = CsvParser.Parse(csv);
            return rows.Count >= 2 && rows[0].Length >= 2;
        }

        /// <summary>
        /// 빈 칸을 표에 넣으면 첫 언어로 물러나지 않고 빈 문구가 보이므로, 값이 있는 칸만 넣는다.
        /// </summary>
        private static void LoadTable(List<string[]> rows)
        {
            string[] header = rows[0];
            for (int column = 1; column < header.Length; column++)
                languages.Add(header[column]);

            for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                string[] row = rows[rowIndex];
                if (row.Length == 0 || string.IsNullOrEmpty(row[0]))
                    continue;

                var translations = new Dictionary<string, string>();
                for (int column = 1; column < header.Length && column < row.Length; column++)
                {
                    if (!string.IsNullOrEmpty(row[column]))
                        translations[header[column]] = row[column];
                }
                table[row[0]] = translations;
            }
        }

        /// <summary>처음 켜는 사람도 읽을 수 있도록 저장된 언어가 없으면 시스템 언어를 고른다.</summary>
        private static string ChooseInitialLanguage()
        {
            string savedLanguage = OptionsStore.Current.languageCode;
            if (!string.IsNullOrEmpty(savedLanguage) && languages.Contains(savedLanguage))
                return savedLanguage;

            string systemLanguage = Application.systemLanguage == SystemLanguage.Korean ? "ko" : "en";
            return languages.Contains(systemLanguage) ? systemLanguage : languages[0];
        }

        /// <summary>
        /// 번역이 덜 된 언어에서도 빈 칸이 보이지 않도록 첫 번째 언어로 물러나고, 키가 없으면 무엇이 빠졌는지 보이게 키 이름을 준다.
        /// </summary>
        private static string Lookup(string key, string languageCode)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;
            if (!table.TryGetValue(key, out Dictionary<string, string> translations))
                return key;
            if (translations.TryGetValue(languageCode, out string text))
                return text;
            return languages.Count > 0 && translations.TryGetValue(languages[0], out string fallbackText) ? fallbackText : key;
        }
    }
}
