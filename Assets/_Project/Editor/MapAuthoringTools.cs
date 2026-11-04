using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuardVsMannequin.Cameras;
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
    /// <summary>
    /// 맵을 직접 만들 때 쓰는 작업들. (맵 제작 도우미 창 <see cref="MapMakerWindow"/>에서 호출)
    /// - 새 맵 뼈대: 게임에 필요한 시스템·UI·경계·지점이 들어 있고, 바닥만 있는 씬을 Scenes/Custom에 만든다.
    /// - 선택한 오브젝트를 변신 사물/벽으로 등록·해제, 충돌체 추가, 조명을 사냥 시간에 꺼지는 조명으로 등록
    /// - 투명 경계를 환경 크기에 맞추기
    /// 씬 구조 규칙: Map(MapInfo, NavMeshSurface) / Map/Environment(맵 모델) / Map/Lights / 지점 그룹, 맵 밖에 Boundary (Invisible).
    /// </summary>
    public static class MapAuthoringTools
    {
        public const string MapRootName = "Map";
        public const string EnvironmentName = "Environment";
        public const string BoundaryName = "Boundary (Invisible)";

        private const float DefaultAreaSize = 40f;
        private const float DefaultCeiling = 8f;

        #region 새 맵

        /// <summary>
        /// 새 맵 씬을 만든다. 이름은 영문·숫자로 쓰며, 씬 파일은 Scenes/Custom/Map_&lt;이름&gt;.unity가 된다.
        /// 이름을 "Map_"로 시작해 적어도 접두어는 한 번만 붙는다.
        /// </summary>
        public static void CreateNewMap(string mapName)
        {
            string safeName = StripMapPrefix(new string((mapName ?? string.Empty).Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray()));
            if (string.IsNullOrEmpty(safeName))
            {
                EditorUtility.DisplayDialog("새 맵", "맵 이름을 영문·숫자로 입력하세요.", "확인");
                return;
            }
            if (!File.Exists(GameSystemsSceneBuilder.ScenePath))
            {
                EditorUtility.DisplayDialog("새 맵", "게임 시스템 씬이 없습니다. 'GameSettingTools/테스트 환경 생성'을 먼저 실행하세요.", "확인");
                return;
            }

            string sceneName = "Map_" + safeName;
            string scenePath = $"{MapCatalog.CustomMapFolder}/{sceneName}.unity";
            if (File.Exists(scenePath))
            {
                EditorUtility.DisplayDialog("새 맵", $"이미 있는 맵입니다: {scenePath}", "확인");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Directory.CreateDirectory(MapCatalog.CustomMapFolder);
            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Transform mapRoot = new GameObject(MapRootName).transform;
            Transform environment = new GameObject(EnvironmentName).transform;
            environment.SetParent(mapRoot, false);
            CreateStarterFloor(environment);

            var playArea = new Bounds(new Vector3(0f, DefaultCeiling * 0.5f, 0f), new Vector3(DefaultAreaSize, DefaultCeiling, DefaultAreaSize));
            CreateLights(mapRoot);
            CreateDefaultPoints(mapRoot);

            NavMeshSurface surface = mapRoot.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            Physics.SyncTransforms();
            float ceilingHeight = MapSceneBuilder.CreateBoundary(playArea, environment);

            MapInfo mapInfo = mapRoot.gameObject.AddComponent<MapInfo>();
            var info = new SerializedObject(mapInfo);
            info.FindProperty("mapName").stringValue = safeName;
            info.FindProperty("ceilingHeight").floatValue = ceilingHeight;
            info.ApplyModifiedPropertiesWithoutUndo();

            // 게임 시스템·HUD·씬 카메라는 공용 씬(Game_Systems)에 있다. 맵에는 카메라의 대기 화면 위치만 둔다.
            GameSystemsSceneBuilder.PlaceOverview(mapInfo, playArea);
            SceneLightingSetup.Apply(scene);

            EditorSceneManager.SaveScene(scene, scenePath);
            MapRegistrySync.Sync();

            Selection.activeGameObject = environment.gameObject;
            Debug.Log($"[MapAuthoringTools] 새 맵을 만들었습니다: {scenePath}. Map/Environment 아래에 맵 모델을 넣고 도우미 창의 순서대로 진행하세요.");
        }

        /// <summary>
        /// 앞에 붙은 "Map_"(대소문자 무시)를 뗀다. 씬 이름을 만들 때 접두어가 두 번 붙지 않게 하기 위함이다.
        /// </summary>
        private static string StripMapPrefix(string name)
        {
            const string prefix = "Map_";
            while (name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                name = name.Substring(prefix.Length);
            return name;
        }

        /// <summary>
        /// 시작용 바닥(40×40)을 만든다. 맵 모델을 넣은 뒤 지워도 된다.
        /// </summary>
        private static void CreateStarterFloor(Transform environment)
        {
            Material floorMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{TestEnvironmentSetupTool.MaterialFolder}/M_Floor.mat");
            GameObject floor = TestEnvironmentSetupTool.CreateBlock(environment, "StarterFloor (지워도 됨)", new Vector3(0f, -0.25f, 0f),
                new Vector3(DefaultAreaSize, 0.5f, DefaultAreaSize), floorMaterial);
            floor.isStatic = true;
        }

        /// <summary>
        /// 조명 그룹과 기본 방향광을 만든다. (사냥 시간 조명 제어는 공용 게임 시스템 씬에 있다)
        /// </summary>
        private static void CreateLights(Transform mapRoot)
        {
            Transform lights = new GameObject("Lights").transform;
            lights.SetParent(mapRoot, false);

            var sun = new GameObject("Directional Light");
            sun.transform.SetParent(lights, false);
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.shadows = LightShadows.Soft;
            sun.AddComponent<MapLight>();
        }

        /// <summary>
        /// 기본 지점(마네킹 스폰 10, 전원 장치 후보 12, 가드 스폰 후보 5)을 원형으로 놓는다. 맵을 만든 뒤 옮기거나 다시 배치한다.
        /// </summary>
        private static void CreateDefaultPoints(Transform mapRoot)
        {
            MapSceneBuilder.CreatePoints(mapRoot, "MannequinSpawnPoints", Ring(10, 12f), Vector3.zero,
                (parent, position, rotation) => TestEnvironmentSetupTool.CreateSpawnPoint(parent, PlayerRole.Mannequin, position, rotation));
            MapSceneBuilder.CreatePoints(mapRoot, "GeneratorSpawnPoints", Ring(12, 16f), Vector3.zero,
                (parent, position, rotation) => MapSceneBuilder.CreateMarker<GeneratorSpawnPoint>(parent, "GeneratorSpawnPoint", position, rotation));
            MapSceneBuilder.CreatePoints(mapRoot, MapSceneBuilder.GuardSpawnGroup, Ring(5, 18f), Vector3.zero,
                (parent, position, rotation) => TestEnvironmentSetupTool.CreateSpawnPoint(parent, PlayerRole.Guard, position, rotation));
        }

        /// <summary>
        /// 원 위에 고르게 놓인 점들.
        /// </summary>
        private static List<Vector3> Ring(int count, float radius)
        {
            var result = new List<Vector3>();
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                result.Add(new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
            return result;
        }

        #endregion

        #region 선택한 오브젝트

        /// <summary>
        /// 선택한 오브젝트를 하나씩 변신 가능 사물로 등록한다. 자식 조각까지 합쳐 그 모습 그대로 변신한다. 등록한 개수를 반환한다.
        /// (여러 조각으로 된 가구는 부모를 선택한다)
        /// </summary>
        public static int RegisterSelectedAsProps()
        {
            return RegisterSelected(isWall: false, "변신 사물");
        }

        /// <summary>
        /// 선택한 오브젝트를 하나씩 벽으로 등록한다. (변신하면 고정 크기 벽 조각) 등록한 개수를 반환한다.
        /// </summary>
        public static int RegisterSelectedAsWalls()
        {
            return RegisterSelected(isWall: true, "벽");
        }

        /// <summary>
        /// 선택한 오브젝트(자식 포함)의 변신 가능 등록을 해제한다. 해제한 개수를 반환한다.
        /// </summary>
        public static int UnregisterSelected()
        {
            int removed = 0;
            foreach (TransformableProp prop in SelectedComponents<TransformableProp>())
            {
                Undo.DestroyObjectImmediate(prop);
                removed++;
            }
            MarkDirty();
            return removed;
        }

        /// <summary>
        /// 선택한 오브젝트(자식 포함) 중 충돌체가 없는 메시에 MeshCollider를 붙인다. (플레이어가 통과하지 않고, NavMesh에 포함됨)
        /// </summary>
        public static int AddCollidersToSelection()
        {
            int added = 0;
            foreach (MeshRenderer target in SelectedComponents<MeshRenderer>())
            {
                MeshFilter filter = target.GetComponent<MeshFilter>();
                if (target.GetComponent<Collider>() != null || filter == null || filter.sharedMesh == null)
                    continue;
                Undo.AddComponent<MeshCollider>(target.gameObject).sharedMesh = filter.sharedMesh;
                added++;
            }
            MarkDirty();
            return added;
        }

        /// <summary>
        /// 씬의 모든 조명(또는 선택한 조명)을 사냥 시간에 꺼지는 MapLight로 등록하고 실시간 조명으로 바꾼다.
        /// </summary>
        public static int RegisterLights(bool selectionOnly)
        {
            IEnumerable<Light> lights = selectionOnly
                ? SelectedComponents<Light>()
                : Object.FindObjectsByType<Light>(FindObjectsInactive.Include);

            int registered = 0;
            foreach (Light target in lights)
            {
                Undo.RecordObject(target, "조명 등록");
                target.lightmapBakeType = LightmapBakeType.Realtime;
                if (target.GetComponent<MapLight>() == null)
                {
                    Undo.AddComponent<MapLight>(target.gameObject);
                    registered++;
                }
            }
            MarkDirty();
            return registered;
        }

        /// <summary>
        /// 투명 경계(벽·천장)를 Map/Environment의 크기에 맞춰 다시 만든다. 맵 정보의 천장 높이도 맞춘다.
        /// </summary>
        public static bool FitBoundaryToEnvironment()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform mapRoot = FindMapRoot(scene);
            Transform environment = mapRoot != null ? mapRoot.Find(EnvironmentName) : null;
            if (environment == null || environment.GetComponentsInChildren<Renderer>().Length == 0)
            {
                EditorUtility.DisplayDialog("경계 맞추기", "Map/Environment 아래에 맵 모델이 없습니다.", "확인");
                return false;
            }

            GameObject oldBoundary = scene.GetRootGameObjects().FirstOrDefault(r => r.name == BoundaryName);
            if (oldBoundary != null)
                Undo.DestroyObjectImmediate(oldBoundary);

            Bounds bounds = CharacterAssetBuilder.GetRendererBounds(environment.gameObject);
            float ceilingHeight = MapSceneBuilder.CreateBoundary(bounds, environment);
            GameObject newBoundary = scene.GetRootGameObjects().FirstOrDefault(r => r.name == BoundaryName);
            if (newBoundary != null)
                Undo.RegisterCreatedObjectUndo(newBoundary, "경계 맞추기");

            MapInfo mapInfo = mapRoot.GetComponent<MapInfo>();
            if (mapInfo != null)
            {
                var info = new SerializedObject(mapInfo);
                info.FindProperty("ceilingHeight").floatValue = ceilingHeight;
                info.ApplyModifiedProperties();
            }
            MarkDirty();
            return true;
        }

        /// <summary>
        /// 선택한 오브젝트마다 변신 사물로 등록한다. 자식에 따로 붙은 변신 사물 표시는 합친다.
        /// 선택 안에 다른 선택의 자식이 있으면 가장 위의 오브젝트만 등록한다.
        /// </summary>
        private static int RegisterSelected(bool isWall, string label)
        {
            GameObject[] roots = Selection.gameObjects
                .Where(selected => !Selection.gameObjects.Any(other => other != selected && selected.transform.IsChildOf(other.transform)))
                .ToArray();

            int count = roots.Count(target => PropRegistrar.Register(target, isWall, absorbChildren: true));
            MarkDirty();
            Debug.Log($"[MapAuthoringTools] {label} {count}개 등록 (번호는 씬을 저장할 때 매겨집니다)");
            return count;
        }

        /// <summary>
        /// 선택한 오브젝트와 그 자식에서 컴포넌트를 모은다. (중복 제거)
        /// </summary>
        private static List<T> SelectedComponents<T>() where T : Component
        {
            return Selection.gameObjects
                .SelectMany(selected => selected.GetComponentsInChildren<T>(true))
                .Distinct()
                .ToList();
        }

        #endregion

        /// <summary>
        /// 씬의 Map 루트를 찾는다.
        /// </summary>
        public static Transform FindMapRoot(Scene scene)
        {
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == MapRootName);
            return root != null ? root.transform : null;
        }

        /// <summary>
        /// 열린 씬을 저장 필요 상태로 표시한다.
        /// </summary>
        private static void MarkDirty()
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
