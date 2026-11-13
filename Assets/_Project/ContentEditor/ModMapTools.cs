using System.Linq;
using GuardVsMannequin.Core;
using GuardVsMannequin.Map;
using GuardVsMannequin.Objectives;
using GuardVsMannequin.Props;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>게임 코드 없이 모드 SDK에서 맵을 만들 수 있도록 맵 도구 메뉴를 모은다.</summary>
    public static class ModMapTools
    {
        private const string Menu = EditorMenus.Mod + "맵 도구/";

        /// <summary>제작자가 게임이 기대하는 맵 구조를 외우지 않도록 MapInfo·NavMeshSurface·지점 그룹을 한 번에 만든다.</summary>
        [MenuItem(Menu + "맵 뼈대 만들기 (열린 씬)")]
        private static void CreateSkeleton()
        {
            if (Object.FindAnyObjectByType<MapInfo>() != null)
            {
                EditorUtility.DisplayDialog("맵 뼈대", "이미 MapInfo가 있습니다.", "확인");
                return;
            }

            var map = new GameObject("Map");
            Undo.RegisterCreatedObjectUndo(map, "맵 뼈대");
            map.AddComponent<MapInfo>();
            NavMeshSurface surface = map.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            new GameObject("Environment").transform.SetParent(map.transform, false);
            foreach (string groupName in new[] { "MannequinSpawnPoints", "GuardSpawnPoints", "GeneratorSpawnPoints", "Lights" })
                new GameObject(groupName).transform.SetParent(map.transform, false);

            var overview = new GameObject("CameraOverview");
            overview.transform.SetParent(map.transform, false);
            overview.transform.SetPositionAndRotation(new Vector3(0f, 30f, -28f), Quaternion.Euler(50f, 0f, 0f));
            var mapInfo = new SerializedObject(map.GetComponent<MapInfo>());
            mapInfo.FindProperty("overviewPoint").objectReferenceValue = overview.transform;
            mapInfo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(map.scene);
            Selection.activeGameObject = map;
            Debug.Log("[ModMapTools] 맵 뼈대를 만들었습니다. 맵 모델은 Map/Environment 아래에 넣으세요. (사람 키 1.8m 기준)");
        }

        /// <summary>여러 조각으로 된 물체는 부모 하나로 등록해야 통째로 복사되므로 부모를 선택하게 안내한다.</summary>
        [MenuItem(Menu + "선택 → 변신 사물로 등록")]
        private static void RegisterProps() => RegisterSelected();

        /// <summary>충돌체가 없으면 조준·충돌이 안 되므로 충돌체 없는 조각에만 MeshCollider를 붙인다.</summary>
        [MenuItem(Menu + "선택 → 충돌체 추가")]
        private static void AddColliders()
        {
            int addedCount = 0;
            foreach (MeshFilter meshFilter in Selection.gameObjects.SelectMany(selected => selected.GetComponentsInChildren<MeshFilter>(true)).Distinct())
            {
                if (meshFilter.sharedMesh == null || meshFilter.GetComponent<Collider>() != null)
                    continue;
                Undo.AddComponent<MeshCollider>(meshFilter.gameObject).sharedMesh = meshFilter.sharedMesh;
                addedCount++;
            }
            Debug.Log($"[ModMapTools] 충돌체 {addedCount}개 추가");
        }

        /// <summary>
        /// 사냥 시간에 맵을 어둡게 하려면 조명을 실행 중에 꺼야 하므로, 구운 조명이 아닌 실시간 조명으로 바꾸고 MapLight를 붙인다.
        /// </summary>
        [MenuItem(Menu + "모든 조명 등록 (사냥 시간에 꺼짐)")]
        private static void RegisterLights()
        {
            int registeredCount = 0;
            foreach (Light sceneLight in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                Undo.RecordObject(sceneLight, "조명 등록");
                sceneLight.lightmapBakeType = LightmapBakeType.Realtime;
                if (sceneLight.GetComponent<MapLight>() != null)
                    continue;
                Undo.AddComponent<MapLight>(sceneLight.gameObject);
                registeredCount++;
            }
            Debug.Log($"[ModMapTools] 조명 {registeredCount}개 등록");
        }

        /// <summary>제작자가 지금 보고 있는 곳에 바로 놓이도록 씬 뷰 가운데 아래 바닥에 만든다.</summary>
        [MenuItem(Menu + "지점 추가/마네킹 스폰")]
        private static void AddMannequinSpawn() => AddPoint("MannequinSpawnPoints", "SpawnPoint_Mannequin", point => SetRole(point.AddComponent<SpawnPoint>(), PlayerRole.Mannequin));

        /// <summary>제작자가 지금 보고 있는 곳에 바로 놓이도록 씬 뷰 가운데 아래 바닥에 만든다.</summary>
        [MenuItem(Menu + "지점 추가/가드 스폰 후보")]
        private static void AddGuardSpawn() => AddPoint("GuardSpawnPoints", "SpawnPoint_Guard", point => SetRole(point.AddComponent<SpawnPoint>(), PlayerRole.Guard));

        /// <summary>제작자가 지금 보고 있는 곳에 바로 놓이도록 씬 뷰 가운데 아래 바닥에 만든다.</summary>
        [MenuItem(Menu + "지점 추가/전원 장치 후보")]
        private static void AddGenerator() => AddPoint("GeneratorSpawnPoints", "GeneratorSpawnPoint", point => point.AddComponent<GeneratorSpawnPoint>());

        /// <summary>내보내기 전에도 문제를 미리 보도록 번호를 매긴 뒤 검사하고 Console에 오브젝트를 함께 남긴다.</summary>
        [MenuItem(Menu + "맵 검사 (열린 씬)")]
        private static void ValidateActive()
        {
            Scene scene = SceneManager.GetActiveScene();
            PropIndexer.AssignIndices(scene);
            var issues = ModMapValidator.Validate(scene);
            foreach (ModMapValidator.Issue issue in issues)
            {
                if (issue.isError)
                    Debug.LogError($"[맵 검사] {issue.message}", issue.context);
                else
                    Debug.LogWarning($"[맵 검사] {issue.message}", issue.context);
            }
            int errorCount = issues.Count(issue => issue.isError);
            Debug.Log($"[맵 검사] {scene.name}: 오류 {errorCount}개, 경고 {issues.Count - errorCount}개");
        }

        /// <summary>
        /// 부모와 자식을 함께 선택하면 같은 물체가 두 번 등록되므로 선택 안에서 가장 위의 것만 등록한다.
        /// </summary>
        private static void RegisterSelected()
        {
            GameObject[] roots = Selection.gameObjects
                .Where(selected => !Selection.gameObjects.Any(other => other != selected && selected.transform.IsChildOf(other.transform)))
                .ToArray();
            int registeredCount = roots.Count(target => PropRegistrar.Register(target, absorbChildren: true));
            if (roots.Length > 0)
                EditorSceneManager.MarkSceneDirty(roots[0].scene);
            Debug.Log($"[ModMapTools] 변신 사물 {registeredCount}개 등록 (번호는 씬 저장 때 매겨집니다)");
        }

        /// <summary>지점이 공중에 뜨지 않도록 씬 뷰 가운데 아래 바닥을 찾아 놓는다.</summary>
        private static void AddPoint(string groupName, string pointName, System.Action<GameObject> setup)
        {
            MapInfo mapInfo = Object.FindAnyObjectByType<MapInfo>();
            if (mapInfo == null)
            {
                EditorUtility.DisplayDialog("지점 추가", "먼저 '맵 뼈대 만들기'를 실행하세요.", "확인");
                return;
            }

            Transform group = mapInfo.transform.Find(groupName);
            if (group == null)
            {
                group = new GameObject(groupName).transform;
                group.SetParent(mapInfo.transform, false);
            }

            Vector3 position = Vector3.zero;
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                position = sceneView.pivot;
                if (Physics.Raycast(position + Vector3.up * 50f, Vector3.down, out RaycastHit groundHit, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    position = groundHit.point;
            }

            var point = new GameObject(pointName);
            Undo.RegisterCreatedObjectUndo(point, "지점 추가");
            point.transform.SetParent(group, false);
            point.transform.position = position;
            setup(point);
            Selection.activeGameObject = point;
        }

        /// <summary>역할 필드는 공개 setter가 없어 SerializedObject로 쓴다.</summary>
        private static void SetRole(SpawnPoint point, PlayerRole role)
        {
            var serializedPoint = new SerializedObject(point);
            serializedPoint.FindProperty("role").enumValueIndex = System.Array.IndexOf(System.Enum.GetValues(typeof(PlayerRole)), role);
            serializedPoint.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
