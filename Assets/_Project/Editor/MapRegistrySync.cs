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
    /// <summary>
    /// 맵 목록(<see cref="MapCatalog.AllMaps"/>)에 맞춰 게임이 맵을 고르는 데 필요한 곳을 모두 갱신한다.
    /// 1. 빌드 설정: 로비 → 룸 → 게임 시스템 → 존재하는 맵 씬
    /// 2. 내장 맵 목록 파일 Resources/Data/maps.json (맵 번호 = 순서, 이미 적힌 맵별 설정·인원·이름 문구 키는 유지)
    /// 3. strings.csv: 없는 이름 문구 줄을 씬 이름으로 추가 (이미 있는 이름은 유지, 나중에 직접 번역)
    /// 이름 문구 키(map.&lt;N&gt;)는 맵이 처음 등록될 때 비어 있는 번호로 정해지고 이후 바뀌지 않는다.
    /// Custom 폴더 맵은 파일 이름 순이라 맵을 추가하면 맵 번호가 밀리지만, 이름은 맵을 따라간다.
    /// 맵 설정의 범위(0 ~ 맵 수 - 1)는 실행 중에 맵 목록(ContentRegistry)에서 정한다.
    /// </summary>
    public static class MapRegistrySync
    {
        private const string StringsPath = "Assets/_Project/Resources/Localization/strings.csv";
        private const string MapListPath = "Assets/_Project/Resources/Data/maps.json";

        /// <summary>
        /// 맵 목록을 게임 설정 전체에 반영한다.
        /// </summary>
        public static void Sync()
        {
            MapCatalog.Entry[] maps = MapCatalog.AllMaps;
            TestEnvironmentSetupTool.RegisterBuildScenes();
            BuiltInMapList list = WriteBuiltInMapList(maps);
            int added = AddMissingMapNames(list, maps);

            AssetDatabase.SaveAssets();
            Loc.Reload();
            GameSettingDefinitions.ClearCache();
            Debug.Log($"[MapRegistrySync] 맵 {maps.Length}개 등록 ({string.Join(", ", maps.Select((m, i) => $"{i}:{m.sceneName}"))}). " +
                      $"맵 이름 문구 {added}개 추가.");
        }

        /// <summary>
        /// 내장 맵 목록 파일(maps.json)을 맵 번호 순서로 쓰고, 쓴 목록을 반환한다.
        /// 이미 적힌 맵(같은 ID)의 맵별 설정·인원·이름·이름 문구 키는 유지하고, 새 맵에는 비어 있는 map.&lt;N&gt; 키를 준다.
        /// </summary>
        private static BuiltInMapList WriteBuiltInMapList(MapCatalog.Entry[] maps)
        {
            var previous = new Dictionary<string, MapManifest>();
            if (File.Exists(MapListPath))
            {
                BuiltInMapList existing = JsonUtility.FromJson<BuiltInMapList>(File.ReadAllText(MapListPath, Encoding.UTF8));
                foreach (MapManifest manifest in existing?.maps ?? new List<MapManifest>())
                {
                    if (!string.IsNullOrEmpty(manifest?.id))
                        previous[manifest.id] = manifest;
                }
            }

            var list = new BuiltInMapList();
            var manifests = new MapManifest[maps.Length];
            for (int i = 0; i < maps.Length; i++)
            {
                string id = "builtin." + maps[i].sceneName.ToLowerInvariant();
                manifests[i] = previous.TryGetValue(id, out MapManifest kept) ? kept : new MapManifest { id = id, name = maps[i].displayName };
                manifests[i].scene = maps[i].sceneName;
            }

            // 남아 있는 맵의 키는 그대로 두고, 키가 없는 새 맵에만 아직 쓰지 않은 번호를 준다.
            var usedKeys = new HashSet<string>(manifests.Where(m => !string.IsNullOrEmpty(m.nameKey)).Select(m => m.nameKey));
            int nextKey = 0;
            foreach (MapManifest manifest in manifests)
            {
                if (!string.IsNullOrEmpty(manifest.nameKey))
                    continue;
                while (usedKeys.Contains($"map.{nextKey}"))
                    nextKey++;
                manifest.nameKey = $"map.{nextKey}";
                usedKeys.Add(manifest.nameKey);
            }
            list.maps.AddRange(manifests);

            File.WriteAllText(MapListPath, JsonUtility.ToJson(list, true), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(MapListPath);
            return list;
        }

        /// <summary>
        /// strings.csv에 없는 맵 이름 문구(맵 목록의 nameKey) 줄을 마지막 map. 줄 뒤에 추가한다. 추가한 개수를 반환한다.
        /// 새 이름은 모든 언어 칸에 씬 이름을 넣으므로, 나중에 파일에서 번역하면 된다.
        /// </summary>
        private static int AddMissingMapNames(BuiltInMapList list, MapCatalog.Entry[] maps)
        {
            List<string> lines = ReadLines(StringsPath, out bool hasBom, out string newLine);
            string header = lines.FirstOrDefault(line => line.StartsWith("key,")) ?? "key,ko,en";
            int languageCount = Mathf.Max(1, header.Split(',').Length - 1);

            int lastMapLine = lines.FindLastIndex(line => line.StartsWith("map."));
            int insertAt = lastMapLine >= 0 ? lastMapLine + 1 : lines.Count;
            int added = 0;
            for (int i = 0; i < maps.Length; i++)
            {
                string key = list.maps[i].nameKey;
                if (lines.Any(line => line.StartsWith(key + ",")))
                    continue;

                string name = maps[i].displayName.Replace(",", " ");
                lines.Insert(insertAt++, key + string.Concat(Enumerable.Repeat("," + name, languageCount)));
                added++;
            }

            if (added > 0)
                WriteLines(StringsPath, lines, hasBom, newLine);
            return added;
        }

        /// <summary>
        /// 텍스트 파일을 줄 단위로 읽는다. BOM과 줄바꿈 형식을 기억해 같은 형식으로 다시 쓰기 위함이다.
        /// </summary>
        private static List<string> ReadLines(string path, out bool hasBom, out string newLine)
        {
            byte[] bytes = File.ReadAllBytes(path);
            hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            string text = new UTF8Encoding(false).GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0));
            newLine = text.Contains("\r\n") ? "\r\n" : "\n";

            List<string> lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
            if (lines.Count > 0 && lines[lines.Count - 1].Length == 0)
                lines.RemoveAt(lines.Count - 1);
            return lines;
        }

        /// <summary>
        /// 줄 목록을 원래 BOM·줄바꿈 형식으로 저장하고 다시 가져온다.
        /// </summary>
        private static void WriteLines(string path, List<string> lines, bool hasBom, string newLine)
        {
            File.WriteAllText(path, string.Join(newLine, lines) + newLine, new UTF8Encoding(hasBom));
            AssetDatabase.ImportAsset(path);
        }
    }
}
