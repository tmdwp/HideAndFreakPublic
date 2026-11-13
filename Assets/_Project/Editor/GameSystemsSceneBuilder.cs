using GuardVsMannequin.Cameras;
using GuardVsMannequin.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>모드 맵이 FishNet 없이 만들어질 수 있도록 네트워크 시스템을 모든 맵이 함께 쓰는 Game_Systems 씬에 모은다.</summary>
    public static class GameSystemsSceneBuilder
    {
        public const string ScenePath = TestEnvironmentSetupTool.SceneFolder + "/Game_Systems.unity";

        private const string OverviewName = "CameraOverview";

        /// <summary>손으로 고친 시스템 씬은 코드와 어긋나기 쉬워 매번 빈 씬부터 새로 만든다.</summary>
        public static bool Build()
        {
            if (!TestEnvironmentSetupTool.TryLoadScenePrefabs(out TestEnvironmentSetupTool.ScenePrefabs prefabs))
            {
                Debug.LogError("[GameSystemsSceneBuilder] 가드·마네킹 프리팹이 없습니다. 'GameSettingTools/테스트 환경 생성'을 먼저 실행하세요.");
                return false;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SpectatorController spectator = TestEnvironmentSetupTool.CreateSceneCamera();

            TestEnvironmentSetupTool.GameSystemsRefs systems = TestEnvironmentSetupTool.CreateGameSystems(prefabs);
            TestEnvironmentSetupTool.CreateUi(systems, spectator);

            var huntingLighting = new GameObject("HuntingLighting");
            HuntingLightingController lightingController = huntingLighting.AddComponent<HuntingLightingController>();
            TestEnvironmentSetupTool.SetField(lightingController, "noiseLibrary", AssetDatabase.LoadAssetAtPath<Object>(TestEnvironmentSetupTool.NoiseLibraryPath));

            SceneLightingSetup.Apply(scene);

            // 경로가 있어야 FishNet이 씬 ID를 만들므로 먼저 저장한다.
            EditorSceneManager.SaveScene(scene, ScenePath);
            TestEnvironmentSetupTool.GenerateNetworkSceneIds(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[GameSystemsSceneBuilder] 게임 시스템 씬을 만들었습니다: {ScenePath}");
            return true;
        }

        /// <summary>맵 빌더를 다시 실행해도 지점이 쌓이지 않도록 이미 있는 지점을 재사용한다.</summary>
        public static void SetOverview(MapInfo mapInfo, Vector3 position, Quaternion rotation)
        {
            Transform overviewPoint = mapInfo.OverviewPoint;
            if (overviewPoint == null)
            {
                overviewPoint = mapInfo.transform.Find(OverviewName);
                if (overviewPoint == null)
                {
                    overviewPoint = new GameObject(OverviewName).transform;
                    overviewPoint.SetParent(mapInfo.transform, false);
                }
            }
            overviewPoint.SetPositionAndRotation(position, rotation);

            var serializedMapInfo = new SerializedObject(mapInfo);
            serializedMapInfo.FindProperty("overviewPoint").objectReferenceValue = overviewPoint;
            serializedMapInfo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>맵마다 손으로 정하지 않도록 플레이 영역 크기에 비례한 거리에서 내려다보게 한다.</summary>
        public static void PlaceOverview(MapInfo mapInfo, Bounds playArea)
        {
            float distance = Mathf.Max(playArea.size.x, playArea.size.z) * 0.6f + 10f;
            Vector3 position = playArea.center + new Vector3(0f, distance * 0.8f, -distance * 0.6f);
            SetOverview(mapInfo, position, Quaternion.LookRotation(playArea.center - position));
        }
    }
}
