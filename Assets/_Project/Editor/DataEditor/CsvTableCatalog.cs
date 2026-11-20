using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using GuardVsMannequin.Core;
using GuardVsMannequin.Room;

namespace GuardVsMannequin.Editor
{
    /// <summary>게임이 읽는 CSV를 찾아 파일마다 맞는 검사 규칙을 붙인다. 새 CSV는 기본 규칙으로 자동으로 목록에 나타난다.</summary>
    internal static class CsvTableCatalog
    {
        private const string SearchFolder = "Assets/_Project/Resources";
        private const string TuningFileName = "game_tuning.csv";
        private const string SettingsFileName = "game_settings.csv";
        private const string StringsFileName = "strings.csv";
        private const string StringsPath = SearchFolder + "/Localization/" + StringsFileName;

        private static readonly string[] KnownFileOrder = { TuningFileName, SettingsFileName, StringsFileName };

        public static List<CsvTableProfile> FindAll()
        {
            if (!Directory.Exists(SearchFolder))
                return new List<CsvTableProfile>();

            return Directory.GetFiles(SearchFolder, "*.csv", SearchOption.AllDirectories)
                .Select(file => file.Replace('\\', '/'))
                .OrderBy(path => OrderOf(Path.GetFileName(path)))
                .ThenBy(path => path, StringComparer.Ordinal)
                .Select(CreateProfile)
                .ToList();
        }

        private static int OrderOf(string fileName)
        {
            int index = Array.IndexOf(KnownFileOrder, fileName);
            return index < 0 ? KnownFileOrder.Length : index;
        }

        private static CsvTableProfile CreateProfile(string path)
        {
            switch (Path.GetFileName(path))
            {
                case TuningFileName: return CreateTuningProfile(path);
                case SettingsFileName: return CreateSettingsProfile(path);
                case StringsFileName: return CreateStringsProfile(path);
                default: return CreateGenericProfile(path);
            }
        }

        private static CsvTableProfile CreateTuningProfile(string path)
        {
            return new CsvTableProfile(path, "밸런스 수치 (game_tuning)",
                "value만 고치면 됩니다. id는 코드(GameTuning) 이름이라 바꾸면 안 됩니다. 줄이 없으면 코드 기본값을 씁니다.\n" +
                "이 파일 값이 다른 PC끼리는 함께 플레이할 수 없습니다. (게임 버전 끝 -t 지문이 달라짐)",
                new ICsvTableRule[]
                {
                    new KeyColumnRule(),
                    new ExtraCellsRule(),
                    new NumberColumnRule("value"),
                    new KnownKeyRule(GetTuningKeys, "GameTuning", "파일에 없어 코드 기본값을 씁니다."),
                },
                allowColumnEdit: false,
                new Dictionary<string, float> { ["id"] = 240f, ["value"] = 90f, ["설명"] = 560f });
        }

        private static CsvTableProfile CreateSettingsProfile(string path)
        {
            return new CsvTableProfile(path, "룸 설정 범위 (game_settings)",
                "룸에서 호스트가 고르는 항목의 조정 단위(step)와 범위(min~max). 표시 순서 = 줄 순서.\n" +
                "이름 문구는 strings.csv의 setting.<id>, unit은 strings.csv의 키입니다. Map 줄의 max는 '맵 목록 갱신'이 맞춥니다.",
                new ICsvTableRule[]
                {
                    new KeyColumnRule(),
                    new ExtraCellsRule(),
                    new NumberColumnRule("step", mustBePositive: true),
                    new NumberColumnRule("min"),
                    new NumberColumnRule("max"),
                    new MinMaxRule("min", "max"),
                    new KnownKeyRule(GetSettingKeys, "GameSettingId", "파일에 없어 코드의 기본 범위를 씁니다."),
                    new KeyReferenceRule("unit", "{0}", () => CsvKeyIndex.Load(StringsPath), StringsFileName),
                    new KeyReferenceRule(null, "setting.{0}", () => CsvKeyIndex.Load(StringsPath), StringsFileName),
                },
                allowColumnEdit: false,
                new Dictionary<string, float> { ["id"] = 220f, ["step"] = 80f, ["min"] = 80f, ["max"] = 80f, ["unit"] = 160f });
        }

        private static CsvTableProfile CreateStringsProfile(string path)
        {
            return new CsvTableProfile(path, "화면 문장 (strings)",
                "열 = 언어 코드(ko, en, ...). '열 추가'로 새 언어를 넣으면 설정 화면 언어 목록에 자동으로 나타납니다.\n" +
                "{0} {1}은 코드가 채우는 값이라 언어마다 같아야 합니다. 번역이 비면 첫 언어가 표시됩니다.",
                new ICsvTableRule[]
                {
                    new KeyColumnRule(),
                    new ExtraCellsRule(),
                    new TranslationRule(),
                },
                allowColumnEdit: true,
                new Dictionary<string, float> { ["key"] = 260f },
                otherColumnWidth: 380f);
        }

        private static CsvTableProfile CreateGenericProfile(string path)
        {
            return new CsvTableProfile(path, Path.GetFileName(path), "전용 검사 규칙이 없는 CSV입니다. 키 중복과 칸 개수만 검사합니다.",
                new ICsvTableRule[] { new KeyColumnRule(), new ExtraCellsRule() },
                allowColumnEdit: true);
        }

        /// <summary>
        /// GameTuning의 공개 정적 속성이 곧 파일 id 목록이라 리플렉션으로 모으고, 파일에 없는 항목은 지금 값(= 코드 기본값)을 추가용 기본값으로 쓴다.
        /// </summary>
        private static IReadOnlyList<CsvKnownKey> GetTuningKeys()
        {
            var keys = new List<CsvKnownKey>();
            foreach (PropertyInfo property in typeof(GameTuning).GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                if (property.Name == nameof(GameTuning.Fingerprint))
                    continue;
                keys.Add(new CsvKnownKey(property.Name, new[] { property.Name, ReadDefault(property), string.Empty }));
            }
            return keys;
        }

        private static string ReadDefault(PropertyInfo property)
        {
            try
            {
                object value = property.GetValue(null);
                switch (value)
                {
                    case bool flag: return flag ? "1" : "0";
                    case float number: return number.ToString("0.####", CultureInfo.InvariantCulture);
                    case IFormattable formattable: return formattable.ToString(null, CultureInfo.InvariantCulture);
                    default: return string.Empty;
                }
            }
            catch (Exception)
            {
                // 값을 못 읽어도 줄 추가는 할 수 있도록 빈 값으로 둔다. (숫자 검사가 다시 알려 준다)
                return string.Empty;
            }
        }

        /// <summary>범위 기본값은 런타임 내부에만 있어 자동 추가는 제공하지 않는다.</summary>
        private static IReadOnlyList<CsvKnownKey> GetSettingKeys()
        {
            return Enum.GetNames(typeof(GameSettingId)).Select(name => new CsvKnownKey(name, null)).ToList();
        }
    }

    /// <summary>다른 파일이 참조하는 키를 검사할 때마다 파일을 다시 읽지 않도록, 수정 시각이 같으면 읽어 둔 키 목록을 쓴다.</summary>
    internal static class CsvKeyIndex
    {
        private static readonly Dictionary<string, (DateTime writeTime, HashSet<string> keys)> cache = new();

        /// <summary>파일이 없으면 null을 돌려 참조 검사를 건너뛰게 한다.</summary>
        public static HashSet<string> Load(string assetPath)
        {
            if (!File.Exists(assetPath))
                return null;

            DateTime writeTime = File.GetLastWriteTimeUtc(assetPath);
            if (cache.TryGetValue(assetPath, out var cached) && cached.writeTime == writeTime)
                return cached.keys;

            var keys = new HashSet<string>(CsvTableDocument.Load(assetPath).Rows.Select(row => row.Get(0)));
            cache[assetPath] = (writeTime, keys);
            return keys;
        }
    }
}
