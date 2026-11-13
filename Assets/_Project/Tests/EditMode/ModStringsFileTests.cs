using System.Collections.Generic;
using GuardVsMannequin.Core;
using GuardVsMannequin.Editor;
using NUnit.Framework;

namespace GuardVsMannequin.Tests
{
    /// <summary>모드 설정의 맵 이름 번역으로 만든 strings.csv를 게임 CSV 규칙으로 다시 읽어 확인한다.</summary>
    public class ModStringsFileTests
    {
        /// <summary>각 테스트가 맵 키·표시 이름·번역만 한 줄로 적을 수 있도록 맵 설정 생성을 줄인다.</summary>
        private static ModProject.MapSetting CreateMapSetting(string mapKey, string displayName, params (string language, string text)[] names)
        {
            var map = new ModProject.MapSetting { mapKey = mapKey, displayName = displayName, names = new List<ModProject.LocalizedName>() };
            foreach ((string language, string text) in names)
                map.names.Add(new ModProject.LocalizedName { language = language, text = text });
            return map;
        }

        /// <summary>빈 strings.csv가 게임 번역을 덮지 않도록, 적은 번역이 공백뿐이면 파일을 만들지 않는지 확인한다.</summary>
        [Test]
        public void Build_NoTranslations_ReturnsNull()
        {
            Assert.IsNull(ModStringsFile.Build("my.mod", new[] { CreateMapSetting("a", "A", ("ko", " "), ("en", "")) }));
        }

        /// <summary>게임이 모드 맵 이름을 찾는 키 규칙과 맞아야 하고 쉼표·큰따옴표가 CSV 열을 깨뜨리면 안 되므로 둘을 함께 확인한다.</summary>
        [Test]
        public void Build_WritesKeysAndEscapesText()
        {
            List<string[]> rows = CsvParser.Parse(ModStringsFile.Build("my.mod", new[] { CreateMapSetting("base", "Base", ("ko", "지하, \"방\""), ("en", "Basement")) }));

            CollectionAssert.AreEqual(new[] { "key", "ko", "en" }, rows[0]);
            CollectionAssert.AreEqual(new[] { "mod.my.mod.base", "지하, \"방\"", "Basement" }, rows[1]);
        }

        /// <summary>기본 언어 칸이 비면 어느 언어에서도 이름이 안 보일 수 있으므로 표시 이름으로 채우고, 다른 언어는 비워 게임 대체 규칙에 맡기는지 확인한다.</summary>
        [Test]
        public void Build_FillsFallbackLanguageFromDisplayName()
        {
            List<string[]> rows = CsvParser.Parse(ModStringsFile.Build("my.mod", new[]
            {
                CreateMapSetting("a", "Display A", ("en", "English A")),
                CreateMapSetting("b", "Display B", ("ja", "日本語 B"))
            }));

            CollectionAssert.AreEqual(new[] { "key", "ko", "en", "ja" }, rows[0]);
            CollectionAssert.AreEqual(new[] { "mod.my.mod.a", "Display A", "English A", "" }, rows[1]);
            CollectionAssert.AreEqual(new[] { "mod.my.mod.b", "Display B", "", "日本語 B" }, rows[2]);
        }
    }
}
