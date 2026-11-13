using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GuardVsMannequin.Editor
{
    /// <summary>제작자가 키를 직접 적지 않도록 맵 이름 번역으로 strings.csv 내용을 만든다.</summary>
    public static class ModStringsFile
    {
        // 게임이 번역이 빈 언어에 대신 보여 주는 첫 번째 언어.
        private const string FallbackLanguage = "ko";

        /// <summary>게임이 첫 언어 열을 대체 언어로 쓰므로 그 언어를 맨 앞에 두고, 번역이 없으면 null을 돌려준다.</summary>
        /// <param name="modId">모드 ID.</param>
        /// <param name="maps">맵 설정 목록.</param>
        public static string Build(string modId, IEnumerable<ModProject.MapSetting> maps)
        {
            List<ModProject.MapSetting> translatedMaps = maps.Where(map => FilledNames(map).Any()).ToList();
            if (translatedMaps.Count == 0)
                return null;

            List<string> languages = translatedMaps.SelectMany(FilledNames).Select(name => name.language.Trim())
                .Where(language => language != FallbackLanguage).Distinct(StringComparer.Ordinal).ToList();
            languages.Insert(0, FallbackLanguage);

            var csv = new StringBuilder();
            csv.Append("key,").AppendLine(string.Join(",", languages));
            foreach (ModProject.MapSetting map in translatedMaps)
            {
                IEnumerable<string> cells = languages.Select(language => Escape(FindText(map, language)));
                csv.Append(Escape($"mod.{modId}.{map.mapKey}")).Append(',').AppendLine(string.Join(",", cells));
            }
            return csv.ToString();
        }

        /// <summary>
        /// 기본으로 넣어 둔 빈 언어 줄이 빈 열로 나가지 않도록 언어와 이름을 모두 적은 번역만 고른다.
        /// </summary>
        private static IEnumerable<ModProject.LocalizedName> FilledNames(ModProject.MapSetting map)
        {
            return (map.names ?? Enumerable.Empty<ModProject.LocalizedName>())
                .Where(name => name != null && !string.IsNullOrWhiteSpace(name.language) && !string.IsNullOrWhiteSpace(name.text));
        }

        /// <summary>
        /// 대체 언어 칸이 비면 게임이 키를 그대로 보여 주므로, 그때는 표시 이름으로 채운다.
        /// </summary>
        private static string FindText(ModProject.MapSetting map, string language)
        {
            ModProject.LocalizedName foundName = FilledNames(map).FirstOrDefault(name => name.language.Trim() == language);
            if (foundName != null)
                return foundName.text.Trim();
            return language == FallbackLanguage ? map.displayName ?? string.Empty : string.Empty;
        }

        /// <summary>게임의 CsvParser 규칙과 맞도록 줄바꿈은 \n으로, 쉼표·큰따옴표가 있으면 큰따옴표로 감싼다.</summary>
        private static string Escape(string value)
        {
            string escapedText = value.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\\n");
            return escapedText.IndexOf(',') >= 0 || escapedText.IndexOf('"') >= 0 ? $"\"{escapedText.Replace("\"", "\"\"")}\"" : escapedText;
        }
    }
}
