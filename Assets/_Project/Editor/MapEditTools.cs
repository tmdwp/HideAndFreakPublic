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
    /// <summary>
    /// 열려 있는 맵 씬의 배치를 손쉽게 고치는 기능. 맵 제작 도우미(<see cref="MapMakerWindow"/>)의 "지점 배치" 버튼이 쓴다. (모두 Ctrl+Z로 되돌릴 수 있다)
    /// - 씬 뷰가 보고 있는 곳(화면 중앙 아래 바닥)에 전원 장치 후보 / 마네킹 스폰 / 가드 스폰 후보 추가
    /// - 전원 장치 후보를 NavMesh 바닥 분석으로 넓게 다시 배치
    /// 추가한 지점은 이동 도구(W)로 옮기고, 필요 없으면 지우면 된다.
    /// </summary>
    public static class MapEditTools
    {
        private const string MapRootName = "Map";
        private const string GeneratorGroup = "GeneratorSpawnPoints";
        private const string MannequinGroup = "MannequinSpawnPoints";

        /// <summary>
        /// 씬 뷰 중앙 아래 바닥에 전원 장치 후보를 추가한다.
        /// </summary>
        internal static void AddGeneratorPoint()
        {
            AddAtSceneViewCenter(GeneratorGroup, "GeneratorSpawnPoint",
                (parent, position, rotation) => MapSceneBuilder.CreateMarker<GeneratorSpawnPoint>(parent, "GeneratorSpawnPoint", position, rotation));
        }

        /// <summary>
        /// 씬 뷰 중앙 아래 바닥에 마네킹 스폰 지점을 추가한다.
        /// </summary>
        internal static void AddMannequinSpawn()
        {
            AddAtSceneViewCenter(MannequinGroup, "SpawnPoint_Mannequin",
                (parent, position, rotation) => TestEnvironmentSetupTool.CreateSpawnPoint(parent, PlayerRole.Mannequin, position, rotation));
        }

        /// <summary>
        /// 씬 뷰 중앙 아래 바닥에 가드 스폰 후보를 추가한다.
        /// </summary>
        internal static void AddGuardSpawn()
        {
            AddAtSceneViewCenter(MapSceneBuilder.GuardSpawnGroup, "SpawnPoint_Guard",
                (parent, position, rotation) => TestEnvironmentSetupTool.CreateSpawnPoint(parent, PlayerRole.Guard, position, rotation));
        }

        /// <summary>
        /// 전원 장치 후보를 지우고, 바닥 분석으로 서로 최대한 멀리 떨어지게 다시 배치한다.
        /// 개수는 룸 설정 최댓값(12개)과 기존 개수 중 큰 쪽이다.
        /// </summary>
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
            GeneratorSpawnPoint[] existing = group.GetComponentsInChildren<GeneratorSpawnPoint>(true);
            int count = Mathf.Max(MapSceneBuilder.GeneratorCandidateCount, existing.Length);

            // 천장 조건 없이 걸을 수 있는 바닥 전체에서 고른다.
            List<Vector3> floor = MapSceneBuilder.SampleFloorPoints(surface, indoor: false);
            List<Vector3> picked = MapSceneBuilder.PickSpread(floor, count, MapSceneBuilder.GeneratorEdgeClearance);
            surface.RemoveData();
            surface.navMeshData = null;

            if (picked.Count == 0)
            {
                EditorUtility.DisplayDialog("전원 장치 재배치", "바닥을 찾지 못했습니다. 바닥에 충돌체가 있는지 확인하세요.", "확인");
                return;
            }

            foreach (GeneratorSpawnPoint point in existing)
                Undo.DestroyObjectImmediate(point.gameObject);

            Vector3 center = picked.Aggregate(Vector3.zero, (sum, p) => sum + p) / picked.Count;
            foreach (Vector3 position in picked)
            {
                Vector3 toCenter = center - position;
                toCenter.y = 0f;
                Quaternion rotation = toCenter.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toCenter) : Quaternion.identity;
                MapSceneBuilder.CreateMarker<GeneratorSpawnPoint>(group, "GeneratorSpawnPoint", position, rotation);
                Undo.RegisterCreatedObjectUndo(group.GetChild(group.childCount - 1).gameObject, "전원 장치 후보 재배치");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[MapEditTools] 전원 장치 후보 {picked.Count}개를 다시 배치했습니다. 씬을 저장하세요.");
        }

        /// <summary>
        /// 씬 뷰 화면 중앙에서 아래로 바닥을 찾아 지점을 만들고 선택한다.
        /// </summary>
        private static void AddAtSceneViewCenter(string groupName, string label, System.Action<Transform, Vector3, Quaternion> create)
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform mapRoot = FindMapRoot(scene);
            SceneView view = SceneView.lastActiveSceneView;
            if (mapRoot == null || view == null)
            {
                EditorUtility.DisplayDialog("지점 추가", "맵 씬(Map 오브젝트가 있는 씬)을 열고 씬 뷰를 한 번 클릭한 뒤 실행하세요.", "확인");
                return;
            }

            Physics.SyncTransforms();
            Vector3 position = view.pivot;
            if (Physics.Raycast(view.pivot + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 50f))
                position = hit.point;

            Vector3 forward = view.rotation * Vector3.forward;
            forward.y = 0f;
            Quaternion rotation = forward.sqrMagnitude > 0.01f ? Quaternion.LookRotation(forward) : Quaternion.identity;

            Transform group = GetOrCreateGroup(mapRoot, groupName);
            create(group, position, rotation);
            GameObject created = group.GetChild(group.childCount - 1).gameObject;
            Undo.RegisterCreatedObjectUndo(created, $"{label} 추가");
            Selection.activeGameObject = created;
            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>
        /// 씬의 Map 루트를 찾는다.
        /// </summary>
        private static Transform FindMapRoot(Scene scene)
        {
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == MapRootName);
            return root != null ? root.transform : null;
        }

        /// <summary>
        /// Map 아래의 그룹 오브젝트를 찾거나 만든다.
        /// </summary>
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
