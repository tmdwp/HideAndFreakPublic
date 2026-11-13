using System.Collections.Generic;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Mods
{
    /// <summary>빌드 후에도 고칠 수 있도록 maps.json에서 내장 맵 목록을 읽고, 실패하면 코드 기본 목록을 쓴다.</summary>
    public sealed class BuiltInContentSource : IContentSource
    {
        public const string Id = "builtin";

        private const string FilePath = "Data/maps";

        // 파일을 읽을 수 없을 때 쓰는 기본 목록.
        private static readonly (string id, string scene, string nameKey)[] FallbackMaps =
        {
            ("builtin.map_citystreet", "Map_CityStreet", "map.0"),
            ("builtin.map_forest", "Map_Forest", "map.6"),
            ("builtin.map_giantroom", "Map_GiantRoom", "map.1"),
            ("builtin.map_laboratory", "Map_Laboratory", "map.4"),
            ("builtin.map_transferstation", "Map_TransferStation", "map.5")
        };

        /// <inheritdoc />
        public string SourceId => Id;

        /// <inheritdoc />
        public IReadOnlyList<MapEntry> LoadMaps()
        {
            // 데이터 파일이 깨져도 게임이 맵 없이 열리지 않도록 코드 기본 목록으로 물러난다.
            if (!DataFiles.TryLoad(FilePath, ".json", TryParse, out BuiltInMapList mapList))
            {
                Debug.LogWarning($"[BuiltInContentSource] {FilePath}.json을 사용할 수 없어 기본 맵 목록을 사용합니다.");
                mapList = CreateFallback();
            }

            var entries = new List<MapEntry>(mapList.maps.Count);
            foreach (MapManifest manifest in mapList.maps)
                entries.Add(new MapEntry(manifest, Id, null));
            return entries;
        }

        /// <summary>열 수 없는 맵이 목록에 생기지 않도록 ID·씬 이름이 빠진 맵이 있으면 파일 전체를 깨진 것으로 본다.</summary>
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
        /// 번역 문구가 없을 때도 빈 이름이 되지 않도록 씬 이름을 기본 이름으로 함께 넣는다.
        /// </summary>
        private static BuiltInMapList CreateFallback()
        {
            var mapList = new BuiltInMapList();
            foreach ((string id, string scene, string nameKey) in FallbackMaps)
            {
                mapList.maps.Add(new MapManifest
                {
                    id = id,
                    scene = scene,
                    nameKey = nameKey,
                    name = scene
                });
            }
            return mapList;
        }
    }
}
