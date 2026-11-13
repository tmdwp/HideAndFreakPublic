using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GuardVsMannequin.Props;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>맵마다 고친 내용을 유지한 채 기존 맵 씬의 물체를 맵 물체 프리팹 인스턴스로 바꾼다.</summary>
    public static class MapObjectSceneMigrator
    {
        private const string ReportPath = "Temp/MapObjectPrefabs/migration.txt";

        /// <summary>실외 맵은 맵 최적화 여유 거리가 길어야 하므로 따로 구별한다.</summary>
        private static readonly string[] OutdoorScenes = { "Map_Forest", "Map_CityStreet" };

        /// <summary>모든 맵 씬을 열어 저장하는 큰 작업이라 확인을 받고, 바꾼 씬은 최적화를 다시 한다.</summary>
        [MenuItem(EditorMenus.Root + "맵 물체/맵 씬의 물체를 프리팹으로 교체 (전체 맵)")]
        private static void MigrateFromMenu()
        {
            if (!EditorUtility.DisplayDialog("물체 프리팹으로 교체",
                    "모든 맵 씬과 테스트 씬의 물체를 Prefabs/Obj 프리팹 인스턴스로 바꾸고 맵 최적화를 다시 한 뒤 저장합니다.\n" +
                    "씬을 직접 고친 내용(위치·크기·머티리얼·변신 등록)은 유지합니다. 계속할까요?", "교체", "취소"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            MapObjectPrefabBuilder.EnsurePrefabs();
            string returnScenePath = SceneManager.GetActiveScene().path;
            var report = new StringBuilder();
            int totalReplaced = 0;
            foreach (string scenePath in TargetScenes())
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var replacedCounts = new Dictionary<string, int>();
                int replacedCount = MigrateScene(scene, replacedCounts);
                totalReplaced += replacedCount;
                if (replacedCount > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    MapOptimizer.Optimize(scene, indoor: !OutdoorScenes.Contains(scene.name));
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                report.AppendLine($"[{scene.name}] {replacedCount}개");
                foreach (KeyValuePair<string, int> countEntry in replacedCounts.OrderBy(entry => entry.Key))
                    report.AppendLine($"  {Path.GetFileNameWithoutExtension(countEntry.Key)}: {countEntry.Value}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report.ToString());
            if (!string.IsNullOrEmpty(returnScenePath) && File.Exists(returnScenePath))
                EditorSceneManager.OpenScene(returnScenePath, OpenSceneMode.Single);
            Debug.Log($"[MapObjectSceneMigrator] 물체 {totalReplaced}개를 프리팹으로 교체했습니다.\n{report}");
            EditorUtility.DisplayDialog("물체 프리팹으로 교체", $"물체 {totalReplaced}개를 프리팹으로 바꿨습니다.\n자세한 내용: {ReportPath}", "확인");
        }

        /// <summary>테스트 씬도 같은 모델을 쓰므로 함께 바꾸고, 파일이 없는 맵은 뺀다.</summary>
        private static IEnumerable<string> TargetScenes()
        {
            return MapCatalog.AllMaps.Select(map => map.ScenePath)
                .Append(TestEnvironmentSetupTool.ScenePath)
                .Where(File.Exists)
                .Distinct();
        }

        /// <summary>교체하는 동안 계층이 바뀌어 반복이 깨지지 않도록 대상을 먼저 모은다.</summary>
        private static int MigrateScene(Scene scene, Dictionary<string, int> replacedCounts)
        {
            var targets = new List<(GameObject target, string prefab, Vector3 scale)>();
            foreach (GameObject rootObject in scene.GetRootGameObjects())
                CollectTargets(rootObject.transform, targets);

            int replacedCount = 0;
            foreach ((GameObject target, string prefabPath, Vector3 scale) in targets)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (target == null || prefab == null)
                    continue;
                Replace(target, prefab, scale);
                replacedCounts[prefabPath] = replacedCounts.TryGetValue(prefabPath, out int previousCount) ? previousCount + 1 : 1;
                replacedCount++;
            }
            return replacedCount;
        }

        /// <summary>같은 물체를 두 번 바꾸지 않도록 이미 맵 물체 프리팹인 것은 건너뛴다.</summary>
        private static void CollectTargets(Transform node, List<(GameObject, string, Vector3)> targets)
        {
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(node.gameObject)
                && MapObjectCatalog.IsObjectPrefab(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(node.gameObject)))
                return;

            string modelPath = ModelOf(node.gameObject);
            if (modelPath != null && MapObjectCatalog.TryGetReplacement(modelPath, out string prefabPath, out Vector3 scale))
            {
                targets.Add((node.gameObject, prefabPath, scale));
                return;
            }
            foreach (Transform child in node)
                CollectTargets(child, targets);
        }

        /// <summary>풀어 둔 모델은 프리팹 연결이 없어 메시가 어느 FBX에서 왔는지로 원래 모델을 찾는다.</summary>
        private static string ModelOf(GameObject target)
        {
            if (!target.TryGetComponent(out MeshFilter meshFilter) || meshFilter.sharedMesh == null)
                return null;
            string meshAssetPath = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
            return meshAssetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) ? meshAssetPath : null;
        }

        /// <summary>맵마다 고친 내용을 잃지 않도록 위치·상태·변신 등록·머티리얼 등을 모두 옮긴 뒤 옛 물체를 지운다.</summary>
        private static void Replace(GameObject oldObject, GameObject prefab, Vector3 unifyScale)
        {
            Transform oldTransform = oldObject.transform;
            var createdObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, oldTransform.parent);
            Transform createdTransform = createdObject.transform;
            createdTransform.SetSiblingIndex(oldTransform.GetSiblingIndex());
            createdObject.name = oldObject.name;
            createdTransform.localPosition = oldTransform.localPosition;
            createdTransform.localRotation = oldTransform.localRotation;
            createdTransform.localScale = Vector3.Scale(oldTransform.localScale, unifyScale);
            createdObject.SetActive(oldObject.activeSelf);

            // 같은 경로의 자식(모델 계층이 같을 때)마다 상태·위치·머티리얼·충돌체를 옮긴다.
            foreach (Transform oldPart in oldObject.GetComponentsInChildren<Transform>(true))
            {
                Transform newPart = oldPart == oldTransform ? createdTransform : createdTransform.Find(RelativePath(oldTransform, oldPart));
                if (newPart == null)
                    continue;
                CopyPartState(oldPart, newPart, isRoot: oldPart == oldTransform);
            }

            CopyTransformableProp(oldObject, createdObject);
            MoveForeignChildren(oldTransform, createdTransform);
            Object.DestroyImmediate(oldObject);
        }

        /// <summary>맵마다 바꿔 끼운 머티리얼과 빼 둔 서랍 위치가 사라지지 않게 자식 상태를 옮긴다.</summary>
        private static void CopyPartState(Transform oldPart, Transform newPart, bool isRoot)
        {
            GameObject oldPartObject = oldPart.gameObject, newPartObject = newPart.gameObject;
            newPartObject.layer = oldPartObject.layer;
            GameObjectUtility.SetStaticEditorFlags(newPartObject, GameObjectUtility.GetStaticEditorFlags(oldPartObject));
            if (!isRoot)
            {
                newPart.localPosition = oldPart.localPosition;
                newPart.localRotation = oldPart.localRotation;
                newPart.localScale = oldPart.localScale;
                newPartObject.SetActive(oldPartObject.activeSelf);
            }

            if (oldPartObject.TryGetComponent(out Renderer oldRenderer) && newPartObject.TryGetComponent(out Renderer newRenderer)
                && oldRenderer.sharedMaterials.Length == newRenderer.sharedMaterials.Length)
            {
                newRenderer.sharedMaterials = oldRenderer.sharedMaterials;
                newRenderer.shadowCastingMode = oldRenderer.shadowCastingMode;
            }
            if (oldPartObject.TryGetComponent(out MeshCollider oldCollider) && newPartObject.TryGetComponent(out MeshCollider newCollider))
                newCollider.convex = oldCollider.convex;
            // 옛 물체에서 충돌체를 지웠거나 꺼 두었으면(공중 조명 등) 새 충돌체도 끈다.
            if (newPartObject.TryGetComponent(out Collider addedCollider) && (!oldPartObject.TryGetComponent(out Collider originalCollider) || !originalCollider.enabled))
                addedCollider.enabled = false;
        }

        /// <summary>
        /// 번호가 바뀌면 이미 저장된 변신 동기화가 어긋나므로 직렬화 값을 그대로 복사한다.
        /// </summary>
        private static void CopyTransformableProp(GameObject oldObject, GameObject createdObject)
        {
            if (!oldObject.TryGetComponent(out TransformableProp oldProp))
                return;
            TransformableProp newProp = createdObject.GetComponent<TransformableProp>() ?? createdObject.AddComponent<TransformableProp>();
            EditorUtility.CopySerialized(oldProp, newProp);
        }

        /// <summary>옛 물체를 지울 때 나중에 붙인 자식이 사라지지 않도록 새 인스턴스 아래로 옮긴다.</summary>
        private static void MoveForeignChildren(Transform oldRoot, Transform newRoot)
        {
            foreach (Transform child in oldRoot.Cast<Transform>().ToList())
            {
                if (newRoot.Find(child.name) == null)
                    child.SetParent(newRoot, true);
            }
        }

        /// <summary>
        /// 새 인스턴스에서 같은 자식을 Transform.Find로 찾으려고 뿌리 기준 상대 경로("A/B")를 만든다.
        /// </summary>
        private static string RelativePath(Transform root, Transform part)
        {
            var pathNames = new List<string>();
            for (Transform current = part; current != null && current != root; current = current.parent)
                pathNames.Add(current.name);
            pathNames.Reverse();
            return string.Join("/", pathNames);
        }
    }
}
