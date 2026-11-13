using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuardVsMannequin.Core;
using GuardVsMannequin.Map;
using GuardVsMannequin.Objectives;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static GuardVsMannequin.Editor.MapAssets;

namespace GuardVsMannequin.Editor
{
    using Gap = MapWallGap;

    /// <summary>밤의 도심 사거리 "도시 길거리" 맵을 항상 같은 결과로 만든다.</summary>
    public static class CityStreetBuilder
    {
        private const string MapName = "City Street";
        private const string SceneName = "Map_CityStreet";
        internal const string PreviewFolder = "Temp/CityStreet";

        private const float MinX = -32f, MaxX = 32f, MinZ = -20f, MaxZ = 20f;

        /// <summary>두 길 모두에 쓰는 차도 절반 폭.</summary>
        private const float RoadHalf = 4f;
        private const float Facade = 8f;
        /// <summary>보도·블록 바닥 높이(연석 높이).</summary>
        private const float Walk = 0.15f;
        /// <summary>가게 천장 높이.</summary>
        private const float ShopCeiling = Walk + 3.5f;
        private const float WallThickness = 0.25f;

        // 공사장 콘크리트 판 (2층 높이)
        private const float SlabTop = Walk + 3.5f;

        private static readonly Vector3[] MannequinSpawns =
        {
            new Vector3(-14f, Walk, 10f), new Vector3(-10f, Walk, 13.5f), new Vector3(-12f, Walk, 18f), new Vector3(-18f, Walk, 11f),
            new Vector3(-18f, Walk, 17f), new Vector3(10f, Walk, 10f), new Vector3(16f, Walk, 14f), new Vector3(12f, Walk, 18f),
            new Vector3(-25f, Walk, 6f), new Vector3(6f, Walk, 12f)
        };

        private static readonly Vector3[] GuardSpawns =
        {
            new Vector3(-25f, Walk, -11f), new Vector3(-14f, Walk, -12f), new Vector3(12f, Walk, -11f),
            new Vector3(24f, Walk, -10f), new Vector3(-27f, Walk, -5.5f)
        };

        private static readonly Vector3[] GeneratorSpots =
        {
            new Vector3(-18f, Walk, 15f), new Vector3(-10f, Walk, 18.5f), new Vector3(17f, Walk, 18f), new Vector3(9.5f, Walk, 14.5f),
            new Vector3(-27f, Walk, 7f), new Vector3(28f, Walk, 7f), new Vector3(-30f, Walk, -10f), new Vector3(-11.6f, Walk, -12f),
            new Vector3(14f, Walk, -14f), new Vector3(28f, Walk, -10f), new Vector3(27f, SlabTop, -17f),
            new Vector3(0f, 0f, 16f), new Vector3(0f, 0f, -16f), new Vector3(-24f, 0f, 2f)
        };

        /// <summary>
        /// 만드는 동안 함께 쓰는 상태.
        /// </summary>
        private sealed class Context : MapLayoutTools
        {
            public CityMaterials materials;
            public Transform lights;
            /// <summary>배치도(위에서 찍기)에서 숨길 지붕·천장.</summary>
            public readonly List<Renderer> roofs = new List<Renderer>();

            /// <summary>
            /// 지붕이 있으면 위에서 찍은 배치도에 가게 안이 보이지 않으므로, 지붕·천장을 만들 때 숨길 목록에 함께 넣는다.
            /// </summary>
            public GameObject Roof(Transform parent, string name, Vector3 center, Vector3 size, Material material)
            {
                GameObject roof = Box(parent, name, center, size, material);
                roofs.Add(roof.GetComponent<Renderer>());
                return roof;
            }
        }

        #region 메뉴

        /// <summary>씬을 덮어쓰므로 작업 중인 씬을 잃지 않도록 저장 여부를 먼저 묻고, 배치 확인용 사진까지 남긴다.</summary>
        [MenuItem(EditorMenus.Root + "맵 생성/도시 길거리 (Map_CityStreet)")]
        private static void BuildFromMenu()
        {
            // 저장하면 지운 씬이 되살아나므로 파일이 없으면 묻지 않는다.
            Scene active = SceneManager.GetActiveScene();
            bool fileExists = !string.IsNullOrEmpty(active.path) && File.Exists(active.path);
            if (fileExists && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            BuildScene($"{MapCatalog.CustomMapFolder}/{SceneName}.unity", capturePreview: true);
            MapRegistrySync.Sync();
        }

        #endregion

        /// <summary>손으로 고친 배치와 섞이지 않도록 빈 씬부터 새로 만든다.</summary>
        /// <param name="capturePreview">true면 Temp/CityStreet에 배치도·장면 사진을 남긴다.</param>
        public static void BuildScene(string scenePath, bool capturePreview)
        {
            // 빌더는 프리팹을 놓으므로 빠진 맵 물체 프리팹을 먼저 만든다.
            MapObjectPrefabBuilder.EnsurePrefabs();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Transform mapRoot = new GameObject(MapAuthoringTools.MapRootName).transform;

            CityMaterials materials = CityMaterials.LoadOrCreate();
            var context = new Context
            {
                materials = materials,
                placer = new MapPiecePlacer(null),
                environment = MapLayoutTools.CreateChild(mapRoot, MapAuthoringTools.EnvironmentName),
                lights = MapLayoutTools.CreateChild(mapRoot, "Lights")
            };

            BuildGround(context);
            BuildRoadMarkings(context);
            BuildBackdrop(context);
            BuildNorthWestBlock(context);
            BuildNorthEastBlock(context);
            BuildParkingLot(context);
            BuildConstructionSite(context);
            FurnishStreets(context);
            FurnishShops(context);

            // 정리가 등록 컴포넌트를 지우지 않도록 변신 사물 등록을 마지막에 한다.
            MapSceneBuilder.CleanEnvironment(context.environment);
            MapLayoutTools.ReplaceMaterials(context.environment, materials.PropRemap);
            int converted = UrpMaterialConverter.ConvertRenderers(context.environment.GetComponentsInChildren<Renderer>(true));
            int colliders = MapSceneBuilder.AddMissingColliders(context.environment);
            context.placer.RegisterCandidates(out int propCount, out int skipped);
            Physics.SyncTransforms();

            BuildShopLights(context);
            BuildOutskirts(context, MapLayoutTools.CreateChild(mapRoot, "Outskirts"));
            CreatePoints(mapRoot);

            NavMeshSurface surface = mapRoot.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            var playArea = new Bounds(new Vector3(0f, 6f, 0f), new Vector3(MaxX - MinX, 12f, MaxZ - MinZ));
            float ceilingHeight = MapSceneBuilder.CreateBoundary(playArea, context.environment);

            MapInfo mapInfo = mapRoot.gameObject.AddComponent<MapInfo>();
            var info = new SerializedObject(mapInfo);
            info.FindProperty("mapName").stringValue = MapName;
            info.FindProperty("ceilingHeight").floatValue = ceilingHeight;
            info.ApplyModifiedPropertiesWithoutUndo();
            // 대기 화면: 남서쪽 주차장 위에서 사거리를 내려다본다.
            var overviewPosition = new Vector3(-30f, 13f, -18f);
            GameSystemsSceneBuilder.SetOverview(mapInfo, overviewPosition, Quaternion.LookRotation(new Vector3(0f, 0f, 4f) - overviewPosition));

            ApplyAtmosphere(scene, materials.skybox);

            // 최적화(오클루전 굽기)에 저장된 경로가 필요하므로 먼저 저장한다.
            EditorSceneManager.SaveScene(scene, scenePath);
            MapOptimizer.Optimize(scene, indoor: false);
            if (capturePreview)
                CapturePreviews(context, playArea);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            WriteReport(context, converted, colliders, propCount, skipped);
            Debug.Log($"[CityStreetBuilder] 도시 길거리 완료: {scenePath}\n" +
                      $"변신 사물 {propCount}개(크기·구성으로 제외 {skipped}개), 충돌체 추가 {colliders}개, 머티리얼 변환 {converted}개, " +
                      $"없는 에셋 {context.placer.MissingCount}개. 에셋 크기 목록: {PreviewFolder}/sizes.txt");
        }

        #region 바닥·도로

        /// <summary>길거리처럼 보이도록 차도와 보도 사이에 실제 높이 차와 연석을 둔다.</summary>
        private static void BuildGround(Context context)
        {
            Transform group = context.Group("Ground");
            CityMaterials materials = context.materials;

            MapLayoutTools.Box(group, "Road", new Vector3(0f, -0.15f, 0f), new Vector3(MaxX - MinX, 0.3f, MaxZ - MinZ), materials.asphalt);
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    var center = new Vector3(sx * (RoadHalf + MaxX) / 2f, Walk / 2f, sz * (RoadHalf + MaxZ) / 2f);
                    MapLayoutTools.Box(group, "Sidewalk", center, new Vector3(MaxX - RoadHalf, Walk, MaxZ - RoadHalf), materials.sidewalk);
                    // 연석.
                    MapLayoutTools.Box(group, "Curb", new Vector3(center.x, Walk / 2f + 0.005f, sz * (RoadHalf + 0.1f)),
                        new Vector3(MaxX - RoadHalf, Walk + 0.01f, 0.2f), materials.curb);
                    MapLayoutTools.Box(group, "Curb", new Vector3(sx * (RoadHalf + 0.1f), Walk / 2f + 0.005f, center.z),
                        new Vector3(0.2f, Walk + 0.01f, MaxZ - RoadHalf), materials.curb);
                }
            }
        }

        /// <summary>텅 빈 아스팔트는 도로로 읽히지 않아 걸리지 않는 얇은 판으로 노면 표시를 그린다.</summary>
        private static void BuildRoadMarkings(Context context)
        {
            Transform group = context.Group("RoadMarkings");
            CityMaterials materials = context.materials;
            const float y = 0.006f;
            const float crossEnd = RoadHalf + 3.5f;

            for (float along = MinX + 1f; along < MaxX - 1f; along += 3f)
            {
                if (Mathf.Abs(along) > crossEnd)
                    MapLayoutTools.Box(group, "CenterLine", new Vector3(along, y, 0f), new Vector3(1.6f, 0.012f, 0.15f), materials.lineYellow, collider: false);
            }
            for (float along = MinZ + 1f; along < MaxZ - 1f; along += 3f)
            {
                if (Mathf.Abs(along) > crossEnd)
                    MapLayoutTools.Box(group, "CenterLine", new Vector3(0f, y, along), new Vector3(0.15f, 0.012f, 1.6f), materials.lineYellow, collider: false);
            }

            foreach (float side in new[] { -1f, 1f })
            {
                float middle = side * (RoadHalf + 1.75f);
                for (float across = -RoadHalf + 0.5f; across <= RoadHalf - 0.49f; across += 1f)
                {
                    // 큰길과 골목길을 건너는 횡단보도.
                    MapLayoutTools.Box(group, "Crosswalk", new Vector3(middle, y, across), new Vector3(2.5f, 0.012f, 0.5f), materials.lineWhite, collider: false);
                    MapLayoutTools.Box(group, "Crosswalk", new Vector3(across, y, middle), new Vector3(0.5f, 0.012f, 2.5f), materials.lineWhite, collider: false);
                }
                // 정지선: 횡단보도 바깥, 차선 한쪽(진입 차선)만
                float stop = side * (RoadHalf + 3.9f);
                MapLayoutTools.Box(group, "StopLine", new Vector3(stop, y, side * RoadHalf / 2f), new Vector3(0.3f, 0.012f, RoadHalf), materials.lineWhite, collider: false);
                MapLayoutTools.Box(group, "StopLine", new Vector3(-side * RoadHalf / 2f, y, stop), new Vector3(RoadHalf, 0.012f, 0.3f), materials.lineWhite, collider: false);
            }

            foreach (Vector2 p in new[] { new Vector2(-14f, 1.6f), new Vector2(1.6f, -13f), new Vector2(20f, -2f) })
            {
                GameObject manhole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                manhole.name = "Manhole";
                manhole.transform.SetParent(group, false);
                manhole.transform.position = new Vector3(p.x, y, p.y);
                manhole.transform.localScale = new Vector3(0.8f, 0.006f, 0.8f);
                manhole.GetComponent<Renderer>().sharedMaterial = materials.manhole;
                Object.DestroyImmediate(manhole.GetComponent<Collider>());
            }
        }

        /// <summary>도시가 영역 끝에서 끊겨 보이지 않도록 외곽 벽을 투명하게 두어 영역 밖 거리와 하늘이 보이게 한다.</summary>
        private static void BuildBackdrop(Context context)
        {
            Transform group = context.Group("Backdrop");
            const float height = 16f, depth = 1.5f;

            foreach (float side in new[] { -1f, 1f })
            {
                InvisibleWall(group, "OuterWallX", new Vector3(side * (MaxX + depth / 2f), height / 2f, 0f), new Vector3(depth, height, MaxZ - MinZ + depth * 2f));
                InvisibleWall(group, "OuterWallZ", new Vector3(0f, height / 2f, side * (MaxZ + depth / 2f)), new Vector3(MaxX - MinX, height, depth));
            }
        }

        /// <summary>막기만 하면 되므로 렌더러 없이 상자 충돌체만 정적으로 둔다.</summary>
        private static void InvisibleWall(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var wall = new GameObject(name);
            wall.transform.SetParent(parent, false);
            wall.transform.position = center;
            wall.isStatic = true;
            wall.AddComponent<BoxCollider>().size = size;
        }

        #endregion

        #region 영역 밖 거리 (보이기만 함)

        /// <summary>안개가 끝을 덮는 영역 밖 거리의 끝(맵 중심에서의 거리).</summary>
        private const float OutskirtsReach = 100f;
        /// <summary>영역 밖 건물 깊이와, 외곽 벽에서 건물까지의 틈.</summary>
        private const float RowDepth = 14f, EdgeGap = 2f;

        /// <summary>갇힌 느낌이 들지 않도록 영역 밖에 보이기만 하는 도시를 맵 최적화 대상 밖에 만든다.</summary>
        private static void BuildOutskirts(Context context, Transform root)
        {
            CityMaterials materials = context.materials;
            var random = new System.Random(20261110);

            MapLayoutTools.Box(root, "OuterGround", new Vector3(0f, -0.17f, 0f), new Vector3(OutskirtsReach * 2f, 0.3f, OutskirtsReach * 2f), materials.outerGround, collider: false);
            foreach (float side in new[] { -1f, 1f })
            {
                StreetCorridor(context, root, random, alongX: true, side, MaxX);
                StreetCorridor(context, root, random, alongX: false, side, MaxZ);
            }

            // 모서리 건물은 영역 쪽을 본다.
            float rowEnd = Facade + RowDepth;
            float ringOuter = MaxX + EdgeGap + RowDepth;
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    float from = sx > 0f ? rowEnd : -ringOuter;
                    float to = sx > 0f ? ringOuter : -rowEnd;
                    BuildingRow(root, materials, random, alongX: true, from, to, sz * (MaxZ + EdgeGap), sz * (MaxZ + EdgeGap + RowDepth));
                }
            }

            MapLayoutTools.ReplaceMaterials(root, materials.PropRemap);
            UrpMaterialConverter.ConvertRenderers(root.GetComponentsInChildren<Renderer>(true));
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(node.gameObject, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>길이 경계에서 끊기지 않도록 영역 밖으로 도로를 이어 보이게 하되 실시간 조명은 늘리지 않는다.</summary>
        private static void StreetCorridor(Context context, Transform root, System.Random random, bool alongX, float side, float edge)
        {
            CityMaterials materials = context.materials;
            float start = edge;   // 영역 안 차도·보도와 겹치면 같은 높이 면이 깜빡이므로 끝에서 바로 잇는다.
            float length = OutskirtsReach - start;
            float mid = side * (start + OutskirtsReach) / 2f;
            // 길 방향·가로 방향 좌표를 월드 좌표로 바꾼다.
            Vector3 ToWorldPoint(float along, float y, float across) => alongX ? new Vector3(along, y, across) : new Vector3(across, y, along);
            Vector3 ToWorldSize(float alongSize, float height, float acrossSize) => alongX ? new Vector3(alongSize, height, acrossSize) : new Vector3(acrossSize, height, alongSize);

            MapLayoutTools.Box(root, "Road", ToWorldPoint(mid, -0.15f, 0f), ToWorldSize(length, 0.3f, RoadHalf * 2f), materials.asphalt, collider: false);
            foreach (float across in new[] { -1f, 1f })
            {
                float walkCenter = across * (RoadHalf + Facade) / 2f;
                MapLayoutTools.Box(root, "Sidewalk", ToWorldPoint(mid, Walk / 2f, walkCenter), ToWorldSize(length, Walk, Facade - RoadHalf), materials.sidewalk, collider: false);
                MapLayoutTools.Box(root, "Curb", ToWorldPoint(mid, Walk / 2f + 0.005f, across * (RoadHalf + 0.1f)), ToWorldSize(length, Walk + 0.01f, 0.2f), materials.curb, collider: false);

                float rowFrom = side > 0f ? edge + EdgeGap : -OutskirtsReach;
                float rowTo = side > 0f ? OutskirtsReach : -(edge + EdgeGap);
                BuildingRow(root, materials, random, alongX, rowFrom, rowTo, across * Facade, across * (Facade + RowDepth));
            }
            for (float along = start + 1.5f; along < OutskirtsReach; along += 3f)
                MapLayoutTools.Box(root, "CenterLine", ToWorldPoint(side * along, 0.006f, 0f), ToWorldSize(1.6f, 0.012f, 0.15f), materials.lineYellow, collider: false);

            // 실시간 조명을 늘리지 않도록 가로등은 빛을 내지 않고 등만 빛난다.
            for (float along = start + 6f; along < OutskirtsReach - 4f; along += 15f)
            {
                foreach (float across in new[] { -1f, 1f })
                {
                    float yaw = alongX ? (across > 0f ? 180f : 0f) : (across > 0f ? -90f : 90f);
                    Vector3 pole = ToWorldPoint(side * along, Walk, across * (RoadHalf + 0.6f));
                    GameObject lamp = context.placer.Place(City.StreetLamp, root, pole, yaw, name: "FarStreetLamp");
                    if (lamp == null)
                        continue;
                    lamp.transform.position = new Vector3(pole.x, lamp.transform.position.y, pole.z);
                    Vector3 head = pole + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 1.15f + Vector3.up * 4.96f;
                    MapLayoutTools.Box(root, "FarLampGlow", head, ToWorldSize(alongX ? 0.28f : 0.5f, 0.03f, alongX ? 0.5f : 0.28f), materials.lampOn, collider: false);
                }
            }

            // 길가에 주차된 차 몇 대
            for (float along = start + 10f; along < OutskirtsReach - 8f; along += 9f)
            {
                if (random.NextDouble() > 0.45)
                    continue;
                float across = random.NextDouble() < 0.5 ? -1f : 1f;
                float yaw = (alongX ? 0f : 90f) + (across > 0f ? 0f : 180f);
                context.placer.Place(City.Sedan, root, ToWorldPoint(side * along, 0f, across * (RoadHalf - 1.1f)), yaw, name: "FarParkedCar");
            }
        }

        /// <summary>영역 밖 배경이라 상자로 가볍게 채우되, 반복이 티 나지 않게 크기를 무작위로 이어 붙인다.</summary>
        private static void BuildingRow(Transform parent, CityMaterials materials, System.Random random, bool alongX, float from, float to, float front, float back)
        {
            Material[] walls = { materials.brick, materials.concreteWall, materials.plaster, materials.backdrop };
            Material[] signs = { materials.signRed, materials.signBlue, materials.signGreen, materials.signWarm, materials.signDark };
            float outward = Mathf.Sign(front - back);
            float depth = Mathf.Abs(back - front);
            float acrossCenter = (front + back) / 2f;
            // 길 방향·가로 방향 좌표를 월드 좌표로 바꾼다.
            Vector3 ToWorldPoint(float along, float y, float across) => alongX ? new Vector3(along, y, across) : new Vector3(across, y, along);
            Vector3 ToWorldSize(float alongSize, float height, float acrossSize) => alongX ? new Vector3(alongSize, height, acrossSize) : new Vector3(acrossSize, height, alongSize);

            float cursor = from;
            while (to - cursor > 1f)
            {
                float width = Mathf.Min(to - cursor, Range(random, 5f, 11f));
                float height = Range(random, 7f, 24f);
                float center = cursor + width / 2f;
                MapLayoutTools.Box(parent, "FarBuilding", ToWorldPoint(center, height / 2f, acrossCenter), ToWorldSize(width - 0.1f, height, depth), walls[random.Next(walls.Length)], collider: false);

                for (float along = cursor + 1.4f; along <= cursor + width - 1.2f; along += 2.6f)
                {
                    for (float y = 4.2f; y + 1.4f <= height - 0.6f; y += 3.2f)
                    {
                        if (random.NextDouble() < 0.35)
                            MapLayoutTools.Box(parent, "FarWindow", ToWorldPoint(along, y + 0.7f, front + outward * 0.03f), ToWorldSize(1.2f, 1.4f, 0.04f),
                                random.NextDouble() < 0.8 ? materials.windowLit : materials.glowWhite, collider: false);
                    }
                }
                if (random.NextDouble() < 0.5)
                    MapLayoutTools.Box(parent, "FarSign", ToWorldPoint(center, 3.4f, front + outward * 0.1f), ToWorldSize(width * 0.7f, 0.7f, 0.12f), signs[random.Next(signs.Length)], collider: false);
                cursor += width;
            }
        }

        /// <summary>다시 만들어도 같은 배경이 나오도록 시드를 고정한 System.Random으로 범위 난수를 만든다.</summary>
        private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        #endregion

        #region 북쪽 블록: 상가·뒷골목·카페·편의점
        /// <summary>북서 블록의 상가·뒷골목·카페 건물을 만든다.</summary>
        private static void BuildNorthWestBlock(Context context)
        {
            Transform group = context.Group("NW_Block");
            CityMaterials materials = context.materials;

            // 닫힌 상가 (높이 12m)
            SolidBuilding(group, materials, materials.brick, -32f, -20f, Facade, MaxZ, 12f);
            Shutter(group, materials, alongX: true, Facade, -1f, -29f, 4f);
            Shutter(group, materials, alongX: true, Facade, -1f, -23f, 4f);
            ShopSign(group, alongX: true, Facade, -1f, -29f, 4.5f, materials.signRed);
            ShopSign(group, alongX: true, Facade, -1f, -23f, 4.5f, materials.signDark);
            FacadeWindows(group, materials, alongX: true, Facade, -1f, -32f, -20f, 5.2f, 12f, seed: 1);
            FacadeWindows(group, materials, alongX: false, -20f, 1f, Facade, MaxZ, 4f, 12f, seed: 2);

            // 뒷골목 바닥 (어두운 아스팔트)
            MapLayoutTools.Box(group, "AlleyFloor", new Vector3(-18f, Walk + 0.005f, (Facade + MaxZ) / 2f), new Vector3(4f, 0.01f, MaxZ - Facade), materials.alley, collider: false);
            // 뒷골목 막다른 끝 벽돌담 (3m)
            MapLayoutTools.Box(group, "AlleyEndWall", new Vector3(-18f, Walk + 1.5f, MaxZ - WallThickness / 2f), new Vector3(4f, 3f, WallThickness), materials.brick);
            foreach (float z in new[] { 10f, 13.5f, 17f })
                MapLayoutTools.Box(group, "WallPipe", new Vector3(-19.82f, 6f, z), new Vector3(0.12f, 12f, 0.12f), materials.pipe, collider: false);

            // 카페 홀과 주방.
            const float x0 = -16f, x1 = -8f, hallBack = 15f;
            WallRun(context, group, alongX: true, Facade, x0, x1, materials.cafeWall,
                new Gap(-13.6f, 4.2f, 0.5f, 2.5f, true), new Gap(-10.2f, 1.8f, 0f, 2.6f, false));
            WallRun(context, group, alongX: false, x0, Facade, MaxZ, materials.cafeWall, Gap.Door(13f, 1.2f));
            WallRun(context, group, alongX: false, x1, Facade, MaxZ, materials.cafeWall, new Gap(11.5f, 4f, 0.5f, 2.5f, true));
            WallRun(context, group, alongX: true, hallBack, x0, x1, materials.cafeWall, Gap.Door(-14.3f, 1.2f));
            WallRun(context, group, alongX: true, MaxZ, x0, x1, materials.cafeWall);
            Interior(context, group, materials, materials.cafeFloor, x0, x1, Facade, MaxZ, upperTop: 9f, materials.plaster);
            FacadeWindows(group, materials, alongX: true, Facade, -1f, x0, x1, 4.6f, 9f, seed: 3);
            Awning(group, materials.awningRed, alongX: true, Facade, -1f, x0 + 0.3f, x1 - 0.3f, 3.3f);
            ShopSign(group, alongX: true, Facade, -1f, (x0 + x1) / 2f, 4.15f, materials.signWarm, width: 5f);
        }

        /// <summary>북동 블록의 편의점과 닫힌 상가를 만든다.</summary>
        private static void BuildNorthEastBlock(Context context)
        {
            Transform group = context.Group("NE_Block");
            CityMaterials materials = context.materials;

            const float x0 = 8f, x1 = 20f, storeBack = 16f;
            WallRun(context, group, alongX: true, Facade, x0, x1, materials.storeWall,
                new Gap(10.6f, 4f, 0.4f, 2.7f, true), new Gap(14.3f, 1.8f, 0f, 2.6f, false), new Gap(17.7f, 3.4f, 0.4f, 2.7f, true));
            WallRun(context, group, alongX: false, x0, Facade, MaxZ, materials.storeWall, new Gap(11.8f, 5f, 0.4f, 2.7f, true));
            WallRun(context, group, alongX: false, x1, Facade, MaxZ, materials.storeWall);
            WallRun(context, group, alongX: true, storeBack, x0, x1, materials.storeWall, Gap.Door(18.6f, 1.2f));
            WallRun(context, group, alongX: true, MaxZ, x0, x1, materials.storeWall);
            Interior(context, group, materials, materials.storeFloor, x0, x1, Facade, MaxZ, upperTop: 10f, materials.concreteWall);
            FacadeWindows(group, materials, alongX: true, Facade, -1f, x0, x1, 4.8f, 10f, seed: 4);
            FacadeWindows(group, materials, alongX: false, x0, -1f, Facade, MaxZ, 4.8f, 10f, seed: 5);
            // 간판 띠: 초록·파랑 두 줄
            MapLayoutTools.Box(group, "StoreSignGreen", new Vector3((x0 + x1) / 2f, ShopCeiling + 0.35f, Facade - 0.2f), new Vector3(x1 - x0, 0.35f, 0.12f), materials.signGreen, collider: false);
            MapLayoutTools.Box(group, "StoreSignBlue", new Vector3((x0 + x1) / 2f, ShopCeiling + 0.75f, Facade - 0.2f), new Vector3(x1 - x0, 0.4f, 0.12f), materials.signBlue, collider: false);

            // 닫힌 상가 (높이 14m)
            SolidBuilding(group, materials, materials.concreteWall, x1, MaxX, Facade, MaxZ, 14f);
            Shutter(group, materials, alongX: true, Facade, -1f, 23.5f, 4f);
            Shutter(group, materials, alongX: true, Facade, -1f, 29f, 4f);
            ShopSign(group, alongX: true, Facade, -1f, 23.5f, 4.5f, materials.signBlue);
            ShopSign(group, alongX: true, Facade, -1f, 29f, 4.5f, materials.signDark);
            FacadeWindows(group, materials, alongX: true, Facade, -1f, x1, MaxX, 5.2f, 14f, seed: 6);
        }

        /// <summary>가게 안이 보도와 구별되고 위층이 비어 보이지 않도록 바닥·천장·위층 덩어리를 만든다.</summary>
        private static void Interior(Context context, Transform group, CityMaterials materials, Material floor, float x0, float x1, float z0, float z1,
            float upperTop, Material upperMaterial)
        {
            float half = WallThickness / 2f;
            MapLayoutTools.Box(group, "ShopFloor", new Vector3((x0 + x1) / 2f, Walk + 0.005f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.01f, z1 - z0), floor, collider: false);
            context.Roof(group, "ShopCeiling", new Vector3((x0 + x1) / 2f, ShopCeiling - 0.01f, (z0 + z1) / 2f), new Vector3(x1 - x0 - WallThickness, 0.02f, z1 - z0 - WallThickness), materials.ceiling);
            context.Roof(group, "UpperFloors", new Vector3((x0 + x1) / 2f, (ShopCeiling + upperTop) / 2f, (z0 + z1) / 2f),
                new Vector3(x1 - x0 + half * 2f, upperTop - ShopCeiling, z1 - z0 + half * 2f), upperMaterial);
        }

        #endregion

        #region 남쪽 블록: 주차장·공사장

        /// <summary>남서 주차장을 만든다.</summary>
        private static void BuildParkingLot(Context context)
        {
            Transform group = context.Group("SW_ParkingLot");
            CityMaterials materials = context.materials;
            const float x0 = MinX, x1 = -Facade, z0 = MinZ, z1 = -Facade;

            MapLayoutTools.Box(group, "ParkingFloor", new Vector3((x0 + x1) / 2f, Walk + 0.005f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.01f, z1 - z0), materials.parking, collider: false);
            // 낮은 담 (1m)
            LowWall(group, materials.concreteWall, alongX: true, z1, x0, -22f);
            LowWall(group, materials.concreteWall, alongX: true, z1, -16f, x1);
            LowWall(group, materials.concreteWall, alongX: false, x1, z0, -12f);
            LowWall(group, materials.concreteWall, alongX: false, x1, -10.5f, z1);
            LowWall(group, materials.concreteWall, alongX: true, z0 + WallThickness / 2f, x0, x1);
            LowWall(group, materials.concreteWall, alongX: false, x0 + WallThickness / 2f, z0, z1);

            // 주차 칸.
            const float stallWidth = 2.8f, stallDepth = 5f;
            float firstLine = x0 + 0.8f;
            for (int i = 0; i <= 8; i++)
            {
                float x = firstLine + stallWidth * i;
                MapLayoutTools.Box(group, "StallLine", new Vector3(x, Walk + 0.012f, z0 + stallDepth / 2f), new Vector3(0.12f, 0.01f, stallDepth), materials.lineWhite, collider: false);
            }
            for (int i = 0; i < 8; i++)
            {
                float x = firstLine + stallWidth * (i + 0.5f);
                context.Prop(group, City.WheelStop, x, z0 + 0.9f, 0f, 0.12f, baseY: Walk, transformable: false);
                if (i == 1 || i == 4 || i == 6)
                    continue;
                // 차가 뒤 담 쪽을 보도록 돌린다.
                context.Prop(group, City.Sedan, x, z0 + 3f, 90f, 1.45f, baseY: Walk, transformable: false);
            }
            context.Prop(group, City.Sedan, -26f, -11.5f, 15f, 1.45f, baseY: Walk, transformable: false);
            context.Prop(group, City.ParkingBooth, -19f, -10.4f, 180f, 2.5f, baseY: Walk);
            foreach (float x in new[] { -22.3f, -15.7f })
                context.Prop(group, City.Bollard, x, -7.7f, 0f, 0.9f, baseY: Walk);
            context.Against(group, City.UtilityBox, MapSide.East, x1, -15f, 1.4f, baseY: Walk);
            context.Prop(group, Kit.Cone, -21f, -12.5f, 0f, 0.6f, baseY: Walk);
            context.Prop(group, Kit.Cone, -20.2f, -13.2f, 0f, 0.6f, baseY: Walk);
            context.Prop(group, City.StreetBin, -30.6f, -9f, 0f, 1f, baseY: Walk);
            context.Prop(group, City.Scooter, -12.5f, -10f, -30f, 1.15f, baseY: Walk);
            context.Prop(group, City.TrashBags, -30.8f, -12.5f, 40f, 0.6f, baseY: Walk);
            context.Prop(group, Kit.BoxStack, -9.2f, -19.2f, 0f, 0.7f, baseY: Walk);

            // 주차장 가로등 2개 (차 쪽을 비춘다)
            foreach (float x in new[] { -27f, -13f })
                StreetLamp(context, group, x, -13.6f, 180f, Walk, new Color(0.85f, 0.92f, 1f), 3.2f, 13f);
        }

        /// <summary>남동 공사장을 만든다.</summary>
        private static void BuildConstructionSite(Context context)
        {
            Transform group = context.Group("SE_ConstructionSite");
            CityMaterials materials = context.materials;
            const float x0 = Facade, x1 = MaxX, z0 = MinZ, z1 = -Facade;

            MapLayoutTools.Box(group, "Dirt", new Vector3((x0 + x1) / 2f, Walk + 0.005f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.01f, z1 - z0), materials.dirt, collider: false);

            // 보도 쪽에 지지대가 보이지 않도록 가림막을 안쪽으로 둔다.
            for (float x = x0 + 1f; x < x1; x += 2f)
            {
                if (x > 14f && x < 18f)
                    continue;
                context.Prop(group, City.ConstructionFence, x, z1 - 0.3f, 0f, 2f, baseY: Walk, transformable: false);
            }
            for (float z = z1 - 1f; z > z0; z -= 2f)
            {
                if (z > -14f && z < -12f)
                    continue;
                context.Prop(group, City.ConstructionFence, x0 + 0.3f, z, -90f, 2f, baseY: Walk, transformable: false);
            }
            // 바깥쪽(남·동) 가림막: 지지대가 안쪽을 보게 돌린다.
            for (float x = x0 + 1f; x < x1; x += 2f)
                context.Prop(group, City.ConstructionFence, x, z0 + 0.3f, 180f, 2f, baseY: Walk, transformable: false);
            for (float z = z1 - 1f; z > z0; z -= 2f)
                context.Prop(group, City.ConstructionFence, x1 - 0.3f, z, 90f, 2f, baseY: Walk, transformable: false);

            // 반쯤 지은 콘크리트 판: x 20~30, z −19~−13, 윗면 3.65m, 기둥 6개, 서쪽 계단
            const float sx0 = 20f, sx1 = 30f, sz0 = -19f, sz1 = -13f;
            MapLayoutTools.Box(group, "SlabDeck", new Vector3((sx0 + sx1) / 2f, SlabTop - 0.15f, (sz0 + sz1) / 2f), new Vector3(sx1 - sx0, 0.3f, sz1 - sz0), materials.concrete);
            foreach (float x in new[] { sx0 + 0.3f, (sx0 + sx1) / 2f, sx1 - 0.3f })
            {
                foreach (float z in new[] { sz0 + 0.3f, sz1 - 0.3f })
                    MapLayoutTools.Box(group, "Column", new Vector3(x, (Walk + SlabTop - 0.3f) / 2f, z), new Vector3(0.4f, SlabTop - 0.3f - Walk, 0.4f), materials.concrete);
            }
            const float stairZ = -15.2f, stairWidth = 1.4f;
            float stairRun = Mathf.RoundToInt((SlabTop - Walk) / 0.25f) * 0.35f;
            MapLayoutTools.Stairs(group, materials.concrete, new Vector3(sx0 - stairRun, Walk, stairZ), Vector3.right, SlabTop - Walk, stairWidth);
            MapLayoutTools.RailingRun(group, materials.rail, alongX: true, sz1, sx0, sx1, SlabTop);
            MapLayoutTools.RailingRun(group, materials.rail, alongX: true, sz0, sx0, sx1, SlabTop);
            MapLayoutTools.RailingRun(group, materials.rail, alongX: false, sx1, sz0, sz1, SlabTop);
            MapLayoutTools.RailingRun(group, materials.rail, alongX: false, sx0, sz0, stairZ - stairWidth / 2f, SlabTop);
            MapLayoutTools.RailingRun(group, materials.rail, alongX: false, sx0, stairZ + stairWidth / 2f, sz1, SlabTop);

            // 현장 사무실 컨테이너.
            MapLayoutTools.Box(group, "SiteOffice", new Vector3(12.5f, Walk + 1.3f, -17.9f), new Vector3(6f, 2.6f, 2.4f), materials.container);
            MapLayoutTools.Box(group, "SiteOfficeDoor", new Vector3(14f, Walk + 1.05f, -16.68f), new Vector3(0.9f, 2f, 0.04f), materials.containerDoor, collider: false);
            MapLayoutTools.Box(group, "SiteOfficeWindow", new Vector3(11f, Walk + 1.6f, -16.68f), new Vector3(1.6f, 0.8f, 0.04f), materials.windowLit, collider: false);

            // 바닥 자재와 장비
            context.Prop(group, City.Dumpster, 18.2f, -18.6f, 0f, 1.3f, baseY: Walk);
            GameObject pallet = context.Prop(group, Kit.Pallet, 12f, -11.5f, 0f, 0.15f, baseY: Walk, transformable: false);
            context.Decorate(pallet, Kit.Crate, Vector2.zero, 10f, 1f);
            GameObject pallet2 = context.Prop(group, Kit.Pallet, 14.5f, -13.5f, 90f, 0.15f, baseY: Walk, transformable: false);
            context.Decorate(pallet2, Kit.BoxStack, Vector2.zero, 0f, 0.7f);
            float pipeYaw = MapLayoutTools.MeasureLengthYaw(Kit.Pipe);
            foreach ((float x, float z) in new[] { (10f, -15.5f), (10f, -15.1f), (10f, -14.7f) })
                context.Prop(group, Kit.Pipe, x, z, pipeYaw, 0.3f, baseY: Walk);
            context.Prop(group, Kit.Barrel, 21.5f, -10f, 0f, 0.9f, baseY: Walk);
            context.Prop(group, Kit.Barrel, 22.4f, -10.4f, 0f, 0.9f, baseY: Walk);
            context.Prop(group, Kit.Barrel, 30.4f, -10f, 0f, 0.9f, baseY: Walk);
            context.Prop(group, City.WaterBarrier, 27f, -11f, 0f, 0.8f, baseY: Walk);
            context.Prop(group, City.WaterBarrier, 25.5f, -11f, 0f, 0.8f, baseY: Walk);
            context.Prop(group, City.Barricade, 16f, -10.2f, 0f, 1.05f, baseY: Walk);
            context.Prop(group, Kit.Cone, 15f, -9.6f, 0f, 0.6f, baseY: Walk);
            context.Prop(group, Kit.Cone, 17f, -9.6f, 0f, 0.6f, baseY: Walk);
            context.Prop(group, Kit.SandbagWall, 30.5f, -16f, 90f, 0.6f, baseY: Walk, transformable: false);
            context.Prop(group, Kit.Crate, 30.6f, -11.8f, 15f, 1f, baseY: Walk);
            context.Prop(group, Kit.CrateSmall, 19f, -12f, 30f, 0.6f, baseY: Walk);

            // 판 위: 자재 더미·드럼통·고깔
            context.Prop(group, Kit.Crate, 28.5f, -18f, 0f, 1f, baseY: SlabTop);
            context.Prop(group, Kit.BoxStack, 26f, -14f, 90f, 0.7f, baseY: SlabTop);
            context.Prop(group, Kit.Barrel, 22f, -18.2f, 0f, 0.9f, baseY: SlabTop);
            context.Prop(group, Kit.Cone, 23.5f, -13.6f, 0f, 0.6f, baseY: SlabTop);
            context.Prop(group, City.CrateStack, 29f, -14.2f, 0f, 0.75f, baseY: SlabTop);

            // 공사장 작업등 (주황)
            StreetLamp(context, group, 26f, -9.2f, 180f, Walk, new Color(1f, 0.72f, 0.4f), 3f, 12f);
            StreetLamp(context, group, 19.5f, -12.2f, 0f, Walk, new Color(1f, 0.72f, 0.4f), 2.6f, 11f);
        }

        /// <summary>구역을 나누되 시야는 가리지 않도록 허리 높이 담을 만든다.</summary>
        private static void LowWall(Transform parent, Material material, bool alongX, float line, float from, float to)
        {
            float mid = (from + to) / 2f;
            Vector3 center = alongX ? new Vector3(mid, Walk + 0.5f, line) : new Vector3(line, Walk + 0.5f, mid);
            Vector3 size = alongX ? new Vector3(to - from, 1f, WallThickness) : new Vector3(WallThickness, 1f, to - from);
            MapLayoutTools.Box(parent, "LowWall", center, size, material);
        }

        #endregion

        #region 거리·가게 소품

        /// <summary>보도·차도의 거리 소품을 놓는다.</summary>
        private static void FurnishStreets(Context context)
        {
            Transform group = context.Group("Street_Props");
            var warm = new Color(1f, 0.82f, 0.58f);
            const float curbSide = RoadHalf + 0.6f;

            // 가로등은 팔이 차도 쪽을 보게 놓는다.
            foreach (float x in new[] { -26f, -19f, 11f, 25f })
                StreetLamp(context, group, x, curbSide, 180f, Walk, warm, 4.5f, 15f);
            foreach (float x in new[] { -26f, -13f, 13f, 25f })
                StreetLamp(context, group, x, -curbSide, 0f, Walk, warm, 4.5f, 15f);
            foreach (float z in new[] { -15f, 15f })
            {
                StreetLamp(context, group, -curbSide, z, 90f, Walk, warm, 4.5f, 15f);
                StreetLamp(context, group, curbSide, z, -90f, Walk, warm, 4.5f, 15f);
            }

            // 신호등: 네 모서리, 팔이 차도 위로
            PlaceAtPivot(context, group, City.TrafficLight, curbSide + 0.6f, curbSide + 0.6f, 180f);
            PlaceAtPivot(context, group, City.TrafficLight, -curbSide - 0.6f, -curbSide - 0.6f, 0f);
            PlaceAtPivot(context, group, City.TrafficLight, -curbSide - 0.6f, curbSide + 0.6f, 90f);
            PlaceAtPivot(context, group, City.TrafficLight, curbSide + 0.6f, -curbSide - 0.6f, -90f);

            // 사거리 모서리 볼라드 (횡단보도 끝)
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    context.Prop(group, City.Bollard, sx * (RoadHalf + 3.8f), sz * (RoadHalf + 0.45f), 0f, 0.9f, baseY: Walk);
                    context.Prop(group, City.Bollard, sx * (RoadHalf + 0.45f), sz * (RoadHalf + 3.8f), 0f, 0.9f, baseY: Walk);
                }
            }

            // 버스 정류장.
            context.Prop(group, City.BusStop, -20f, -6.5f, 0f, 2.6f, baseY: Walk, transformable: false);
            context.Prop(group, City.NewsBox, -17.4f, -6.9f, 0f, 1.1f, baseY: Walk);
            context.Prop(group, City.StreetBin, -22.6f, -4.9f, 0f, 1f, baseY: Walk);
            context.Prop(group, City.PhoneBooth, -29.5f, -6.4f, 0f, 2.3f, baseY: Walk);
            context.Prop(group, City.RoadSign, -15.5f, -4.7f, 180f, 2.8f, baseY: Walk);

            // 북쪽 보도
            context.Prop(group, City.FireHydrant, -22f, 4.6f, 0f, 0.8f, baseY: Walk);
            context.Prop(group, City.PlanterBox, -26f, 7.3f, 0f, 1.1f, baseY: Walk);
            context.Prop(group, City.PlanterBox, -31f, 7.3f, 0f, 1.1f, baseY: Walk);
            context.Prop(group, City.Mailbox, -6.8f, 7.3f, 180f, 1.3f, baseY: Walk);
            context.Prop(group, City.StreetBin, -4.9f, 11f, 0f, 1f, baseY: Walk);
            context.Prop(group, City.Bicycle, -8.8f, 6.9f, 0f, 1f, baseY: Walk);
            context.Prop(group, City.Bicycle, -6.6f, 13.5f, 85f, 1f, baseY: Walk);
            context.Prop(group, City.StreetBin, 8.9f, 4.8f, 0f, 1f, baseY: Walk);
            context.Prop(group, City.FireHydrant, 4.6f, 17f, 0f, 0.8f, baseY: Walk);
            context.Prop(group, City.PlanterBox, 26.5f, 7.3f, 0f, 1.1f, baseY: Walk);
            context.Prop(group, City.UtilityBox, 31f, 7.2f, 180f, 1.4f, baseY: Walk);
            context.Prop(group, City.PhoneBooth, 6.8f, 18.5f, -90f, 2.3f, baseY: Walk);

            // 남쪽·골목길 보도
            context.Prop(group, City.FireHydrant, 9.5f, -4.6f, 0f, 0.8f, baseY: Walk);
            context.Prop(group, City.Mailbox, 6.8f, -7.3f, 0f, 1.3f, baseY: Walk);
            context.Prop(group, City.UtilityBox, 7.3f, -16f, -90f, 1.4f, baseY: Walk);
            context.Prop(group, City.RoadSign, 4.7f, -10f, 90f, 2.8f, baseY: Walk);
            context.Prop(group, City.PlanterBox, -6.6f, -12f, 90f, 1.1f, baseY: Walk);
            context.Prop(group, City.StreetBin, -4.9f, -10f, 0f, 1f, baseY: Walk);
            context.Prop(group, City.Bicycle, -6.6f, -16.5f, 90f, 1f, baseY: Walk);
            context.Prop(group, City.ParkBench, 20f, -6.9f, 0f, 0.85f, baseY: Walk);
            context.Prop(group, City.StreetBin, 22f, -6.9f, 0f, 1f, baseY: Walk);
            context.Prop(group, City.PlanterBox, 29f, -7.2f, 0f, 1.1f, baseY: Walk);
            context.Prop(group, City.FireHydrant, -4.6f, -18f, 0f, 0.8f, baseY: Walk);

            // 차도: 버려진 차·오토바이, 고깔
            context.Prop(group, City.Sedan, -12f, -1.9f, 0f, 1.45f, transformable: false);
            context.Prop(group, City.Sedan, 1.8f, 12.5f, 95f, 1.45f, transformable: false);
            context.Prop(group, City.Scooter, 10f, 1.8f, 35f, 1.15f);
            context.Prop(group, Kit.Cone, -9f, -0.5f, 0f, 0.6f);
            context.Prop(group, Kit.Cone, -8.4f, -2.6f, 0f, 0.6f);
            context.Prop(group, City.TrashBags, -1.8f, -11f, 70f, 0.6f);

            // 도로 끝 차단.
            foreach (float side in new[] { -1f, 1f })
            {
                int i = 0;
                for (float across = -RoadHalf + 0.9f; across <= RoadHalf - 0.8f; across += 1.55f, i++)
                {
                    string barrier = i % 2 == 0 ? City.Barricade : City.WaterBarrier;
                    float typical = i % 2 == 0 ? 1.05f : 0.8f;
                    context.Prop(group, barrier, side * (MaxX - 2.5f), across, 90f, typical);
                    context.Prop(group, barrier, across, side * (MaxZ - 2.5f), 0f, typical);
                }
                context.Prop(group, Kit.Cone, side * (MaxX - 4f), 1.5f, 0f, 0.6f);
                context.Prop(group, Kit.Cone, 1.5f, side * (MaxZ - 4f), 0f, 0.6f);
            }
        }

        /// <summary>카페·편의점·뒷골목 안의 소품을 놓는다.</summary>
        private static void FurnishShops(Context context)
        {
            Transform group = context.Group("Shop_Props");

            // 카페 홀.
            foreach ((float x, float z) in new[] { (-14.3f, 10.2f), (-11.5f, 10.2f), (-14.3f, 12.9f), (-11.5f, 12.9f) })
                CafeSet(context, group, x, z, Walk);
            context.Against(group, City.Counter, MapSide.North, 15f, -10.5f, 1f, baseY: Walk);
            context.Against(group, Subway.Planter, MapSide.West, -16f, 9f, 1.2f, baseY: Walk);
            context.Against(group, City.CrateStack, MapSide.North, 15f, -8.75f, 0.75f, baseY: Walk);
            // 카페 바깥 자리
            CafeSet(context, group, -14.8f, 6.4f, Walk);
            context.Prop(group, City.Parasol, -14.8f, 6.4f, 0f, 2.4f, baseY: Walk);
            context.Prop(group, City.SignBoard, -11.6f, 6.6f, 0f, 0.95f, baseY: Walk);
            context.Prop(group, City.PlanterBox, -7.2f, 11.5f, 90f, 1.1f, baseY: Walk);

            // 카페 주방.
            context.Against(group, Kit.Shelf, MapSide.North, MaxZ, -13.5f, 1.9f, baseY: Walk);
            context.Against(group, Kit.Shelf, MapSide.North, MaxZ, -11.2f, 1.9f, baseY: Walk);
            context.Against(group, City.FridgeCase, MapSide.East, -8f, 18.5f, 2f, baseY: Walk);
            context.Against(group, Kit.Table, MapSide.West, -16f, 18.2f, 0.8f, baseY: Walk);
            context.Prop(group, City.CrateStack, -9.4f, 16f, 20f, 0.75f, baseY: Walk);
            context.Prop(group, Kit.BoxStack, -13f, 16.4f, 0f, 0.7f, baseY: Walk);
            context.Prop(group, Kit.Bucket, -15.2f, 16f, 0f, 0.3f, baseY: Walk);

            // 뒷골목 (x −20~−16)
            context.Against(group, City.Dumpster, MapSide.West, -20f, 12f, 1.3f, baseY: Walk, yaw: 90f);
            context.Prop(group, City.TrashBags, -17.2f, 10.8f, 0f, 0.6f, baseY: Walk);
            context.Prop(group, City.TrashBags, -18.5f, 18.8f, 140f, 0.6f, baseY: Walk);
            context.Against(group, City.ACUnit, MapSide.West, -20f, 15.4f, 0.65f, baseY: Walk);
            context.Against(group, City.ACUnit, MapSide.West, -20f, 16.6f, 0.65f, baseY: Walk);
            context.Against(group, City.ACUnit, MapSide.East, -16f, 16.5f, 0.65f, baseY: Walk);
            context.Prop(group, Kit.Barrel, -16.7f, 19.2f, 0f, 0.9f, baseY: Walk);
            context.Prop(group, City.CrateStack, -19.2f, 9.2f, 10f, 0.75f, baseY: Walk);
            context.Prop(group, Kit.Box, -16.8f, 9.4f, 25f, 0.35f, baseY: Walk);

            // 편의점 매장.
            foreach (float x in new[] { 9.2f, 10.5f, 11.8f, 13.1f, 14.4f })
                context.Against(group, City.FridgeCase, MapSide.North, 16f, x, 2f, baseY: Walk);
            foreach (float z in new[] { 10.8f, 13f })
            {
                context.Prop(group, City.ShopShelf, 10.6f, z, 0f, 1.5f, baseY: Walk);
                context.Prop(group, City.ShopShelf, 12.2f, z, 0f, 1.5f, baseY: Walk);
            }
            context.Prop(group, City.Counter, 17.8f, 11.4f, -90f, 1f, baseY: Walk);
            context.Against(group, Kit.Shelf, MapSide.East, 20f, 11.4f, 1.9f, baseY: Walk);
            context.Prop(group, City.CrateStack, 17f, 14.8f, 0f, 0.75f, baseY: Walk);
            context.Prop(group, Kit.BoxStack, 15.9f, 14.5f, 90f, 0.7f, baseY: Walk);
            context.Prop(group, Subway.WetFloorSign, 13.6f, 9.2f, 20f, 0.62f, baseY: Walk);

            // 편의점 창고 (z 16~20)
            context.Against(group, Kit.Shelf, MapSide.North, MaxZ, 9.8f, 1.9f, baseY: Walk);
            context.Against(group, Kit.Shelf, MapSide.North, MaxZ, 12.2f, 1.9f, baseY: Walk);
            GameObject pallet = context.Prop(group, Kit.Pallet, 15f, 18f, 0f, 0.15f, baseY: Walk, transformable: false);
            context.Decorate(pallet, Kit.BoxStack, Vector2.zero, 0f, 0.7f);
            context.Prop(group, City.CrateStack, 17.6f, 18.8f, 0f, 0.75f, baseY: Walk);
            context.Prop(group, City.CrateStack, 18.6f, 18.8f, 10f, 0.75f, baseY: Walk);
            context.Prop(group, Kit.Box, 9.2f, 16.8f, 15f, 0.35f, baseY: Walk);

            // 편의점 앞
            context.Against(group, VendingMachine, MapSide.North, Facade, 19.1f, 2f, baseY: Walk);
            context.Prop(group, City.Scooter, 16.4f, 6.4f, 0f, 1.15f, baseY: Walk);
            context.Prop(group, City.NewsBox, 12.6f, 7.4f, 180f, 1.1f, baseY: Walk);
            context.Prop(group, City.SignBoard, 13.2f, 6.2f, 0f, 0.95f, baseY: Walk);
            context.Prop(group, City.CrateStack, 8.8f, 7.4f, 0f, 0.75f, baseY: Walk);
        }

        /// <summary>카페 자리가 여러 곳이라 탁자와 마주 보는 의자 두 개를 한 묶음으로 놓는다.</summary>
        private static void CafeSet(Context context, Transform group, float x, float z, float baseY)
        {
            context.Prop(group, City.CafeTable, x, z, 0f, 0.75f, baseY: baseY);
            context.Prop(group, City.CafeChair, x, z - 0.62f, 0f, 0.85f, baseY: baseY);
            context.Prop(group, City.CafeChair, x, z + 0.62f, 180f, 0.85f, baseY: baseY);
        }

        #endregion

        #region 도형 도우미

        /// <summary>벽 높이를 매번 넘기지 않도록 고정하고, 건물이 뚫리지 않게 벽은 변신 대상으로 등록하지 않는다.</summary>
        private static void WallRun(Context context, Transform parent, bool alongX, float line, float from, float to, Material material, params Gap[] gaps)
            => MapLayoutTools.WallRun(parent, alongX, line, from, to, Walk, ShopCeiling, material, context.materials.glass, WallThickness, null, gaps);

        /// <summary>들어갈 수 없는 건물은 내부가 필요 없어 속이 꽉 찬 상자로 둔다.</summary>
        private static void SolidBuilding(Transform parent, CityMaterials materials, Material material, float x0, float x1, float z0, float z1, float height)
        {
            MapLayoutTools.Box(parent, "Building", new Vector3((x0 + x1) / 2f, (Walk + height) / 2f, (z0 + z1) / 2f), new Vector3(x1 - x0, height - Walk, z1 - z0), material);
            // 옥상 난간 띠
            MapLayoutTools.Box(parent, "RoofTrim", new Vector3((x0 + x1) / 2f, height + 0.15f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.3f, z1 - z0), materials.trim, collider: false);
        }

        /// <summary>민무늬 벽이 상자처럼 보이지 않도록 창 격자를 붙이고, 다시 만들어도 같도록 위치 해시로 일부만 켠다.</summary>
        /// <param name="outward">면이 바라보는 방향(−1/+1).</param>
        private static void FacadeWindows(Transform parent, CityMaterials materials, bool alongX, float line, float outward, float from, float to,
            float yFrom, float yTo, int seed)
        {
            for (float along = from + 1.5f; along <= to - 1.2f; along += 3f)
            {
                for (float y = yFrom; y + 1.5f <= yTo; y += 3.2f)
                {
                    int hash = Mathf.Abs((Mathf.RoundToInt(along * 10f) * 73856093) ^ (Mathf.RoundToInt(y * 10f) * 19349663) ^ (seed * 83492791));
                    Material glass = hash % 10 < 3 ? materials.windowLit : materials.windowDark;
                    Vector3 center = alongX ? new Vector3(along, y + 0.75f, line + outward * 0.03f) : new Vector3(line + outward * 0.03f, y + 0.75f, along);
                    Vector3 size = alongX ? new Vector3(1.3f, 1.5f, 0.04f) : new Vector3(0.04f, 1.5f, 1.3f);
                    MapLayoutTools.Box(parent, "Window", center, size, glass, collider: false);
                }
            }
        }

        /// <summary>들어갈 수 없는 가게임을 알리도록 닫힌 셔터를 붙인다.</summary>
        private static void Shutter(Transform parent, CityMaterials materials, bool alongX, float line, float outward, float center, float width)
        {
            // 셔터 면 기준 높이·바깥 거리를 월드 위치/크기로 바꾼다.
            Vector3 Point(float y, float offset) => alongX ? new Vector3(center, y, line + outward * offset) : new Vector3(line + outward * offset, y, center);
            Vector3 Size(float h, float t) => alongX ? new Vector3(width, h, t) : new Vector3(t, h, width);
            MapLayoutTools.Box(parent, "Shutter", Point(Walk + 1.5f, 0.04f), Size(3f, 0.06f), materials.shutter, collider: false);
            for (float y = Walk + 0.3f; y < Walk + 3f; y += 0.3f)
                MapLayoutTools.Box(parent, "ShutterRib", Point(y, 0.08f), Size(0.04f, 0.02f), materials.trim, collider: false);
            MapLayoutTools.Box(parent, "ShutterBox", Point(Walk + 3.15f, 0.12f), Size(0.3f, 0.24f), materials.trim, collider: false);
        }

        /// <summary>밤거리 분위기와 길 찾기 표지가 되도록 간판을 달되, 너무 밝지 않게 켜짐·꺼짐을 고른다.</summary>
        private static void ShopSign(Transform parent, bool alongX, float line, float outward, float center, float y, Material material, float width = 3.6f)
        {
            Vector3 position = alongX ? new Vector3(center, y, line + outward * 0.12f) : new Vector3(line + outward * 0.12f, y, center);
            Vector3 size = alongX ? new Vector3(width, 0.8f, 0.15f) : new Vector3(0.15f, 0.8f, width);
            MapLayoutTools.Box(parent, "ShopSign", position, size, material, collider: false);
        }

        /// <summary>가게 앞이 밋밋하지 않도록 머리에 걸리지 않는 차양을 단다.</summary>
        private static void Awning(Transform parent, Material material, bool alongX, float line, float outward, float from, float to, float y)
        {
            float mid = (from + to) / 2f;
            Vector3 position = alongX ? new Vector3(mid, y, line + outward * 0.65f) : new Vector3(line + outward * 0.65f, y, mid);
            Vector3 size = alongX ? new Vector3(to - from, 0.06f, 1.3f) : new Vector3(1.3f, 0.06f, to - from);
            GameObject awning = MapLayoutTools.Box(parent, "Awning", position, size, material, collider: false);
            Vector3 axis = alongX ? Vector3.right : Vector3.forward;
            awning.transform.rotation = Quaternion.AngleAxis(alongX ? outward * 15f : -outward * 15f, axis);
        }

        /// <summary>팔이 뻗은 가로등은 경계 중심이 기둥과 어긋나므로 피벗 기준으로 놓는다.</summary>
        private static GameObject PlaceAtPivot(Context context, Transform group, string path, float x, float z, float yaw, float baseY = Walk)
        {
            GameObject placed = context.Prop(group, path, x, z, yaw, 4.4f, baseY, transformable: false);
            if (placed != null)
                placed.transform.position = new Vector3(x, placed.transform.position.y, z);
            return placed;
        }

        /// <summary>모델의 등갓은 꺼지지 않으므로 사냥 시간에 함께 꺼지도록 등갓 아래에 판형 등을 단다.</summary>
        private static void StreetLamp(Context context, Transform group, float x, float z, float yaw, float baseY, Color color, float intensity, float range)
        {
            GameObject pole = PlaceAtPivot(context, group, City.StreetLamp, x, z, yaw, baseY);
            if (pole == null)
                return;
            Vector3 head = pole.transform.position + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 1.15f + Vector3.up * 4.97f;
            GameObject lamp = MapPiecePlacer.CreatePanelLamp(context.lights, head, color, intensity, range, context.materials.lampOn, context.materials.lampOff, yaw);
            lamp.name = "StreetLamp";
            Transform bulb = lamp.transform.Find("Bulb");
            if (bulb != null)
                bulb.localScale = new Vector3(0.28f, 0.03f, 0.5f);
        }

        #endregion

        #region 조명·지점·분위기

        /// <summary>사냥 시간에 꺼지는 가게·골목 조명을 둔다.</summary>
        private static void BuildShopLights(Context context)
        {
            Material on = context.materials.lampOn;
            Material off = context.materials.lampOff;
            var warm = new Color(1f, 0.84f, 0.62f);
            var cool = new Color(0.9f, 0.96f, 1f);
            float ceiling = ShopCeiling;

            // 가게 천장 높이에 패널 조명 하나를 놓는다.
            void Panel(float x, float z, Color color, float intensity, float range)
                => MapPiecePlacer.CreatePanelLamp(context.lights, new Vector3(x, ceiling, z), color, intensity, range, on, off);

            // 카페 홀·주방
            Panel(-14f, 10.5f, warm, 2.4f, 8f);
            Panel(-10.5f, 12.8f, warm, 2.4f, 8f);
            Panel(-12f, 17.5f, cool, 1.8f, 7f);
            // 편의점 매장·창고
            foreach (float x in new[] { 10.5f, 14.5f, 18f })
            {
                Panel(x, 10.4f, cool, 2.6f, 8f);
                Panel(x, 13.8f, cool, 2.6f, 8f);
            }
            Panel(13f, 18f, cool, 1.6f, 7f);
            // 뒷골목 벽등 (주황)
            MapPiecePlacer.CreatePanelLamp(context.lights, new Vector3(-19.7f, Walk + 3.2f, 14f), new Color(1f, 0.65f, 0.35f), 2f, 8f, on, off, 90f).name = "AlleyLamp";
            // 편의점 간판 빛이 보도에 비치게
            MapPiecePlacer.CreateBareLight(context.lights, "StoreSignGlow", new Vector3(14f, ShopCeiling + 0.3f, Facade - 1.2f), new Color(0.6f, 1f, 0.75f), 1.2f, 6f);
            MapPiecePlacer.CreateBareLight(context.lights, "CafeSignGlow", new Vector3(-12f, ShopCeiling, Facade - 1.2f), new Color(1f, 0.8f, 0.55f), 1.2f, 6f);
        }

        /// <summary>시작하자마자 마주치지 않도록 마네킹은 북쪽, 가드는 남쪽 블록에서 시작하게 한다.</summary>
        private static void CreatePoints(Transform mapRoot)
        {
            List<Vector3> mannequins = MannequinSpawns.Select(p => MapPiecePlacer.FindFreeSpot(p, 0.45f, 0f, "마네킹 스폰")).ToList();
            List<Vector3> guards = GuardSpawns.Select(p => MapPiecePlacer.FindFreeSpot(p, 0.45f, 0f, "가드 스폰")).ToList();
            List<Vector3> generators = GeneratorSpots.Select(p => MapPiecePlacer.FindFreeSpot(p, 0.75f, 1.5f, "전원 장치")).ToList();

            MapSceneBuilder.CreatePoints(mapRoot, "MannequinSpawnPoints", mannequins, Vector3.zero,
                (parent, position, rotation) => TestEnvironmentSetupTool.CreateSpawnPoint(parent, PlayerRole.Mannequin, position, rotation));
            MapSceneBuilder.CreatePoints(mapRoot, "GeneratorSpawnPoints", generators, Vector3.zero,
                (parent, position, rotation) => MapSceneBuilder.CreateMarker<GeneratorSpawnPoint>(parent, "GeneratorSpawnPoint", position, rotation));
            MapSceneBuilder.CreatePoints(mapRoot, MapSceneBuilder.GuardSpawnGroup, guards, Vector3.zero,
                (parent, position, rotation) => TestEnvironmentSetupTool.CreateSpawnPoint(parent, PlayerRole.Guard, position, rotation));
        }

        /// <summary>밤 분위기를 내도록 하늘·환경광·먼 거리 안개를 맞춘다.</summary>
        private static void ApplyAtmosphere(Scene scene, Material skybox)
        {
            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.1f, 0.11f, 0.16f);
            // 영역 밖 거리의 끝이 하늘 그림과 이어지도록 안개로 덮는다.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.045f, 0.045f, 0.07f);
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 115f;
            SceneLightingSetup.Apply(scene);
        }

        #endregion

        #region 확인용 결과

        /// <summary>
        /// Unity를 열지 않고도 결과를 확인할 수 있도록 배치도(지붕·천장을 숨김)와 구역별 눈높이 장면을 찍어 Temp/CityStreet에 저장한다.
        /// </summary>
        private static void CapturePreviews(Context context, Bounds playArea)
        {
            const float eye = 1.7f;
            var shots = new List<MapPreviewCapture.Shot>
            {
                new MapPreviewCapture.Shot("01_Overview", new Vector3(-30f, 13f, -18f), new Vector3(0f, 0f, 4f)),
                new MapPreviewCapture.Shot("02_MainRoadWest", new Vector3(-30f, eye, 0f), new Vector3(10f, 1f, 0f)),
                new MapPreviewCapture.Shot("03_Intersection", new Vector3(-7f, Walk + eye, -7f), new Vector3(6f, 1f, 6f)),
                new MapPreviewCapture.Shot("04_CafeFront", new Vector3(-6f, Walk + eye, 5f), new Vector3(-15f, 1.5f, 9f)),
                new MapPreviewCapture.Shot("05_CafeInside", new Vector3(-9f, Walk + eye, 9f), new Vector3(-15f, 1f, 14f)),
                new MapPreviewCapture.Shot("06_Alley", new Vector3(-18f, Walk + eye, 7f), new Vector3(-18f, 1f, 19f)),
                new MapPreviewCapture.Shot("07_StoreInside", new Vector3(19f, Walk + eye, 9f), new Vector3(9f, 1f, 15f)),
                new MapPreviewCapture.Shot("08_StoreFront", new Vector3(5f, Walk + eye, 5f), new Vector3(16f, 1.5f, 8f)),
                new MapPreviewCapture.Shot("09_ParkingLot", new Vector3(-9f, Walk + eye, -9f), new Vector3(-28f, 1f, -17f)),
                new MapPreviewCapture.Shot("10_ConstructionSite", new Vector3(9f, Walk + eye, -9f), new Vector3(26f, 2f, -16f)),
                new MapPreviewCapture.Shot("11_SlabTop", new Vector3(21f, SlabTop + eye, -18f), new Vector3(30f, 2f, -10f)),
                new MapPreviewCapture.Shot("12_SideRoadNorth", new Vector3(0f, eye, -18f), new Vector3(0f, 1f, 18f)),
            };
            MapPreviewCapture.Capture(PreviewFolder, playArea, context.roofs.Where(r => r != null).ToList(), shots);
        }

        /// <summary>
        /// 배치 좌표를 다듬을 때 에셋 실제 크기와 크기 보정 내역을 참고하도록 Temp/CityStreet/sizes.txt에 남긴다.
        /// </summary>
        private static void WriteReport(Context context, int converted, int colliders, int props, int skipped)
        {
            Directory.CreateDirectory(PreviewFolder);
            string text = $"변신 사물 {props}, 제외 {skipped}, 충돌체 추가 {colliders}, 머티리얼 변환 {converted}, 없는 에셋 {context.placer.MissingCount}\n" +
                          "\n[크기 보정]\n" + string.Join("\n", context.sizeFixes) +
                          "\n\n[에셋 크기 (가로 x 높이 x 깊이, 회전 전)]\n" + context.placer.DescribeSizes();
            File.WriteAllText(Path.Combine(PreviewFolder, "sizes.txt"), text);
        }

        #endregion

        /// <summary>도시 길거리 머티리얼을 없으면 URP Lit으로 만든다.</summary>
        private sealed class CityMaterials
        {
            private const string Folder = "Assets/_Project/Art/MapKit/Materials/CityStreet";
            private const string SkylineTexture = "Assets/_Project/Art/MapKit/Textures/T_City_NightSkyline.png";

            public Material asphalt, sidewalk, curb, lineWhite, lineYellow, manhole, alley, parking, dirt,
                brick, plaster, concreteWall, cafeWall, storeWall, backdrop, trim, shutter, glass, windowLit, windowDark,
                signRed, signBlue, signGreen, signWarm, signDark, awningRed, cafeFloor, storeFloor, ceiling,
                concrete, rail, pipe, container, containerDoor, lampOn, lampOff,
                glowWhite, glowRed, glowGreen, glowAmber, lampHead, outerGround, skybox;

            /// <summary>FBX 가져오기는 발광을 옮기지 않으므로 소품 발광 머티리얼을 맵 발광 머티리얼로 바꿔 끼운다.</summary>
            public Dictionary<string, Material> PropRemap { get; private set; }

            /// <summary>
            /// 빌더를 다시 실행해도 GUID가 유지되어 씬 참조가 끊기지 않도록 있으면 불러오고, 없을 때만 URP Lit으로 만든다.
            /// </summary>
            public static CityMaterials LoadOrCreate()
            {
                var factory = new MapMaterialFactory(Folder);
                var result = new CityMaterials
                {
                    asphalt = factory.Opaque("M_City_Asphalt", new Color(0.16f, 0.16f, 0.17f), 0f, 0.25f),
                    sidewalk = factory.Opaque("M_City_Sidewalk", new Color(0.48f, 0.46f, 0.43f), 0f, 0.2f),
                    curb = factory.Opaque("M_City_Curb", new Color(0.62f, 0.61f, 0.58f), 0f, 0.2f),
                    lineWhite = factory.Opaque("M_City_LineWhite", new Color(0.85f, 0.85f, 0.82f), 0f, 0.3f),
                    lineYellow = factory.Opaque("M_City_LineYellow", new Color(0.9f, 0.7f, 0.1f), 0f, 0.3f),
                    manhole = factory.Opaque("M_City_Manhole", new Color(0.12f, 0.12f, 0.12f), 0.7f, 0.4f),
                    alley = factory.Opaque("M_City_Alley", new Color(0.12f, 0.12f, 0.12f), 0f, 0.5f),
                    parking = factory.Opaque("M_City_Parking", new Color(0.2f, 0.2f, 0.21f), 0f, 0.2f),
                    dirt = factory.Opaque("M_City_Dirt", new Color(0.3f, 0.24f, 0.17f), 0f, 0.05f),
                    brick = factory.Opaque("M_City_Brick", new Color(0.42f, 0.22f, 0.17f), 0f, 0.15f),
                    plaster = factory.Opaque("M_City_Plaster", new Color(0.62f, 0.58f, 0.5f), 0f, 0.2f),
                    concreteWall = factory.Opaque("M_City_ConcreteWall", new Color(0.42f, 0.43f, 0.44f), 0f, 0.15f),
                    cafeWall = factory.Opaque("M_City_CafeWall", new Color(0.7f, 0.6f, 0.47f), 0f, 0.3f),
                    storeWall = factory.Opaque("M_City_StoreWall", new Color(0.86f, 0.87f, 0.86f), 0f, 0.4f),
                    backdrop = factory.Opaque("M_City_Backdrop", new Color(0.13f, 0.14f, 0.17f), 0f, 0.1f),
                    trim = factory.Opaque("M_City_Trim", new Color(0.1f, 0.1f, 0.11f), 0.3f, 0.4f),
                    shutter = factory.Opaque("M_City_Shutter", new Color(0.45f, 0.46f, 0.47f), 0.6f, 0.4f),
                    glass = factory.Transparent("M_City_Glass", new Color(0.55f, 0.7f, 0.78f, 0.22f)),
                    windowLit = factory.Emissive("M_City_WindowLit", new Color(0.95f, 0.8f, 0.55f), new Color(1f, 0.78f, 0.5f) * 1.3f),
                    windowDark = factory.Opaque("M_City_WindowDark", new Color(0.07f, 0.09f, 0.12f), 0.3f, 0.85f),
                    signRed = factory.Emissive("M_City_SignRed", new Color(0.8f, 0.1f, 0.08f), new Color(1f, 0.15f, 0.1f) * 2f),
                    signBlue = factory.Emissive("M_City_SignBlue", new Color(0.1f, 0.3f, 0.8f), new Color(0.2f, 0.45f, 1f) * 2f),
                    signGreen = factory.Emissive("M_City_SignGreen", new Color(0.1f, 0.6f, 0.3f), new Color(0.2f, 1f, 0.5f) * 1.8f),
                    signWarm = factory.Emissive("M_City_SignWarm", new Color(0.95f, 0.75f, 0.45f), new Color(1f, 0.75f, 0.4f) * 2f),
                    signDark = factory.Opaque("M_City_SignDark", new Color(0.25f, 0.24f, 0.22f), 0f, 0.4f),
                    awningRed = factory.Opaque("M_City_Awning", new Color(0.55f, 0.12f, 0.1f), 0f, 0.1f),
                    cafeFloor = factory.Opaque("M_City_CafeFloor", new Color(0.38f, 0.26f, 0.16f), 0f, 0.45f),
                    storeFloor = factory.Opaque("M_City_StoreFloor", new Color(0.82f, 0.82f, 0.8f), 0f, 0.6f),
                    ceiling = factory.Opaque("M_City_Ceiling", new Color(0.78f, 0.78f, 0.76f), 0f, 0.2f),
                    concrete = factory.Opaque("M_City_Concrete", new Color(0.55f, 0.54f, 0.52f), 0f, 0.1f),
                    rail = factory.Opaque("M_City_Rail", new Color(0.85f, 0.65f, 0.1f), 0.4f, 0.4f),
                    pipe = factory.Opaque("M_City_Pipe", new Color(0.35f, 0.36f, 0.37f), 0.7f, 0.4f),
                    container = factory.Opaque("M_City_Container", new Color(0.2f, 0.32f, 0.45f), 0.5f, 0.35f),
                    containerDoor = factory.Opaque("M_City_ContainerDoor", new Color(0.85f, 0.85f, 0.83f), 0.3f, 0.4f),
                    glowWhite = factory.Emissive("M_City_GlowWhiteLit", new Color(0.92f, 0.95f, 1f), new Color(0.9f, 0.95f, 1f) * 1.6f),
                    glowRed = factory.Emissive("M_City_GlowRedLit", new Color(1f, 0.12f, 0.08f), new Color(1f, 0.1f, 0.06f) * 2.5f),
                    glowGreen = factory.Emissive("M_City_GlowGreenLit", new Color(0.15f, 1f, 0.45f), new Color(0.15f, 1f, 0.45f) * 2.5f),
                    glowAmber = factory.Emissive("M_City_GlowAmberLit", new Color(1f, 0.6f, 0.1f), new Color(1f, 0.55f, 0.08f) * 2.5f),
                    lampHead = factory.Opaque("M_City_LampHead", new Color(0.2f, 0.2f, 0.2f), 0.3f, 0.5f),
                    outerGround = factory.Opaque("M_City_OuterGround", new Color(0.04f, 0.04f, 0.045f), 0f, 0.1f),
                    skybox = LoadOrCreateSkybox(),
                    lampOn = AssetDatabase.LoadAssetAtPath<Material>($"{TestEnvironmentSetupTool.MaterialFolder}/M_LampOn.mat"),
                    lampOff = AssetDatabase.LoadAssetAtPath<Material>($"{TestEnvironmentSetupTool.MaterialFolder}/M_LampOff.mat"),
                };
                result.PropRemap = new Dictionary<string, Material>
                {
                    { "M_City_GlowWarm", result.lampHead },
                    { "M_City_GlowWhite", result.glowWhite },
                    { "M_City_GlowRed", result.glowRed },
                    { "M_City_GlowGreen", result.glowGreen },
                    { "M_City_GlowAmber", result.glowAmber },
                };
                AssetDatabase.SaveAssets();
                return result;
            }

            /// <summary>밤 도시 파노라마 텍스처로 하늘 머티리얼을 만든다.</summary>
            private static Material LoadOrCreateSkybox()
            {
                if (AssetImporter.GetAtPath(SkylineTexture) is TextureImporter importer)
                {
                    bool changed = importer.mipmapEnabled || importer.wrapModeU != TextureWrapMode.Repeat || importer.wrapModeV != TextureWrapMode.Clamp
                                   || importer.maxTextureSize != 4096;
                    if (changed)
                    {
                        importer.mipmapEnabled = false;
                        importer.wrapModeU = TextureWrapMode.Repeat;
                        importer.wrapModeV = TextureWrapMode.Clamp;
                        importer.maxTextureSize = 4096;
                        importer.SaveAndReimport();
                    }
                }
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SkylineTexture);
                if (texture == null)
                {
                    Debug.LogWarning($"[CityStreetBuilder] 하늘 그림이 없어 하늘 없이 만듭니다: {SkylineTexture}");
                    return null;
                }

                string path = $"{Folder}/M_City_Skybox.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("Skybox/Panoramic")) { name = "M_City_Skybox" };
                    AssetDatabase.CreateAsset(material, path);
                }
                material.SetTexture("_MainTex", texture);
                material.SetFloat("_Mapping", 1f);      // 위도·경도(360°)
                material.SetFloat("_ImageType", 0f);    // 360°
                material.SetFloat("_Exposure", 1f);
                material.SetFloat("_Rotation", 0f);
                material.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
                EditorUtility.SetDirty(material);
                return material;
            }
        }
    }
}
