using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GuardVsMannequin.Editor
{
    /// <summary>프리팹을 직접 고쳐도 씬 생성 도구를 다시 실행했을 때 유지되도록 UI를 프리팹으로 관리한다.</summary>
    public static class UiPrefabLibrary
    {
        public const string Folder = "Assets/_Project/Prefabs/UI";
        public const string SettingsPanelPath = Folder + "/SettingsPanel.prefab";
        public const string CreditsPanelPath = Folder + "/CreditsPanel.prefab";
        public const string LobbyCanvasPath = Folder + "/LobbyCanvas.prefab";
        public const string RoomCanvasPath = Folder + "/RoomCanvas.prefab";
        public const string HudCanvasPath = Folder + "/HudCanvas.prefab";

        /// <summary>캔버스가 패널을 중첩하므로 복원할 때 패널이 먼저 있도록 패널을 앞에 둔다.</summary>
        public static readonly string[] AllPaths = { SettingsPanelPath, CreditsPanelPath, LobbyCanvasPath, RoomCanvasPath, HudCanvasPath };

        /// <summary>사용자가 고친 프리팹을 지키려고 있으면 그대로 쓰고, 없을 때만 기준본 → 코드 기본 모양 순으로 만든다.</summary>
        public static GameObject GetOrBuildRoot(string path, Func<GameObject> build)
        {
            GameObject existingPrefab = LoadOrRestore(path);
            if (existingPrefab != null)
                return existingPrefab;

            GameObject root = build();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, EnsureFolder(path));
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// 패널은 캔버스 없이 만들면 RectTransform 크기가 계산되지 않으므로 임시 캔버스 안에서 만들어 저장한다.
        /// </summary>
        public static GameObject GetOrBuildPanel(string path, Func<Transform, GameObject> build)
        {
            GameObject existingPrefab = LoadOrRestore(path);
            if (existingPrefab != null)
                return existingPrefab;

            Canvas tempCanvas = EditorUiFactory.CreateCanvas("TempCanvas");
            GameObject panel = build(tempCanvas.transform);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(panel, EnsureFolder(path));
            Object.DestroyImmediate(tempCanvas.gameObject);
            return prefab;
        }

        /// <summary>
        /// Object.Instantiate는 프리팹 연결이 끊겨 프리팹을 고쳐도 씬에 반영되지 않으므로 연결을 유지하는 방식으로 만든다.
        /// </summary>
        public static GameObject Instantiate(GameObject prefab, Transform parent = null)
        {
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        }

        /// <summary>프리팹 저장은 폴더가 없으면 실패하므로 미리 만든다.</summary>
        /// <returns>받은 경로 그대로.</returns>
        public static string EnsureFolder(string path)
        {
            string folderPath = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folderPath) && !AssetDatabase.IsValidFolder(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }
            return path;
        }

        /// <summary>
        /// 캔버스 하나만 복원하면 중첩된 패널 참조가 끊기므로, 하나라도 없으면 없는 프리팹을 패널부터 모두 기준본으로 복원한다.
        /// </summary>
        private static GameObject LoadOrRestore(string path)
        {
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existingPrefab != null || !UiPrefabDefaults.Exists(path))
                return existingPrefab;

            // 캔버스가 중첩한 패널이 먼저 있어야 참조가 이어지므로, 없는 프리팹을 패널부터 모두 복원한다.
            foreach (string otherPath in AllPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(otherPath) == null)
                    UiPrefabDefaults.Restore(otherPath);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
    }
}
