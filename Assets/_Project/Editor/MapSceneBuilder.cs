using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuardVsMannequin.Cameras;
using GuardVsMannequin.Core;
using GuardVsMannequin.Map;
using GuardVsMannequin.Objectives;
using GuardVsMannequin.Player;
using GuardVsMannequin.Props;
using GuardVsMannequin.Room;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 외부 에셋의 데모 씬으로 게임 맵 씬을 만든다. (<see cref="MapCatalog"/>의 1번 이후 맵)
    /// 1. 원본 씬의 오브젝트를 가져와 게임에 필요 없는 스크립트·카메라·UI를 제거하고 머티리얼을 URP로 변환한다.
    /// 2. 충돌체가 없는 메시에 충돌체를 붙이고, 크기·이름으로 변신 가능 사물과 벽을 골라 등록한다. (외형은 게임 중에 그대로 복사)
    /// 3. NavMesh를 임시로 구워 플레이 가능한 바닥(실내 맵은 천장이 있는 곳)을 찾고,
    ///    마네킹 스폰 지점·전원 장치 후보·가드 스폰 후보를 고르게 배치한다.
    /// 4. 조명을 사냥 시간에 꺼지는 MapLight로 등록하고, 부족하면 조명을 추가한다.
    /// 5. 투명 경계(벽·천장), 게임 시스템, HUD를 추가하고 저장한 뒤 빌드 설정과 룸 맵 목록에 등록한다.
    /// NavMesh는 "맵 최적화"(<see cref="MapOptimizer"/>)에서 구워 저장한다. 굽지 않은 맵은 서버가 실행 중에 굽는다.
    /// 자동 배치는 추정이므로, 생성 후 씬에서 위치를 확인하고 필요하면 옮긴다.
    /// "테스트 환경 생성"을 먼저 실행해 프리팹이 있어야 한다.
    /// </summary>
    public static class MapSceneBuilder
    {
        private const int FloorSampleCount = 600;
        private const int MannequinSpawnCount = 10;
        // 자동 크기 조정: 걸을 수 있는 바닥이 이보다 좁으면 환경을 키운다. (최대 10명 기준)
        private const float MinFloorArea = 800f;
        private const float MaxAutoScale = 2.5f;

        // 룸 설정의 전원 장치 수 최댓값(game_settings.csv)과 맞춘다.
        internal const int GeneratorCandidateCount = 12;
        private const int GuardSpawnCount = 5;

        /// <summary>가드 스폰 후보 지점을 모아 두는 그룹 이름.</summary>
        internal const string GuardSpawnGroup = "GuardSpawnPoints";

        private const float SpawnEdgeClearance = 0.5f;
        internal const float GeneratorEdgeClearance = 1.0f;
        private const float IndoorCeilingCheck = 12f;
        private const float LampSpacingArea = 70f;
        private const int ShadowLightLimit = 24;


        // 이름에 들어 있으면 변신 사물에서 제외하는 단어 (구조물·조명·바닥재 등)
        private static readonly string[] NonPropKeywords =
        {
            "floor", "wall", "ceil", "roof", "door", "window", "stair", "step", "light", "lamp", "bulb", "pipe", "beam",
            "column", "pillar", "frame", "trim", "ground", "terrain", "grass", "decal", "cable", "wire", "rail", "tile",
            "skirting", "baseboard", "molding", "moulding", "glass", "road", "building", "house", "fence", "stairs"
        };

        #region 메뉴

        [MenuItem(EditorMenus.Root + "맵 생성/전체 맵 생성")]
        private static void BuildAllMaps()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            foreach (MapCatalog.Entry map in MapCatalog.Maps.Where(m => !string.IsNullOrEmpty(m.sourceScenePath)))
                BuildMap(map);
            FinishBuild();
        }

        [MenuItem(EditorMenus.Root + "맵 생성/Abandoned Asylum")]
        private static void BuildAsylum() => BuildSingle("Map_Asylum");

        [MenuItem(EditorMenus.Root + "맵 생성/Industrial")]
        private static void BuildIndustrial() => BuildSingle("Map_Industrial");

        [MenuItem(EditorMenus.Root + "맵 생성/Horror Hospital")]
        private static void BuildHospital() => BuildSingle("Map_Hospital");

        [MenuItem(EditorMenus.Root + "맵 생성/Alchemist House")]
        private static void BuildAlchemistHouse() => BuildSingle("Map_AlchemistHouse");

        /// <summary>
        /// 맵 하나를 만든다. (맵 번호는 목록이 바뀌면 달라지므로 씬 이름으로 찾는다)
        /// </summary>
        private static void BuildSingle(string sceneName)
        {
            MapCatalog.Entry map = MapCatalog.Maps.FirstOrDefault(entry => entry.sceneName == sceneName);
            if (map == null)
            {
                Debug.LogError($"[MapSceneBuilder] 맵 목록에 없는 씬입니다: {sceneName}");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            BuildMap(map);
            FinishBuild();
        }

        /// <summary>
        /// 빌드 설정, 룸의 맵 목록, 맵 이름 문구, 맵 설정 범위를 갱신하고 에셋을 저장한다.
        /// </summary>
        private static void FinishBuild()
        {
            MapRegistrySync.Sync();
        }

        #endregion

        /// <summary>
        /// 맵 씬 하나를 만들어 저장한다.
        /// </summary>
        private static void BuildMap(MapCatalog.Entry map)
        {
            if (!File.Exists(GameSystemsSceneBuilder.ScenePath))
            {
                Debug.LogError("[MapSceneBuilder] 게임 시스템 씬이 없습니다. 'GameSettingTools/테스트 환경 생성'을 먼저 실행하세요.");
                return;
            }
            if (!File.Exists(map.sourceScenePath))
            {
                Debug.LogError($"[MapSceneBuilder] 원본 씬을 찾을 수 없습니다: {map.sourceScenePath}");
                return;
            }

            Debug.Log($"[MapSceneBuilder] {map.displayName} 맵 생성 시작");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Transform mapRoot = new GameObject("Map").transform;
            Transform environment = ImportEnvironment(map.sourceScenePath, scene, mapRoot);
            CleanEnvironment(environment);

            int converted = UrpMaterialConverter.ConvertRenderers(environment.GetComponentsInChildren<Renderer>(true));
            ConvertTerrains(environment);
            int colliders = AddMissingColliders(environment);
            Physics.SyncTransforms();

            NavMeshSurface navMeshSurface = mapRoot.gameObject.AddComponent<NavMeshSurface>();
            navMeshSurface.collectObjects = CollectObjects.Children;
            navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            // 작은 맵은 환경 전체를 키운다. (벽·사물 분류가 커진 크기로 되도록 등록보다 먼저)
            float mapScale = ScaleEnvironment(map, environment, navMeshSurface);

            RegisterProps(environment, out int propCount, out int wallCount);

            List<Vector3> floor = SampleFloorPoints(navMeshSurface, map.indoor);
            Bounds playArea = GetPlayArea(floor, environment);

            List<Vector3> mannequinSpawns = PickSpread(floor, MannequinSpawnCount, SpawnEdgeClearance);
            List<Vector3> generatorPoints = PickSpread(floor, GeneratorCandidateCount, GeneratorEdgeClearance);
            List<Vector3> guardSpawns = PickSpread(floor, GuardSpawnCount, SpawnEdgeClearance);
            int addedLamps = SetupLights(mapRoot, environment, floor, map.indoor);

            // 여기서 구운 NavMesh는 배치에만 쓰고 지운다. (맵 최적화에서 다시 구워 저장, 없으면 서버가 실행 중에 굽는다)
            navMeshSurface.RemoveData();
            navMeshSurface.navMeshData = null;

            CreatePoints(mapRoot, "MannequinSpawnPoints", mannequinSpawns, playArea.center,
                (parent, position, rotation) => TestEnvironmentSetupTool.CreateSpawnPoint(parent, PlayerRole.Mannequin, position, rotation));
            CreatePoints(mapRoot, "GeneratorSpawnPoints", generatorPoints, playArea.center,
                (parent, position, rotation) => CreateMarker<GeneratorSpawnPoint>(parent, "GeneratorSpawnPoint", position, rotation));
            // 가드 스폰 후보: 실제 위치는 판마다 마네킹 스폰에서 먼 곳을 고른다. (GuardSpawnSelector, NavMesh 지점도 함께 씀)
            CreatePoints(mapRoot, GuardSpawnGroup, guardSpawns, playArea.center,
                (parent, position, rotation) => TestEnvironmentSetupTool.CreateSpawnPoint(parent, PlayerRole.Guard, position, rotation));

            float ceilingHeight = CreateBoundary(playArea, environment);

            MapInfo mapInfo = mapRoot.gameObject.AddComponent<MapInfo>();
            var mapInfoSerialized = new SerializedObject(mapInfo);
            mapInfoSerialized.FindProperty("mapName").stringValue = map.displayName;
            mapInfoSerialized.FindProperty("ceilingHeight").floatValue = ceilingHeight;
            mapInfoSerialized.FindProperty("mannequinSkin").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterSkin>(map.mannequinSkinPath);
            mapInfoSerialized.ApplyModifiedPropertiesWithoutUndo();
            // 게임 시스템·HUD·씬 카메라는 공용 씬(Game_Systems)에 있다. 맵에는 카메라의 대기 화면 위치만 둔다.
            GameSystemsSceneBuilder.PlaceOverview(mapInfo, playArea);

            // 최적화(오클루전 굽기)에 저장된 경로가 필요하므로 먼저 저장한다.
            EditorSceneManager.SaveScene(scene, map.ScenePath);

            // 경계 밖·꺼진 오브젝트 제거, 정적 플래그, 오클루전 컬링 굽기 (저장된 경로가 필요)
            MapOptimizer.Optimize(scene, map.indoor);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[MapSceneBuilder] {map.displayName} 완료: {map.ScenePath}\n" +
                      $"맵 크기 배율 {mapScale:F2}, 머티리얼 변환 {converted}개, 충돌체 추가 {colliders}개, 변신 사물 {propCount}개(벽 {wallCount}개), " +
                      $"바닥 샘플 {floor.Count}개, 조명 추가 {addedLamps}개. 스폰·전원 장치·가드 스폰 후보 위치를 씬에서 확인하세요.");
        }

        #region 환경 가져오기

        /// <summary>
        /// 원본 씬을 추가로 열어 모든 루트 오브젝트를 새 씬의 Map/Environment 아래로 옮기고 원본은 저장하지 않고 닫는다.
        /// 하늘과 환경광 설정도 가져온다.
        /// </summary>
        private static Transform ImportEnvironment(string sourcePath, Scene target, Transform mapRoot)
        {
            Scene source = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Additive);

            SceneManager.SetActiveScene(source);
            Material skybox = RenderSettings.skybox;
            AmbientMode ambientMode = RenderSettings.ambientMode;
            Color ambientSky = RenderSettings.ambientSkyColor;
            Color ambientEquator = RenderSettings.ambientEquatorColor;
            Color ambientGround = RenderSettings.ambientGroundColor;
            float ambientIntensity = RenderSettings.ambientIntensity;
            SceneManager.SetActiveScene(target);

            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.ambientIntensity = ambientIntensity;

            Transform environment = new GameObject("Environment").transform;
            environment.SetParent(mapRoot, false);
            foreach (GameObject root in source.GetRootGameObjects())
            {
                SceneManager.MoveGameObjectToScene(root, target);
                root.transform.SetParent(environment, true);
            }

            EditorSceneManager.CloseScene(source, true);
            return environment;
        }

        /// <summary>
        /// 게임에 방해되는 것을 제거한다.
        /// - 카메라·플레이어 컨트롤러·UI·이벤트 시스템 오브젝트
        /// - 누락된 스크립트, Unity 기본 컴포넌트가 아닌 스크립트(에셋 데모용 스크립트)
        /// - 물리 오브젝트는 움직이지 않게 고정 (피어마다 따로 시뮬레이션되면 위치가 어긋나므로)
        /// </summary>
        internal static void CleanEnvironment(Transform environment)
        {
            // 프리팹 인스턴스 안의 오브젝트는 지울 수 없으므로 먼저 연결을 푼다. (생성된 맵은 원본 에셋과 독립)
            foreach (Transform child in environment.GetComponentsInChildren<Transform>(true))
            {
                if (child != null && PrefabUtility.IsOutermostPrefabInstanceRoot(child.gameObject))
                    PrefabUtility.UnpackPrefabInstance(child.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            foreach (Transform child in environment.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (child == null || child == environment)
                    continue;

                GameObject target = child.gameObject;
                if (target.GetComponent<Camera>() != null || target.GetComponent<CharacterController>() != null
                    || target.GetComponent<Canvas>() != null || target.GetComponent<UnityEngine.EventSystems.EventSystem>() != null)
                {
                    Object.DestroyImmediate(target);
                    continue;
                }

                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);
                foreach (MonoBehaviour behaviour in target.GetComponents<MonoBehaviour>())
                {
                    string ns = behaviour.GetType().Namespace ?? string.Empty;
                    if (!ns.StartsWith("UnityEngine"))
                        Object.DestroyImmediate(behaviour);
                }

                foreach (AudioListener listener in target.GetComponents<AudioListener>())
                    Object.DestroyImmediate(listener);
                foreach (Rigidbody body in target.GetComponents<Rigidbody>())
                    body.isKinematic = true;
            }
        }

        /// <summary>
        /// 지형 머티리얼이 URP용이 아니면 URP 지형 머티리얼로 바꾼다.
        /// </summary>
        private static void ConvertTerrains(Transform environment)
        {
            Terrain[] terrains = environment.GetComponentsInChildren<Terrain>(true);
            if (terrains.Length == 0)
                return;

            Shader terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (terrainShader == null)
                return;

            string path = $"{TestEnvironmentSetupTool.MaterialFolder}/M_TerrainLit.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(terrainShader);
                AssetDatabase.CreateAsset(material, path);
            }

            foreach (Terrain terrain in terrains)
            {
                if (terrain.materialTemplate == null || !terrain.materialTemplate.shader.name.StartsWith("Universal Render Pipeline"))
                    terrain.materialTemplate = material;
            }
        }

        /// <summary>
        /// 충돌체가 없는 메시에 메시 충돌체를 붙인다. (LOD는 가장 자세한 단계만, 아주 작은 장식은 제외)
        /// </summary>
        internal static int AddMissingColliders(Transform environment)
        {
            HashSet<Renderer> lowerLods = GetLowerLodRenderers(environment);
            int added = 0;
            foreach (MeshRenderer meshRenderer in environment.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (lowerLods.Contains(meshRenderer) || meshRenderer.GetComponent<Collider>() != null)
                    continue;

                MeshFilter filter = meshRenderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;
                if (meshRenderer.bounds.size.magnitude < 0.1f)
                    continue;

                MeshCollider meshCollider = meshRenderer.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = filter.sharedMesh;
                added++;
            }
            return added;
        }

        /// <summary>
        /// LOD 그룹에서 가장 자세한 단계(LOD0)가 아닌 렌더러를 모은다.
        /// </summary>
        private static HashSet<Renderer> GetLowerLodRenderers(Transform environment)
        {
            var result = new HashSet<Renderer>();
            foreach (LODGroup group in environment.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = group.GetLODs();
                for (int i = 1; i < lods.Length; i++)
                {
                    foreach (Renderer lodRenderer in lods[i].renderers)
                    {
                        if (lodRenderer != null)
                            result.Add(lodRenderer);
                    }
                }
            }
            return result;
        }

        #endregion

        #region 맵 크기

        /// <summary>
        /// 환경을 키울 배율을 정해 적용한다. 적용한 배율을 반환한다.
        /// - MapCatalog의 scale이 0보다 크면 그 값을 그대로 쓴다.
        /// - 0이면 걸을 수 있는 바닥 넓이를 재서 <see cref="MinFloorArea"/>보다 좁을 때만 넓이가 맞도록 키운다. (최대 <see cref="MaxAutoScale"/>배)
        /// 조명의 범위도 같은 배율로 늘린다. (Light.range는 트랜스폼 크기의 영향을 받지 않으므로)
        /// </summary>
        private static float ScaleEnvironment(MapCatalog.Entry map, Transform environment, NavMeshSurface surface)
        {
            float factor = map.scale;
            if (factor <= 0f)
            {
                List<Vector3> floor = SampleFloorPoints(surface, map.indoor);
                float area = EstimateFloorArea(floor);
                factor = area > 0f && area < MinFloorArea ? Mathf.Clamp(Mathf.Sqrt(MinFloorArea / area), 1f, MaxAutoScale) : 1f;
                surface.RemoveData();
                surface.navMeshData = null;
                Debug.Log($"[MapSceneBuilder] {map.displayName}: 바닥 넓이 약 {area:F0}㎡ → 배율 {factor:F2}");
            }

            if (Mathf.Approximately(factor, 1f))
                return 1f;

            environment.localScale *= factor;
            foreach (Light sceneLight in environment.GetComponentsInChildren<Light>(true))
            {
                if (sceneLight.type != LightType.Directional)
                    sceneLight.range *= factor;
            }
            Physics.SyncTransforms();
            return factor;
        }

        #endregion

        #region 변신 사물

        /// <summary>
        /// 크기와 이름으로 변신 가능 사물과 벽을 골라 등록한다. (렌더러마다, 외형은 게임 중에 그대로 복사)
        /// - 사물: 가장 긴 변 0.3~2.6m, 높이 2.4m 이하, 바닥 면적 4㎡ 미만, 구조물 이름이 아님
        /// - 벽: 높이 2m 이상, 두께 0.6m 이하, 길이 1m 이상 → 고정 크기 벽 조각으로 변신
        ///   (단, 정점이 많고 변신 가능한 크기면 책장·옷장 같은 가구로 보고 그대로 복사)
        /// 조명 기구와 LOD 하위 단계는 제외한다.
        /// </summary>
        private static void RegisterProps(Transform environment, out int propCount, out int wallCount)
        {
            HashSet<Renderer> lowerLods = GetLowerLodRenderers(environment);
            propCount = 0;
            wallCount = 0;

            foreach (MeshRenderer meshRenderer in environment.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter filter = meshRenderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || lowerLods.Contains(meshRenderer))
                    continue;
                if (meshRenderer.GetComponent<Collider>() == null || IsLightFixture(meshRenderer.transform))
                    continue;

                Vector3 size = meshRenderer.bounds.size;
                float longest = Mathf.Max(size.x, size.y, size.z);
                float thickness = Mathf.Min(size.x, size.z);
                float length = Mathf.Max(size.x, size.z);

                bool isNonProp = HasKeyword(meshRenderer.transform, NonPropKeywords);
                if (size.y >= 2f && thickness <= 0.6f && length >= 1f)
                {
                    if (!isNonProp && PropRegistrar.LooksLikeFurniture(meshRenderer))
                    {
                        if (PropRegistrar.Register(meshRenderer.gameObject, isWall: false, absorbChildren: false))
                            propCount++;
                    }
                    else if (PropRegistrar.Register(meshRenderer.gameObject, isWall: true, absorbChildren: false))
                    {
                        wallCount++;
                    }
                    continue;
                }

                bool isProp = longest >= 0.3f && longest <= 2.6f && size.y <= 2.4f && size.x * size.z < 4f && !isNonProp;
                if (isProp && PropRegistrar.Register(meshRenderer.gameObject, isWall: false, absorbChildren: false))
                    propCount++;
            }
        }

        /// <summary>
        /// 조명이 붙은 오브젝트(조명 기구)인지 확인한다. 조명 기구는 변신 대상이 아니다.
        /// </summary>
        private static bool IsLightFixture(Transform target)
        {
            return target.GetComponentInChildren<Light>(true) != null
                   || (target.parent != null && target.parent.GetComponentInChildren<Light>(true) != null && target.parent.childCount <= 3);
        }

        /// <summary>
        /// 오브젝트나 부모 이름에 제외 단어가 들어 있는지 확인한다. (대소문자 무시)
        /// </summary>
        private static bool HasKeyword(Transform target, string[] keywords)
        {
            string name = (target.name + " " + (target.parent != null ? target.parent.name : string.Empty)).ToLowerInvariant();
            return keywords.Any(name.Contains);
        }

        #endregion

        #region 바닥 분석과 배치

        /// <summary>
        /// NavMesh를 구워 걸을 수 있는 바닥에서 점을 넓이에 비례해 뽑는다.
        /// 벽에 너무 붙은 점, (실내 맵이면) 천장이 없는 점을 버리고, 가장 넓게 이어진 영역의 점만 남긴다.
        /// (지붕 위처럼 떨어진 곳을 제외하기 위함)
        /// </summary>
        internal static List<Vector3> SampleFloorPoints(NavMeshSurface surface, bool indoor)
        {
            surface.BuildNavMesh();
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            List<Vector3> result;
            int triangleCount = triangulation.indices.Length / 3;
            if (triangleCount == 0)
            {
                Debug.LogError("[MapSceneBuilder] NavMesh를 만들지 못했습니다. 바닥에 충돌체가 있는지 확인하세요.");
                return new List<Vector3>();
            }

            // 삼각형 넓이의 누적 합으로 넓이 비례 추출
            var cumulative = new float[triangleCount];
            float total = 0f;
            for (int i = 0; i < triangleCount; i++)
            {
                Vector3 a = triangulation.vertices[triangulation.indices[i * 3]];
                Vector3 b = triangulation.vertices[triangulation.indices[i * 3 + 1]];
                Vector3 c = triangulation.vertices[triangulation.indices[i * 3 + 2]];
                total += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                cumulative[i] = total;
            }

            // 천장 검사는 아래에서 위로 쏘므로 메시의 뒷면을 맞혀야 한다. (윗면만 있는 천장 메시가 많아,
            // 기본 설정이면 천장을 통과해 "천장 없음"이 되고 지붕 윗면이 바닥으로 잡힌다)
            bool hitBackfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            try
            {
                result = DrawPoints(triangulation, cumulative, total, indoor);
            }
            finally
            {
                Physics.queriesHitBackfaces = hitBackfaces;
            }

            // 천장이 없는 실내 데모 씬(위가 열린 전시용 방 등)은 천장 조건 없이 다시 뽑는다.
            if (indoor && result.Count < FloorSampleCount / 20)
            {
                Debug.LogWarning("[MapSceneBuilder] 천장이 있는 바닥이 거의 없어 천장 조건 없이 배치합니다.");
                result = DrawPoints(triangulation, cumulative, total, false);
            }

            return KeepLargestConnectedRegion(result);
        }

        /// <summary>
        /// 넓이 비례로 점을 뽑고, 벽에 너무 붙은 점과 (조건이 있으면) 천장이 없는 점을 버린다.
        /// </summary>
        private static List<Vector3> DrawPoints(NavMeshTriangulation triangulation, float[] cumulative, float total, bool requireCeiling)
        {
            var result = new List<Vector3>();
            var random = new System.Random(20260927);
            for (int n = 0; n < FloorSampleCount; n++)
            {
                float pick = (float)random.NextDouble() * total;
                int index = System.Array.BinarySearch(cumulative, pick);
                if (index < 0)
                    index = ~index;
                index = Mathf.Clamp(index, 0, cumulative.Length - 1);

                Vector3 point = RandomPointInTriangle(triangulation, index, random);
                if (NavMeshEdgeTooClose(point, SpawnEdgeClearance))
                    continue;
                if (requireCeiling && !Physics.Raycast(point + Vector3.up * 0.5f, Vector3.up, IndoorCeilingCheck))
                    continue;
                result.Add(point);
            }
            return result;
        }

        /// <summary>
        /// 삼각형 안의 무작위 점.
        /// </summary>
        private static Vector3 RandomPointInTriangle(NavMeshTriangulation triangulation, int triangle, System.Random random)
        {
            Vector3 a = triangulation.vertices[triangulation.indices[triangle * 3]];
            Vector3 b = triangulation.vertices[triangulation.indices[triangle * 3 + 1]];
            Vector3 c = triangulation.vertices[triangulation.indices[triangle * 3 + 2]];
            float r1 = Mathf.Sqrt((float)random.NextDouble());
            float r2 = (float)random.NextDouble();
            return (1f - r1) * a + r1 * (1f - r2) * b + r1 * r2 * c;
        }

        /// <summary>
        /// 경로가 이어지는 점이 가장 많은 영역만 남긴다.
        /// </summary>
        private static List<Vector3> KeepLargestConnectedRegion(List<Vector3> points)
        {
            if (points.Count < 2)
                return points;

            var path = new NavMeshPath();
            List<Vector3> best = points;
            int bestCount = -1;
            // 후보 기준점 몇 개로 각각 도달 가능한 점을 세고, 가장 많은 쪽을 고른다.
            int seedStep = Mathf.Max(1, points.Count / 12);
            for (int s = 0; s < points.Count; s += seedStep)
            {
                var reachable = new List<Vector3>();
                foreach (Vector3 point in points)
                {
                    if (NavMesh.CalculatePath(points[s], point, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                        reachable.Add(point);
                }
                if (reachable.Count > bestCount)
                {
                    bestCount = reachable.Count;
                    best = reachable;
                }
                if (bestCount > points.Count / 2)
                    break;
            }
            return best;
        }

        /// <summary>
        /// 서로 최대한 멀리 떨어진 점을 개수만큼 고른다. (가장 먼 점 샘플링)
        /// 가장자리 여유가 부족한 점은 제외한다.
        /// </summary>
        internal static List<Vector3> PickSpread(List<Vector3> points, int count, float edgeClearance)
        {
            List<Vector3> candidates = points
                .Where(p => !NavMeshEdgeTooClose(p, edgeClearance))
                .ToList();
            if (candidates.Count == 0)
                candidates = new List<Vector3>(points);

            var chosen = new List<Vector3>();
            if (candidates.Count == 0 || count <= 0)
                return chosen;

            chosen.Add(candidates[candidates.Count / 2]);
            while (chosen.Count < count && chosen.Count < candidates.Count)
            {
                Vector3 farthest = candidates[0];
                float farthestDistance = -1f;
                foreach (Vector3 candidate in candidates)
                {
                    float nearest = chosen.Min(c => (c - candidate).sqrMagnitude);
                    if (nearest > farthestDistance)
                    {
                        farthestDistance = nearest;
                        farthest = candidate;
                    }
                }
                chosen.Add(farthest);
            }
            return chosen;
        }

        /// <summary>
        /// NavMesh 가장자리(벽)까지의 거리가 기준보다 가까운지 확인한다.
        /// </summary>
        private static bool NavMeshEdgeTooClose(Vector3 point, float clearance)
        {
            return NavMesh.FindClosestEdge(point, out NavMeshHit edge, NavMesh.AllAreas) && edge.distance < clearance;
        }

        /// <summary>
        /// 바닥 점의 범위로 플레이 영역을 정한다. 점이 없으면 환경 전체 범위를 쓴다.
        /// </summary>
        private static Bounds GetPlayArea(List<Vector3> floor, Transform environment)
        {
            if (floor.Count == 0)
                return CharacterAssetBuilder.GetRendererBounds(environment.gameObject);

            var bounds = new Bounds(floor[0], Vector3.zero);
            foreach (Vector3 point in floor)
                bounds.Encapsulate(point);
            bounds.Expand(new Vector3(4f, 0f, 4f));
            return bounds;
        }

        /// <summary>
        /// 점마다 오브젝트를 만들고 플레이 영역 중심을 바라보게 한다.
        /// </summary>
        internal static void CreatePoints(Transform mapRoot, string groupName, List<Vector3> positions, Vector3 center,
            System.Action<Transform, Vector3, Quaternion> create)
        {
            Transform group = new GameObject(groupName).transform;
            group.SetParent(mapRoot, false);
            foreach (Vector3 position in positions)
            {
                Vector3 toCenter = center - position;
                toCenter.y = 0f;
                Quaternion rotation = toCenter.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toCenter) : Quaternion.identity;
                create(group, position, rotation);
            }
        }

        /// <summary>
        /// 컴포넌트 하나를 가진 표식 오브젝트를 만든다.
        /// </summary>
        internal static void CreateMarker<T>(Transform parent, string name, Vector3 position, Quaternion rotation) where T : Component
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.SetPositionAndRotation(position, rotation);
            marker.AddComponent<T>();
        }

        #endregion

        #region 조명

        /// <summary>
        /// 모든 조명을 실시간으로 바꾸고 사냥 시간에 꺼지는 MapLight로 등록한다.
        /// (다른 씬에서 옮겨 온 구운 조명은 라이트맵이 없어 빛을 내지 않으므로)
        /// 조명이 바닥 넓이에 비해 부족하면 천장 아래에 조명을 추가한다. 추가한 개수를 반환한다.
        /// </summary>
        private static int SetupLights(Transform mapRoot, Transform environment, List<Vector3> floor, bool indoor)
        {
            Transform lightsRoot = new GameObject("Lights").transform;
            lightsRoot.SetParent(mapRoot, false);

            Light[] existing = environment.GetComponentsInChildren<Light>(true);
            foreach (Light sceneLight in existing)
            {
                sceneLight.lightmapBakeType = LightmapBakeType.Realtime;
                if (existing.Length > ShadowLightLimit && sceneLight.type != LightType.Directional)
                    sceneLight.shadows = LightShadows.None;
                if (sceneLight.GetComponent<MapLight>() == null)
                    sceneLight.gameObject.AddComponent<MapLight>();
            }

            int wanted = Mathf.Max(4, Mathf.RoundToInt(floor.Count > 0 ? EstimateFloorArea(floor) / LampSpacingArea : 4f));
            int pointLights = existing.Count(l => l.type != LightType.Directional);
            int toAdd = Mathf.Max(0, wanted - pointLights);
            List<Vector3> lampSpots = PickSpread(floor, toAdd, 0f);
            foreach (Vector3 spot in lampSpots)
                CreateLamp(lightsRoot, spot, indoor);

            // 실외 맵에 방향광이 없으면 추가한다. (사냥 시간에 꺼짐)
            if (!indoor && !existing.Any(l => l.type == LightType.Directional))
            {
                var sun = new GameObject("Directional Light");
                sun.transform.SetParent(lightsRoot, false);
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                Light sunLight = sun.AddComponent<Light>();
                sunLight.type = LightType.Directional;
                sunLight.shadows = LightShadows.Soft;
                sun.AddComponent<MapLight>();
            }

            return lampSpots.Count;
        }

        /// <summary>
        /// 바닥 점이 덮는 대략적인 넓이. (2m 격자 칸 수)
        /// </summary>
        internal static float EstimateFloorArea(List<Vector3> floor)
        {
            var cells = new HashSet<Vector2Int>();
            foreach (Vector3 point in floor)
                cells.Add(new Vector2Int(Mathf.FloorToInt(point.x / 2f), Mathf.FloorToInt(point.z / 2f)));
            return cells.Count * 4f;
        }

        /// <summary>
        /// 천장 바로 아래(천장이 없으면 3m 높이)에 포인트 조명을 만든다.
        /// </summary>
        private static void CreateLamp(Transform parent, Vector3 floorPoint, bool indoor)
        {
            float height = 3f;
            if (Physics.Raycast(floorPoint + Vector3.up * 0.5f, Vector3.up, out RaycastHit hit, IndoorCeilingCheck))
                height = Mathf.Max(1.8f, hit.distance + 0.5f - 0.3f);
            else if (indoor)
                height = 2.7f;

            var lamp = new GameObject("MapLamp");
            lamp.transform.SetParent(parent, false);
            lamp.transform.position = floorPoint + Vector3.up * height;

            Light point = lamp.AddComponent<Light>();
            point.type = LightType.Point;
            point.range = 10f;
            point.intensity = 2.5f;
            point.color = new Color(1f, 0.92f, 0.8f);
            point.shadows = LightShadows.None;
            lamp.AddComponent<MapLight>();
        }

        #endregion

        #region 경계

        /// <summary>
        /// 플레이 영역을 둘러싸는 투명 벽과 천장(충돌체만)을 만든다. 맵 루트 밖에 두어 NavMesh와 변신 대상에서 제외한다.
        /// 맵 정보에 쓸 천장 높이(바닥에서 투명 천장까지)를 반환한다.
        /// </summary>
        internal static float CreateBoundary(Bounds playArea, Transform environment)
        {
            Bounds environmentBounds = CharacterAssetBuilder.GetRendererBounds(environment.gameObject);
            float floorY = playArea.min.y;
            float top = Mathf.Min(environmentBounds.max.y + 1f, floorY + GameConstants.GunRange - 10f);
            top = Mathf.Max(top, playArea.max.y + 4f);

            Transform boundary = new GameObject("Boundary (Invisible)").transform;
            float height = top - floorY + 10f;
            float centerY = floorY - 5f + height * 0.5f;
            Vector3 center = playArea.center;
            Vector3 size = playArea.size + new Vector3(2f, 0f, 2f);

            CreateInvisibleBox(boundary, "Ceiling", new Vector3(center.x, top + 0.25f, center.z), new Vector3(size.x, 0.5f, size.z));
            CreateInvisibleBox(boundary, "Wall_North", new Vector3(center.x, centerY, center.z + size.z * 0.5f), new Vector3(size.x, height, 0.5f));
            CreateInvisibleBox(boundary, "Wall_South", new Vector3(center.x, centerY, center.z - size.z * 0.5f), new Vector3(size.x, height, 0.5f));
            CreateInvisibleBox(boundary, "Wall_East", new Vector3(center.x + size.x * 0.5f, centerY, center.z), new Vector3(0.5f, height, size.z));
            CreateInvisibleBox(boundary, "Wall_West", new Vector3(center.x - size.x * 0.5f, centerY, center.z), new Vector3(0.5f, height, size.z));
            return top - floorY;
        }

        /// <summary>
        /// 보이지 않는 상자 충돌체를 만든다.
        /// </summary>
        private static void CreateInvisibleBox(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var box = new GameObject(name);
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.isStatic = true;
            box.AddComponent<BoxCollider>().size = size;
        }

        #endregion
    }
}
