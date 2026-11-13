using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>모델이 바뀌면 프리팹도 따라 바뀌도록 맵 물체 프리팹을 프리팹 변형으로 만든다.</summary>
    public static class MapObjectPrefabBuilder
    {
        /// <summary>모델을 고친 뒤 프리팹 충돌체까지 맞추려면 전부 다시 만들어야 하므로 메뉴에서는 갱신까지 한다.</summary>
        [MenuItem(EditorMenus.Root + "맵 물체/물체 프리팹 만들기·갱신 (Prefabs/Obj)")]
        private static void RebuildFromMenu()
        {
            int writtenCount = EnsurePrefabs(rebuild: true);
            EditorUtility.DisplayDialog("물체 프리팹", $"맵 물체 프리팹 {writtenCount}개를 만들거나 갱신했습니다.\n{MapObjectCatalog.ObjFolder}", "확인");
        }

        /// <summary>맵 빌더가 시작할 때마다 부르므로 기본은 빠진 것만 만들어 빠르게 끝낸다.</summary>
        public static int EnsurePrefabs(bool rebuild = false)
        {
            int writtenCount = 0;
            foreach ((string modelPath, string prefabPath) in MapObjectCatalog.ModelPrefabs())
            {
                if (ShouldWrite(prefabPath, rebuild) && CreateFromSource(modelPath, prefabPath))
                    writtenCount++;
            }
            foreach ((string sourcePath, string prefabPath) in MapObjectCatalog.PackPrefabs)
            {
                if (ShouldWrite(prefabPath, rebuild) && CreateFromSource(sourcePath, prefabPath))
                    writtenCount++;
            }
            foreach ((string compositePath, (string part, Vector3 position, float yaw, Vector3 scale)[] parts) in MapObjectCatalog.Composites)
            {
                if (ShouldWrite(compositePath, rebuild) && CreateComposite(compositePath, parts))
                    writtenCount++;
            }

            AssetDatabase.SaveAssets();
            if (writtenCount > 0)
                Debug.Log($"[MapObjectPrefabBuilder] 맵 물체 프리팹 {writtenCount}개를 만들거나 갱신했습니다. ({MapObjectCatalog.ObjFolder})");
            return writtenCount;
        }

        /// <summary>
        /// 손으로 만든 프리팹을 덮어쓰면 작업이 사라지므로, 다시 만들 때는 이 도구의 라벨이 붙은 것만 대상으로 한다.
        /// </summary>
        private static bool ShouldWrite(string prefabPath, bool rebuild)
        {
            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existingPrefab == null)
                return true;
            return rebuild && AssetDatabase.GetLabels(existingPrefab).Contains(MapObjectCatalog.Label);
        }

        /// <summary>원본이 바뀌면 따라 바뀌도록 프리팹 변형으로 만들고, 이미 있는 충돌체는 그대로 쓴다.</summary>
        private static bool CreateFromSource(string sourcePath, string prefabPath)
        {
            var sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (sourceAsset == null)
            {
                Debug.LogWarning($"[MapObjectPrefabBuilder] 원본이 없어 건너뜁니다: {sourcePath}");
                return false;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(sourceAsset);
            try
            {
                instance.name = Path.GetFileNameWithoutExtension(prefabPath);
                if (instance.GetComponentInChildren<Collider>(true) == null)
                    AddMeshColliders(instance);
                return Save(instance, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>기본 프리팹을 고치면 묶음에도 반영되도록 중첩 프리팹으로 겹친다.</summary>
        private static bool CreateComposite(string prefabPath, (string part, Vector3 position, float yaw, Vector3 scale)[] parts)
        {
            var compositeRoot = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                foreach ((string partPath, Vector3 position, float yaw, Vector3 scale) in parts)
                {
                    var partPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(partPath);
                    if (partPrefab == null)
                    {
                        Debug.LogWarning($"[MapObjectPrefabBuilder] 묶음 '{compositeRoot.name}'의 기본 프리팹이 없어 건너뜁니다: {partPath}");
                        return false;
                    }
                    var partInstance = (GameObject)PrefabUtility.InstantiatePrefab(partPrefab, compositeRoot.transform);
                    partInstance.transform.localPosition = position;
                    partInstance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    partInstance.transform.localScale = Vector3.Scale(partInstance.transform.localScale, scale);
                }
                return Save(compositeRoot, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(compositeRoot);
            }
        }

        /// <summary>충돌체가 없으면 조준·이동 판정이 통과해 버리므로 메시 모양 그대로 붙인다.</summary>
        private static void AddMeshColliders(GameObject root)
        {
            foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh == null || meshFilter.GetComponent<Collider>() != null)
                    continue;
                meshFilter.gameObject.AddComponent<MeshCollider>().sharedMesh = meshFilter.sharedMesh;
            }
        }

        /// <summary>GUID가 유지되도록 같은 경로에 저장하고, 다시 만들어도 되는 프리팹임을 라벨로 표시한다.</summary>
        private static bool Save(GameObject root, string prefabPath)
        {
            EnsureFolder(Path.GetDirectoryName(prefabPath)?.Replace('\\', '/'));
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool isSaved);
            if (!isSaved || savedPrefab == null)
            {
                Debug.LogError($"[MapObjectPrefabBuilder] 프리팹 저장 실패: {prefabPath}");
                return false;
            }
            var labels = new HashSet<string>(AssetDatabase.GetLabels(savedPrefab)) { MapObjectCatalog.Label };
            AssetDatabase.SetLabels(savedPrefab, labels.ToArray());
            return true;
        }

        /// <summary>
        /// AssetDatabase.CreateFolder는 한 단계씩만 만들 수 있어 상위 폴더부터 재귀로 만든다.
        /// </summary>
        private static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
                return;
            string parentPath = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            EnsureFolder(parentPath);
            AssetDatabase.CreateFolder(parentPath, Path.GetFileName(folderPath));
        }
    }
}
