using System.Collections.Generic;
using System.Linq;
using GuardVsMannequin.Core;
using GuardVsMannequin.Map;
using GuardVsMannequin.Objectives;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>맵 제작 도우미에서 지점을 쉽게 추가·재배치하도록 열린 맵 씬의 배치 편집 기능을 모은다.</summary>
    public static class MapEditTools
    {
        private const string MapRootName = "Map";
        private const string GeneratorGroup = "GeneratorSpawnPoints";
        private const string MannequinGroup = "MannequinSpawnPoints";

        /// <summary>
        /// 맵 빌더와 같은 모양의 지점이 되도록 빌더의 생성 함수를 그대로 쓴다.
        /// </summary>
        internal static void AddGeneratorPoint()
        {
            AddAtSceneViewCenter(GeneratorGroup, "GeneratorSpawnPoint",
                (parent, position, rotation) => MapSceneBuilder.CreateMarker<GeneratorSpawnPoint>(parent, "GeneratorSpawnPoint", position, rotation));
        }

        /// <summary>
        /// 테스트 환경 도구와 같은 스폰 지점이 되도록 그 생성 함수를 그대로 쓴다.
        /// </summary>
        internal static void AddMannequinSpawn()
        {
            AddAtSceneViewCenter(MannequinGroup, "SpawnPoint_Mannequin",
                (parent, position, rotation) => TestEnvironmentSetupTool.CreateSpawnPoint(parent, PlayerRole.Mannequin, position, rotation));
        }

        /// <summary>
        /// 테스트 환경 도구와 같은 스폰 지점이 되도록 그 생성 함수를 그대로 쓴다.
        /// </summary>
        internal static void AddGuardSpawn()
        {
            AddAtSceneViewCenter(MapSceneBuilder.GuardSpawnGroup, "SpawnPoint_Guard",
                (parent, position, rotation) => TestEnvironmentSetupTool.CreateSpawnPoint(parent, PlayerRole.Guard, position, rotation));
        }

        /// <summary>손으로 놓은 후보는 한쪽에 몰리기 쉬우므로 바닥 분석으로 서로 최대한 멀리 다시 배치한다.</summary>
        internal static void RespreadGenerators()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform mapRoot = FindMapRoot(scene);
            NavMeshSurface surface = mapRoot != null ? mapRoot.GetComponent<NavMeshSurface>() : null;
            if (surface == null)
            {
                EditorUtility.DisplayDialog("전원 장치 재배치", "열린 씬에 Map 오브젝트와 NavMeshSurface가 없습니다. 맵 씬을 열고 실행하세요.", "확인");
                return;
            }

            Transform group = GetOrCreateGroup(mapRoot, GeneratorGroup);
            GeneratorSpawnPoint[] existingPoints = group.GetComponentsInChildren<GeneratorSpawnPoint>(true);
            int pointCount = Mathf.Max(MapSceneBuilder.GeneratorCandidateCount, existingPoints.Length);

            // 천장 조건 없이 걸을 수 있는 바닥 전체에서 고른다.
            List<Vector3> floorPoints = MapSceneBuilder.SampleFloorPoints(surface, indoor: false);
            List<Vector3> pickedPoints = MapSceneBuilder.PickSpread(floorPoints, pointCount, MapSceneBuilder.GeneratorEdgeClearance);
            surface.RemoveData();
            surface.navMeshData = null;

            if (pickedPoints.Count == 0)
            {
                EditorUtility.DisplayDialog("전원 장치 재배치", "바닥을 찾지 못했습니다. 바닥에 충돌체가 있는지 확인하세요.", "확인");
                return;
            }

            foreach (GeneratorSpawnPoint existingPoint in existingPoints)
                Undo.DestroyObjectImmediate(existingPoint.gameObject);

            Vector3 center = pickedPoints.Aggregate(Vector3.zero, (sum, point) => sum + point) / pickedPoints.Count;
            foreach (Vector3 position in pickedPoints)
            {
                Vector3 toCenter = center - position;
                toCenter.y = 0f;
                Quaternion rotation = toCenter.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toCenter) : Quaternion.identity;
                MapSceneBuilder.CreateMarker<GeneratorSpawnPoint>(group, "GeneratorSpawnPoint", position, rotation);
                Undo.RegisterCreatedObjectUndo(group.GetChild(group.childCount - 1).gameObject, "전원 장치 후보 재배치");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[MapEditTools] 전원 장치 후보 {pickedPoints.Count}개를 다시 배치했습니다. 씬을 저장하세요.");
        }

        /// <summary>제작자가 보고 있는 곳에 바로 놓이도록 씬 뷰 중앙에서 아래로 바닥을 찾는다.</summary>
        private static void AddAtSceneViewCenter(string groupName, string label, System.Action<Transform, Vector3, Quaternion> create)
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform mapRoot = FindMapRoot(scene);
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (mapRoot == null || sceneView == null)
            {
                EditorUtility.DisplayDialog("지점 추가", "맵 씬(Map 오브젝트가 있는 씬)을 열고 씬 뷰를 한 번 클릭한 뒤 실행하세요.", "확인");
                return;
            }

            Physics.SyncTransforms();
            Vector3 position = sceneView.pivot;
            if (Physics.Raycast(sceneView.pivot + Vector3.up * 2f, Vector3.down, out RaycastHit groundHit, 50f))
                position = groundHit.point;

            Vector3 viewForward = sceneView.rotation * Vector3.forward;
            viewForward.y = 0f;
            Quaternion rotation = viewForward.sqrMagnitude > 0.01f ? Quaternion.LookRotation(viewForward) : Quaternion.identity;

            Transform group = GetOrCreateGroup(mapRoot, groupName);
            create(group, position, rotation);
            GameObject createdPoint = group.GetChild(group.childCount - 1).gameObject;
            Undo.RegisterCreatedObjectUndo(createdPoint, $"{label} 추가");
            Selection.activeGameObject = createdPoint;
            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>맵 씬은 Map 루트 아래에 지점 그룹을 두는 규칙이라 이름으로 찾는다.</summary>
        private static Transform FindMapRoot(Scene scene)
        {
            GameObject mapRootObject = scene.GetRootGameObjects().FirstOrDefault(rootObject => rootObject.name == MapRootName);
            return mapRootObject != null ? mapRootObject.transform : null;
        }

        /// <summary>그룹을 지운 맵에서도 동작하도록 없으면 만든다.</summary>
        private static Transform GetOrCreateGroup(Transform mapRoot, string groupName)
        {
            Transform group = mapRoot.Find(groupName);
            if (group != null)
                return group;

            group = new GameObject(groupName).transform;
            group.SetParent(mapRoot, false);
            Undo.RegisterCreatedObjectUndo(group.gameObject, $"{groupName} 추가");
            return group;
        }
    }
}
