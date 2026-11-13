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
    /// <summary>맵 제작 도우미 창이 호출하는 맵 만들기 작업들을 모은다.</summary>
    public static class MapAuthoringTools
    {
        public const string MapRootName = "Map";
        public const string EnvironmentName = "Environment";
        public const string BoundaryName = "Boundary (Invisible)";

        private const float DefaultAreaSize = 40f;
        private const float DefaultCeiling = 8f;

        #region 새 맵

        /// <summary>처음 맵을 만드는 사람이 구조를 몰라도 되도록 시스템 연결·경계·기본 지점이 든 빈 맵을 만든다.</summary>
        public static void CreateNewMap(string mapName)
        {
            string safeName = StripMapPrefix(new string((mapName ?? string.Empty).Where(character => char.IsLetterOrDigit(character) || character == '_').ToArray()));
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
            var serializedMapInfo = new SerializedObject(mapInfo);
            serializedMapInfo.FindProperty("mapName").stringValue = safeName;
            serializedMapInfo.FindProperty("ceilingHeight").floatValue = ceilingHeight;
            serializedMapInfo.ApplyModifiedPropertiesWithoutUndo();

            // 게임 시스템은 공용 씬에 있으므로 맵에는 카메라의 대기 화면 위치만 둔다.
            GameSystemsSceneBuilder.PlaceOverview(mapInfo, playArea);
            SceneLightingSetup.Apply(scene);

            EditorSceneManager.SaveScene(scene, scenePath);
            MapRegistrySync.Sync();

            Selection.activeGameObject = environment.gameObject;
            Debug.Log($"[MapAuthoringTools] 새 맵을 만들었습니다: {scenePath}. Map/Environment 아래에 맵 모델을 넣고 도우미 창의 순서대로 진행하세요.");
        }

        /// <summary>
        /// 사용자가 "Map_"을 붙여 입력하면 접두어가 두 번 붙으므로 앞에 붙은 것(대소문자 무시)을 모두 뗀다.
        /// </summary>
        private static string StripMapPrefix(string mapName)
        {
            const string prefix = "Map_";
            while (mapName.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                mapName = mapName.Substring(prefix.Length);
            return mapName;
        }

        /// <summary>모델을 넣기 전에도 바로 테스트해 볼 수 있도록 걸을 수 있는 바닥을 둔다.</summary>
        private static void CreateStarterFloor(Transform environment)
        {
            Material floorMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{TestEnvironmentSetupTool.MaterialFolder}/M_Floor.mat");
            GameObject starterFloor = TestEnvironmentSetupTool.CreateBlock(environment, "StarterFloor (지워도 됨)", new Vector3(0f, -0.25f, 0f),
                new Vector3(DefaultAreaSize, 0.5f, DefaultAreaSize), floorMaterial);
            starterFloor.isStatic = true;
        }

        /// <summary>조명이 없으면 맵이 깜깜해 작업할 수 없으므로 사냥 시간에 꺼지는 기본 방향광을 둔다.</summary>
        private static void CreateLights(Transform mapRoot)
        {
            Transform lightGroup = new GameObject("Lights").transform;
            lightGroup.SetParent(mapRoot, false);

            var sunObject = new GameObject("Directional Light");
            sunObject.transform.SetParent(lightGroup, false);
            sunObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light sunLight = sunObject.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.shadows = LightShadows.Soft;
            sunObject.AddComponent<MapLight>();
        }

        /// <summary>바로 판을 시작할 수 있도록 맵 검사가 요구하는 수만큼 지점을 원형으로 미리 놓는다.</summary>
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
        /// 맵 모양을 모르는 상태라 지점이 한쪽에 몰리지 않도록 원 위에 고르게 나눈다.
        /// </summary>
        private static List<Vector3> Ring(int count, float radius)
        {
            var ringPoints = new List<Vector3>();
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                ringPoints.Add(new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
            return ringPoints;
        }

        #endregion

        #region 선택한 오브젝트

        /// <summary>여러 조각으로 된 가구가 통째로 변신하도록 부모를 선택하면 자식 조각까지 합친다.</summary>
        public static int RegisterSelectedAsProps()
        {
            return RegisterSelected("변신 사물");
        }

        /// <summary>잘못 등록한 것을 되돌릴 수 있게 자식까지 Undo로 해제한다.</summary>
        public static int UnregisterSelected()
        {
            int removedCount = 0;
            foreach (TransformableProp prop in SelectedComponents<TransformableProp>())
            {
                Undo.DestroyObjectImmediate(prop);
                removedCount++;
            }
            MarkDirty();
            return removedCount;
        }

        /// <summary>충돌체가 없으면 플레이어가 통과하고 NavMesh에서도 빠지므로 충돌체 없는 메시에 MeshCollider를 붙인다.</summary>
        public static int AddCollidersToSelection()
        {
            int addedCount = 0;
            foreach (MeshRenderer meshRenderer in SelectedComponents<MeshRenderer>())
            {
                MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
                if (meshRenderer.GetComponent<Collider>() != null || meshFilter == null || meshFilter.sharedMesh == null)
                    continue;
                Undo.AddComponent<MeshCollider>(meshRenderer.gameObject).sharedMesh = meshFilter.sharedMesh;
                addedCount++;
            }
            MarkDirty();
            return addedCount;
        }

        /// <summary>사냥 시간에 실행 중 꺼야 하므로 조명을 실시간으로 바꾸고 MapLight를 붙인다.</summary>
        public static int RegisterLights(bool selectionOnly)
        {
            IEnumerable<Light> lights = selectionOnly
                ? SelectedComponents<Light>()
                : Object.FindObjectsByType<Light>(FindObjectsInactive.Include);

            int registeredCount = 0;
            foreach (Light sceneLight in lights)
            {
                Undo.RecordObject(sceneLight, "조명 등록");
                sceneLight.lightmapBakeType = LightmapBakeType.Realtime;
                if (sceneLight.GetComponent<MapLight>() == null)
                {
                    Undo.AddComponent<MapLight>(sceneLight.gameObject);
                    registeredCount++;
                }
            }
            MarkDirty();
            return registeredCount;
        }

        /// <summary>맵 모델을 바꾸면 밖으로 나갈 수 있으므로 환경 크기에 맞춰 경계를 다시 만든다.</summary>
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

            GameObject oldBoundary = scene.GetRootGameObjects().FirstOrDefault(rootObject => rootObject.name == BoundaryName);
            if (oldBoundary != null)
                Undo.DestroyObjectImmediate(oldBoundary);

            Bounds environmentBounds = CharacterAssetBuilder.GetRendererBounds(environment.gameObject);
            float ceilingHeight = MapSceneBuilder.CreateBoundary(environmentBounds, environment);
            GameObject newBoundary = scene.GetRootGameObjects().FirstOrDefault(rootObject => rootObject.name == BoundaryName);
            if (newBoundary != null)
                Undo.RegisterCreatedObjectUndo(newBoundary, "경계 맞추기");

            MapInfo mapInfo = mapRoot.GetComponent<MapInfo>();
            if (mapInfo != null)
            {
                var serializedMapInfo = new SerializedObject(mapInfo);
                serializedMapInfo.FindProperty("ceilingHeight").floatValue = ceilingHeight;
                serializedMapInfo.ApplyModifiedProperties();
            }
            MarkDirty();
            return true;
        }

        /// <summary>
        /// 부모와 자식을 함께 선택하면 같은 물체가 두 번 등록되므로 선택 안에서 가장 위의 오브젝트만 등록하고, 자식에 따로 붙은 표시는 합친다.
        /// </summary>
        private static int RegisterSelected(string label)
        {
            GameObject[] roots = Selection.gameObjects
                .Where(selected => !Selection.gameObjects.Any(other => other != selected && selected.transform.IsChildOf(other.transform)))
                .ToArray();

            int registeredCount = roots.Count(target => PropRegistrar.Register(target, absorbChildren: true));
            MarkDirty();
            Debug.Log($"[MapAuthoringTools] {label} {registeredCount}개 등록 (번호는 씬을 저장할 때 매겨집니다)");
            return registeredCount;
        }

        /// <summary>
        /// 부모와 자식을 함께 선택하면 같은 컴포넌트가 두 번 모이므로 중복을 없앤다.
        /// </summary>
        private static List<T> SelectedComponents<T>() where T : Component
        {
            return Selection.gameObjects
                .SelectMany(selected => selected.GetComponentsInChildren<T>(true))
                .Distinct()
                .ToList();
        }

        #endregion

        /// <summary>맵 씬은 Map 루트 아래에 모든 것을 두는 규칙이라 이름으로 찾는다.</summary>
        public static Transform FindMapRoot(Scene scene)
        {
            GameObject mapRootObject = scene.GetRootGameObjects().FirstOrDefault(rootObject => rootObject.name == MapRootName);
            return mapRootObject != null ? mapRootObject.transform : null;
        }

        /// <summary>
        /// 코드로 바꾼 내용은 씬이 저장 필요 상태가 되지 않아 저장 없이 닫으면 사라지므로 직접 표시한다.
        /// </summary>
        private static void MarkDirty()
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
