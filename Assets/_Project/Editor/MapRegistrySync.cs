using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Mods;
using GuardVsMannequin.Room;
using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>맵을 추가·삭제해도 게스트와 맵 번호가 어긋나지 않도록 빌드 설정·maps.json·strings.csv를 맵 목록에 맞춘다.</summary>
    public static class MapRegistrySync
    {
        private const string StringsPath = "Assets/_Project/Resources/Localization/strings.csv";
        private const string MapListPath = "Assets/_Project/Resources/Data/maps.json";

        /// <summary>고칠 곳이 여러 군데라 하나라도 빠뜨리면 맵 번호가 어긋나므로 한 번에 모두 맞춘다.</summary>
        public static void Sync()
        {
            MapCatalog.Entry[] maps = MapCatalog.AllMaps;
            TestEnvironmentSetupTool.RegisterBuildScenes();
            BuiltInMapList mapList = WriteBuiltInMapList(maps);
            int addedCount = AddMissingMapNames(mapList, maps);

            AssetDatabase.SaveAssets();
            Loc.Reload();
            GameSettingDefinitions.ClearCache();
            Debug.Log($"[MapRegistrySync] 맵 {maps.Length}개 등록 ({string.Join(", ", maps.Select((map, index) => $"{index}:{map.sceneName}"))}). " +
                      $"맵 이름 문구 {addedCount}개 추가.");
        }

        /// <summary>사람이 적어 둔 맵별 설정과 번역이 끊기지 않도록 같은 ID의 항목과 이름 키는 그대로 유지한다.</summary>
        private static BuiltInMapList WriteBuiltInMapList(MapCatalog.Entry[] maps)
        {
            var previousManifests = new Dictionary<string, MapManifest>();
            if (File.Exists(MapListPath))
            {
                BuiltInMapList existingList = JsonUtility.FromJson<BuiltInMapList>(File.ReadAllText(MapListPath, Encoding.UTF8));
                foreach (MapManifest manifest in existingList?.maps ?? new List<MapManifest>())
                {
                    if (!string.IsNullOrEmpty(manifest?.id))
                        previousManifests[manifest.id] = manifest;
                }
            }

            var mapList = new BuiltInMapList();
            var manifests = new MapManifest[maps.Length];
            for (int i = 0; i < maps.Length; i++)
            {
                string mapId = "builtin." + maps[i].sceneName.ToLowerInvariant();
                manifests[i] = previousManifests.TryGetValue(mapId, out MapManifest keptManifest) ? keptManifest : new MapManifest { id = mapId, name = maps[i].displayName };
                manifests[i].scene = maps[i].sceneName;
            }

            // 남아 있는 맵의 키는 그대로 두고, 키가 없는 새 맵에만 아직 쓰지 않은 번호를 준다.
            var usedKeys = new HashSet<string>(manifests.Where(manifest => !string.IsNullOrEmpty(manifest.nameKey)).Select(manifest => manifest.nameKey));
            int nextKeyNumber = 0;
            foreach (MapManifest manifest in manifests)
            {
                if (!string.IsNullOrEmpty(manifest.nameKey))
                    continue;
                while (usedKeys.Contains($"map.{nextKeyNumber}"))
                    nextKeyNumber++;
                manifest.nameKey = $"map.{nextKeyNumber}";
                usedKeys.Add(manifest.nameKey);
            }
            mapList.maps.AddRange(manifests);

            File.WriteAllText(MapListPath, JsonUtility.ToJson(mapList, true), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(MapListPath);
            return mapList;
        }

        /// <summary>이름 문구가 없으면 게임에 키가 그대로 보이므로 우선 씬 이름으로 채워 둔다.</summary>
        private static int AddMissingMapNames(BuiltInMapList mapList, MapCatalog.Entry[] maps)
        {
            List<string> lines = ReadLines(StringsPath, out bool hasBom, out string newLine);
            string headerLine = lines.FirstOrDefault(line => line.StartsWith("key,")) ?? "key,ko,en";
            int languageCount = Mathf.Max(1, headerLine.Split(',').Length - 1);

            int lastMapLineIndex = lines.FindLastIndex(line => line.StartsWith("map."));
            int insertIndex = lastMapLineIndex >= 0 ? lastMapLineIndex + 1 : lines.Count;
            int addedCount = 0;
            for (int i = 0; i < maps.Length; i++)
            {
                string nameKey = mapList.maps[i].nameKey;
                if (lines.Any(line => line.StartsWith(nameKey + ",")))
                    continue;

                string mapName = maps[i].displayName.Replace(",", " ");
                lines.Insert(insertIndex++, nameKey + string.Concat(Enumerable.Repeat("," + mapName, languageCount)));
                addedCount++;
            }

            if (addedCount > 0)
                WriteLines(StringsPath, lines, hasBom, newLine);
            return addedCount;
        }

        /// <summary>
        /// 엑셀로 편집하는 파일이라 BOM·줄바꿈 형식이 바뀌면 diff가 파일 전체로 잡히므로, 원래 형식을 기억해 같은 형식으로 다시 쓴다.
        /// </summary>
        private static List<string> ReadLines(string path, out bool hasBom, out string newLine)
        {
            byte[] bytes = File.ReadAllBytes(path);
            hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            string fileText = new UTF8Encoding(false).GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0));
            newLine = fileText.Contains("\r\n") ? "\r\n" : "\n";

            List<string> lines = fileText.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
            if (lines.Count > 0 && lines[lines.Count - 1].Length == 0)
                lines.RemoveAt(lines.Count - 1);
            return lines;
        }

        /// <summary>바뀐 줄만 diff에 잡히도록 읽을 때의 BOM·줄바꿈 형식 그대로 저장한다.</summary>
        private static void WriteLines(string path, List<string> lines, bool hasBom, string newLine)
        {
            File.WriteAllText(path, string.Join(newLine, lines) + newLine, new UTF8Encoding(hasBom));
            AssetDatabase.ImportAsset(path);
        }
    }
}
