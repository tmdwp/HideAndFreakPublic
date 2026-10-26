using GuardVsMannequin.Cameras;
using GuardVsMannequin.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 모든 맵이 함께 쓰는 게임 시스템 씬(Game_Systems)을 만든다. 판을 시작하면 맵 씬과 함께 불러온다(<c>RoomState</c>).
    /// 들어 있는 것: 게임 진행 시스템(씬 네트워크 오브젝트), HUD·ESC 메뉴, 저체력 효과, 씬 카메라(관전), 사냥 시간 조명 제어.
    /// 맵 씬에는 환경·지점·변신 사물·조명만 두므로 네트워크 오브젝트가 없다. (모드 맵이 FishNet 없이 만들어질 수 있도록)
    /// </summary>
    public static class GameSystemsSceneBuilder
    {
        /// <summary>게임 시스템 씬 경로.</summary>
        public const string ScenePath = TestEnvironmentSetupTool.SceneFolder + "/Game_Systems.unity";

        private const string OverviewName = "CameraOverview";

        /// <summary>
        /// 게임 시스템 씬을 새로 만들어 저장한다. (열린 씬은 닫힌다) 프리팹이 없으면 false.
        /// </summary>
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

            var lighting = new GameObject("HuntingLighting");
            HuntingLightingController controller = lighting.AddComponent<HuntingLightingController>();
            TestEnvironmentSetupTool.SetField(controller, "noiseLibrary", AssetDatabase.LoadAssetAtPath<Object>(TestEnvironmentSetupTool.NoiseLibraryPath));

            SceneLightingSetup.Apply(scene);

            // 경로가 있어야 FishNet이 씬 ID를 만들므로 먼저 저장한다.
            EditorSceneManager.SaveScene(scene, ScenePath);
            TestEnvironmentSetupTool.GenerateNetworkSceneIds(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[GameSystemsSceneBuilder] 게임 시스템 씬을 만들었습니다: {ScenePath}");
            return true;
        }

        /// <summary>
        /// 맵 정보에 씬 카메라의 대기 화면 위치를 지정한다. (없으면 Map 아래에 만든다)
        /// </summary>
        public static void SetOverview(MapInfo mapInfo, Vector3 position, Quaternion rotation)
        {
            Transform point = mapInfo.OverviewPoint;
            if (point == null)
            {
                point = mapInfo.transform.Find(OverviewName);
                if (point == null)
                {
                    point = new GameObject(OverviewName).transform;
                    point.SetParent(mapInfo.transform, false);
                }
            }
            point.SetPositionAndRotation(position, rotation);

            var serialized = new SerializedObject(mapInfo);
            serialized.FindProperty("overviewPoint").objectReferenceValue = point;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 플레이 영역을 비스듬히 내려다보는 대기 화면 위치를 지정한다.
        /// </summary>
        public static void PlaceOverview(MapInfo mapInfo, Bounds playArea)
        {
            float distance = Mathf.Max(playArea.size.x, playArea.size.z) * 0.6f + 10f;
            Vector3 position = playArea.center + new Vector3(0f, distance * 0.8f, -distance * 0.6f);
            SetOverview(mapInfo, position, Quaternion.LookRotation(playArea.center - position));
        }
    }
}
