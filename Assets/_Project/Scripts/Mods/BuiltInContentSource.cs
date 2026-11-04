using System.Collections.Generic;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Mods
{
    /// <summary>
    /// 게임에 들어 있는 맵 목록. Resources/Data/maps.json(빌드 후에는 GameData/Data/maps.json)을 읽고,
    /// 읽을 수 없으면 코드 기본 목록을 쓴다. 파일은 에디터의 "맵 목록 갱신"(MapRegistrySync)이 만든다.
    /// 내장 맵도 모드 맵과 같은 형식(<see cref="MapManifest"/>)이라 등록소에서 같은 방식으로 다뤄진다.
    /// </summary>
    public sealed class BuiltInContentSource : IContentSource
    {
        /// <summary>내장 콘텐츠 출처 ID.</summary>
        public const string Id = "builtin";

        private const string FilePath = "Data/maps";

        // 파일을 읽을 수 없을 때 쓰는 기본 목록 (맵 번호 = 순서, nameKey = strings.csv 이름 문구)
        private static readonly (string id, string scene, string nameKey)[] FallbackMaps =
        {
            ("builtin.map_forest", "Map_Forest", "map.6"),
            ("builtin.map_laboratory", "Map_Laboratory", "map.4"),
            ("builtin.map_transferstation", "Map_TransferStation", "map.5")
        };

        /// <inheritdoc />
        public string SourceId => Id;

        /// <inheritdoc />
        public IReadOnlyList<MapEntry> LoadMaps()
        {
            if (!DataFiles.TryLoad(FilePath, ".json", TryParse, out BuiltInMapList list))
            {
                Debug.LogWarning($"[BuiltInContentSource] {FilePath}.json을 사용할 수 없어 기본 맵 목록을 사용합니다.");
                list = CreateFallback();
            }

            var entries = new List<MapEntry>(list.maps.Count);
            foreach (MapManifest manifest in list.maps)
                entries.Add(new MapEntry(manifest, Id, null));
            return entries;
        }

        /// <summary>
        /// JSON을 읽는다. 맵이 하나도 없거나 ID·씬 이름이 빠진 맵이 있으면 깨진 파일로 본다.
        /// </summary>
        private static bool TryParse(string text, out BuiltInMapList result)
        {
            result = JsonUtility.FromJson<BuiltInMapList>(text);
            if (result?.maps == null || result.maps.Count == 0)
                return false;

            foreach (MapManifest manifest in result.maps)
            {
                if (manifest == null || string.IsNullOrEmpty(manifest.id) || string.IsNullOrEmpty(manifest.scene))
                    return false;
                manifest.settings ??= new List<MapSettingRange>();
            }
            return true;
        }

        /// <summary>
        /// 코드 기본 목록을 만든다. 이름은 strings.csv의 nameKey 문장을 쓴다.
        /// </summary>
        private static BuiltInMapList CreateFallback()
        {
            var list = new BuiltInMapList();
            foreach ((string id, string scene, string nameKey) in FallbackMaps)
            {
                list.maps.Add(new MapManifest
                {
                    id = id,
                    scene = scene,
                    nameKey = nameKey,
                    name = scene
                });
            }
            return list;
        }
    }
}
