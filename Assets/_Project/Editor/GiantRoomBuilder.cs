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

    /// <summary>플레이어가 손가락만 하게 느껴지도록 실제 방을 10배로 키운 "거대한 방" 맵을 항상 같은 결과로 만든다.</summary>
    public static class GiantRoomBuilder
    {
        private const string MapName = "Giant Room";
        private const string SceneName = "Map_GiantRoom";
        internal const string PreviewFolder = "Temp/GiantRoom";

        /// <summary>방 안쪽 반 폭과 높이(m).</summary>
        private const float Half = 25f, RoomHeight = 35f, WallThickness = 1f;
        /// <summary>실제 크기 모델을 키우는 배율.</summary>
        private const float Scale = 10f;

        // 가구 윗면 높이 (실제 크기 × 10)
        private const float DeskTop = 7.5f, NightstandTop = 5.8f, CoffeeTableTop = 4.5f, BedTop = 5.5f, SofaSeat = 4.8f,
            ChairSeat = 4.7f, ToyBoxTop = 4.6f, ShelfTopPanel = 18f;
        /// <summary>계단처럼 밟고 오르도록 아래 서랍일수록 많이 빼는 거리(m).</summary>
        private static readonly float[] DeskDrawerPull = { 0f, 0f, 0f };
        private static readonly float[] NightstandDrawerPull = { 0f, 0f };

        /// <summary>책장 선반 윗면 높이 (아래부터).</summary>
        private static readonly float[] ShelfLevels = { 0.3f, 3.8f, 7.3f, 10.8f, 14.3f };

        // 가구 자리
        private static readonly Vector3 BedCenter = new Vector3(-17.7f, 0f, 14.7f);
        private static readonly Vector3 DeskCenter = new Vector3(21.7f, 0f, 6f);
        private static readonly Vector3 ShelfCenter = new Vector3(4f, 0f, 23.2f);
        private static readonly Vector3 SofaCenter = new Vector3(-20.7f, 0f, -8f);
        private static readonly Vector3 TableCenter = new Vector3(-11f, 0f, -8f);

        private static readonly Vector3[] MannequinSpawns =
        {
            new Vector3(0f, 0f, 0f), new Vector3(-3f, 0f, -15f), new Vector3(10f, 0f, 12f), new Vector3(-6f, 0f, 10f),
            new Vector3(14f, 0f, -2f), new Vector3(21.7f, DeskTop, 8f), new Vector3(-17f, BedTop, 10f), new Vector3(5f, ShelfLevels[2], 23f),
            new Vector3(-11f, CoffeeTableTop, -6f), new Vector3(-20f, SofaSeat, -12f)
        };

        private static readonly Vector3[] GuardSpawns =
        {
            new Vector3(2f, 0f, -20f), new Vector3(-2f, 0f, -19f), new Vector3(6f, 0f, -19f), new Vector3(0f, 0f, -15f), new Vector3(9f, 0f, -17f)
        };

        private static readonly Vector3[] GeneratorSpots =
        {
            new Vector3(-2f, 0f, 2f), new Vector3(10f, 0f, -6f), new Vector3(-4f, 0f, -20f), new Vector3(22f, 0f, 8f),
            new Vector3(16f, 0f, 20f), new Vector3(-8f, 0f, 2f), new Vector3(0f, 0f, 12f), new Vector3(23f, DeskTop, 9f),
            new Vector3(-20f, BedTop, 16f), new Vector3(6f, ShelfLevels[2], 23.2f), new Vector3(-11f, CoffeeTableTop, -10f),
            new Vector3(-21f, SofaSeat, -4f)
        };

        /// <summary>
        /// 만드는 동안 함께 쓰는 상태.
        /// </summary>
        private sealed class Context : MapLayoutTools
        {
            public RoomMaterials materials;
            public Transform lights;
            /// <summary>충돌체 자동 추가·변신 등록에서 빠지도록 Environment 밖에 두는 오르기 물체 묶음.</summary>
            public Transform climbables;
            public readonly List<Renderer> roofs = new List<Renderer>();

            /// <summary>천장이 있으면 위에서 찍은 배치도에 방 안이 보이지 않으므로, 천장을 만들 때 숨길 목록에 함께 넣는다.</summary>
            public GameObject Roof(Transform parent, string name, Vector3 center, Vector3 size, Material material)
            {
                GameObject roof = Box(parent, name, center, size, material);
                roofs.Add(roof.GetComponent<Renderer>());
                return roof;
            }

            /// <summary>플레이어가 손가락만 하게 느껴지도록 가구를 10배로 키운 배율.</summary>
            public GameObject Furniture(Transform group, string path, Vector3 floorCenter, float yaw)
                => placer.Place(path, group, floorCenter, yaw, scale: Vector3.one * Scale);

            /// <summary>10배로 키워도 사람 크기인 작은 물건이 이 맵의 숨을 곳이라 변신 사물 후보로 표시한다.</summary>
            public GameObject Small(Transform group, string path, float x, float z, float yaw, float baseY = 0f)
                => placer.AsProp(placer.Place(path, group, new Vector3(x, baseY, z), yaw, scale: Vector3.one * Scale));
        }

        #region 메뉴

        /// <summary>씬을 덮어쓰므로 작업 중인 씬을 잃지 않도록 저장 여부를 먼저 묻고, 배치 확인용 사진까지 남긴다.</summary>
        [MenuItem(EditorMenus.Root + "맵 생성/거대한 방 (Map_GiantRoom)")]
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
        /// <param name="capturePreview">true면 Temp/GiantRoom에 배치도·장면 사진을 남긴다.</param>
        public static void BuildScene(string scenePath, bool capturePreview)
        {
            // 빌더는 프리팹을 놓으므로 빠진 맵 물체 프리팹을 먼저 만든다.
            MapObjectPrefabBuilder.EnsurePrefabs();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Transform mapRoot = new GameObject(MapAuthoringTools.MapRootName).transform;

            RoomMaterials materials = RoomMaterials.LoadOrCreate();
            var context = new Context
            {
                materials = materials,
                placer = new MapPiecePlacer(null),
                environment = MapLayoutTools.CreateChild(mapRoot, MapAuthoringTools.EnvironmentName),
                lights = MapLayoutTools.CreateChild(mapRoot, "Lights"),
                climbables = MapLayoutTools.CreateChild(mapRoot, "Climbables")
            };

            BuildShell(context);
            BuildFurniture(context);
            BuildClimbRoutes(context);
            PlaceSmallItems(context);

            // 정리가 등록 컴포넌트를 지우지 않도록 변신 사물 등록을 마지막에 한다.
            MapSceneBuilder.CleanEnvironment(context.environment);
            MapLayoutTools.ReplaceMaterials(context.environment, materials.PropRemap);
            int converted = UrpMaterialConverter.ConvertRenderers(context.environment.GetComponentsInChildren<Renderer>(true));
            int colliders = MapSceneBuilder.AddMissingColliders(context.environment);
            context.placer.RegisterCandidates(out int propCount, out int skipped);
            Physics.SyncTransforms();

            BuildLights(context);
            CreatePoints(mapRoot);

            NavMeshSurface surface = mapRoot.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            var playArea = new Bounds(new Vector3(0f, RoomHeight / 2f, 0f), new Vector3(Half * 2f, RoomHeight, Half * 2f));
            float ceilingHeight = MapSceneBuilder.CreateBoundary(playArea, context.environment);

            MapInfo mapInfo = mapRoot.gameObject.AddComponent<MapInfo>();
            var info = new SerializedObject(mapInfo);
            info.FindProperty("mapName").stringValue = MapName;
            info.FindProperty("ceilingHeight").floatValue = ceilingHeight;
            info.ApplyModifiedPropertiesWithoutUndo();
            // 대기 화면: 문 쪽 위에서 방 전체를 내려다본다.
            var overviewPosition = new Vector3(18f, 26f, -22f);
            GameSystemsSceneBuilder.SetOverview(mapInfo, overviewPosition, Quaternion.LookRotation(new Vector3(-6f, 4f, 8f) - overviewPosition));

            ApplyAtmosphere(scene);

            // 최적화(오클루전 굽기)에 저장된 경로가 필요하므로 먼저 저장한다.
            EditorSceneManager.SaveScene(scene, scenePath);
            MapOptimizer.Optimize(scene, indoor: true);
            if (capturePreview)
                CapturePreviews(context, playArea);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            WriteReport(context, converted, colliders, propCount, skipped);
            Debug.Log($"[GiantRoomBuilder] 거대한 방 완료: {scenePath}\n" +
                      $"변신 사물(작은 물건) {propCount}개(크기·구성으로 제외 {skipped}개), 충돌체 추가 {colliders}개, 머티리얼 변환 {converted}개, " +
                      $"없는 에셋 {context.placer.MissingCount}개. 에셋 크기 목록: {PreviewFolder}/sizes.txt");
        }

        #region 방 껍데기

        /// <summary>크기 착각이 살도록 걸레받이·콘센트 같은 작은 부분까지 10배로 만든다.</summary>
        private static void BuildShell(Context context)
        {
            Transform group = context.Group("Shell");
            RoomMaterials materials = context.materials;
            float outer = Half + WallThickness;

            MapLayoutTools.Box(group, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(outer * 2f, 1f, outer * 2f), materials.floor);
            for (float x = -Half + 2.5f; x < Half; x += 5f)
                MapLayoutTools.Box(group, "FloorSeam", new Vector3(x, 0.004f, 0f), new Vector3(0.08f, 0.008f, Half * 2f), materials.floorSeam, collider: false);
            context.Roof(group, "Ceiling", new Vector3(0f, RoomHeight + 0.5f, 0f), new Vector3(outer * 2f, 1f, outer * 2f), materials.ceiling);

            // 북쪽 벽 가운데 창.
            MapLayoutTools.WallRun(group, alongX: true, Half + WallThickness / 2f, -outer, outer, 0f, RoomHeight, materials.wall, materials.glass, WallThickness, null,
                new Gap(-2f, 12f, 20f, 11f, true));
            MapLayoutTools.Box(group, "NightSky", new Vector3(-2f, 25.5f, Half + WallThickness + 1f), new Vector3(16f, 15f, 0.2f), materials.nightSky, collider: false);
            MapLayoutTools.Box(group, "WindowSill", new Vector3(-2f, 19.8f, Half - 0.6f), new Vector3(13f, 0.4f, 1.2f), materials.baseboard);
            MapLayoutTools.Box(group, "WindowBar", new Vector3(-2f, 25.5f, Half + 0.1f), new Vector3(0.3f, 11f, 0.3f), materials.baseboard, collider: false);
            MapLayoutTools.Box(group, "WindowBar", new Vector3(-2f, 25.5f, Half + 0.1f), new Vector3(12f, 0.3f, 0.3f), materials.baseboard, collider: false);

            foreach (float side in new[] { -1f, 1f })
            {
                MapLayoutTools.Box(group, "Wall", new Vector3(side * (Half + WallThickness / 2f), RoomHeight / 2f, 0f), new Vector3(WallThickness, RoomHeight, outer * 2f), materials.wall);
            }
            MapLayoutTools.Box(group, "Wall", new Vector3(0f, RoomHeight / 2f, -(Half + WallThickness / 2f)), new Vector3(outer * 2f, RoomHeight, WallThickness), materials.wall);

            // 걸레받이(높이 1.2m)
            foreach (float side in new[] { -1f, 1f })
            {
                MapLayoutTools.Box(group, "Baseboard", new Vector3(0f, 0.6f, side * (Half - 0.15f)), new Vector3(Half * 2f, 1.2f, 0.3f), materials.baseboard);
                MapLayoutTools.Box(group, "Baseboard", new Vector3(side * (Half - 0.15f), 0.6f, 0f), new Vector3(0.3f, 1.2f, Half * 2f), materials.baseboard);
            }
            // 콘센트 (동쪽·서쪽 벽)
            MapLayoutTools.Box(group, "Outlet", new Vector3(Half - 0.06f, 3f, -6f), new Vector3(0.12f, 1.2f, 0.8f), materials.baseboard, collider: false);
            MapLayoutTools.Box(group, "Outlet", new Vector3(-Half + 0.06f, 3f, 2.8f), new Vector3(0.12f, 1.2f, 0.8f), materials.baseboard, collider: false);
        }

        #endregion

        #region 가구

        /// <summary>숨을 곳과 오를 곳이 생기도록 벽마다 가구를 나눠 놓는다.</summary>
        private static void BuildFurniture(Context context)
        {
            Transform group = context.Group("Furniture");
            RoomMaterials materials = context.materials;

            context.Furniture(group, GiantRoom.Bed, BedCenter, 180f);
            PullDrawers(context.Furniture(group, GiantRoom.Nightstand, new Vector3(-8f, 0f, 22.5f), 180f), 180f, NightstandDrawerPull);
            PullDrawers(context.Furniture(group, GiantRoom.Desk, DeskCenter, -90f), -90f, DeskDrawerPull);
            context.Furniture(group, GiantRoom.DeskChair, new Vector3(14f, 0f, 7.5f), 90f);
            context.Furniture(group, GiantRoom.DeskLamp, new Vector3(22.8f, DeskTop, 1.8f), -60f);
            context.Furniture(group, GiantRoom.Bookshelf, ShelfCenter, 180f);
            BuildShelfBooks(context, group);
            context.Furniture(group, GiantRoom.Wardrobe, new Vector3(-18f, 0f, -21.7f), 0f);
            context.Furniture(group, GiantRoom.Sofa, SofaCenter, 90f);
            context.Furniture(group, GiantRoom.Rug, new Vector3(-6f, 0f, -8f), 0f);
            context.Furniture(group, GiantRoom.CoffeeTable, new Vector3(TableCenter.x, 0.08f, TableCenter.z), 90f);
            context.Furniture(group, GiantRoom.ToyBox, new Vector3(13f, 0f, -21f), 0f);
            context.Furniture(group, GiantRoom.TrashCan, new Vector3(15.5f, 0f, 14f), 0f);
            context.Furniture(group, GiantRoom.FloorLamp, new Vector3(-6f, 0f, -22f), 0f);
            context.Furniture(group, GiantRoom.Door, new Vector3(4f, 0f, -Half + 0.3f), 0f);
            GameObject ceilingLamp = context.Furniture(group, GiantRoom.CeilingLamp, new Vector3(0f, RoomHeight - 1.5f, 0f), 0f);
            if (ceilingLamp != null)
                context.roofs.AddRange(ceilingLamp.GetComponentsInChildren<Renderer>());
        }

        /// <summary>빼낸 서랍을 계단처럼 밟고 오르도록 가구의 서랍을 앞쪽으로 빼낸다.</summary>
        private static void PullDrawers(GameObject furniture, float yaw, float[] pulls)
        {
            if (furniture == null)
                return;
            Vector3 front = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            foreach (Transform part in furniture.GetComponentsInChildren<Transform>(true))
            {
                int marker = part.name.LastIndexOf("_Drawer", System.StringComparison.Ordinal);
                if (marker < 0 || !int.TryParse(part.name.Substring(marker + 7), out int number) || number < 1 || number > pulls.Length)
                    continue;
                part.position += front * pulls[number - 1];
            }
        }

        /// <summary>책마다 다른 색으로 바꿔 끼울 표지 머티리얼 이름.</summary>
        private const string BookCoverMaterial = "M_GR_Blue";
        /// <summary>책 한 권 두께(m, 맵 크기).</summary>
        private const float BookThickness = 0.04f * Scale;

        /// <summary>빈 책장은 책장으로 읽히지 않아 책을 꽂되, 선반을 길로 쓰도록 한쪽은 비운다.</summary>
        private static void BuildShelfBooks(Context context, Transform group)
        {
            var random = new System.Random(50);
            // 눕힌 책 모델을 세우는 회전.
            Quaternion standing = Quaternion.LookRotation(Vector3.up, Vector3.right);
            for (int level = 0; level < ShelfLevels.Length; level++)
            {
                bool leftSide = level % 2 == 0;
                float x = leftSide ? ShelfCenter.x - 3.6f : ShelfCenter.x + 0.6f;
                float end = x + 3f;
                while (x < end)
                {
                    float size = Scale * (0.95f + 0.2f * (float)random.NextDouble());
                    float thickness = 0.04f * size;
                    GameObject book = context.placer.Place(GiantRoom.Book, group, new Vector3(x + thickness / 2f, ShelfLevels[level], ShelfCenter.z + 0.05f), 0f,
                        scale: Vector3.one * size, name: "ShelfBook", tilt: standing);
                    PaintCover(book, context.materials.bookCovers[random.Next(context.materials.bookCovers.Length)]);
                    x += thickness + 0.03f;
                }
            }
        }

        #endregion

        #region 오르는 길 (자연스러운 물건)

        /// <summary>사다리는 방에 어색하므로 아이 방에 있을 법한 물건으로 가구 위에 오르는 길을 만든다.</summary>
        private static void BuildClimbRoutes(Context context)
        {
            Transform group = context.Group("ClimbRoutes");

            // 탁자: 동쪽에서 −X로 올라가 탁자 가장자리(x −8.5)에 닿는다.
            const int tableSteps = 11;
            float tableEdge = TableCenter.x + 2.5f;
            BookStairs(context, group, new Vector3(tableEdge + 0.7f + (tableSteps - 1) * 1.4f, 0f, TableCenter.z), Vector3.left, tableSteps, 1.4f, seed: 7);

            // 소파: 탁자에서 좌판까지 걸친 큰 책 다리.
            GameObject bridge = context.placer.Place(GiantRoom.Book, group, new Vector3(-14.95f, CoffeeTableTop, TableCenter.z - 1f), 0f,
                scale: new Vector3(18f, Scale, 14f), name: "BridgeBook");
            PaintCover(bridge, context.materials.bookCovers[0]);

            // 침대: 바닥까지 흘러내린 이불 자락.
            GameObject drape = context.Furniture(group, GiantRoom.BlanketDrape, new Vector3(-6f, 0f, 12f), 90f);
            if (drape != null)
            {
                Bounds bounds = MapPiecePlacer.GetBounds(drape);
                drape.transform.position += Vector3.right * (BedEdgeX - 0.9f - bounds.min.x);
            }

            BuildDeskCord(context, group);

            // 책장: 1단 빈 칸으로 올라가는 책 계단.
            const int shelfSteps = 9;
            BookStairs(context, group, new Vector3(2.4f + (shelfSteps - 1) * 1.3f, 0f, 20.3f), Vector3.left, shelfSteps, 1.3f, seed: 11);
            // 위 선반으로 오르는 책갈피 끈.
            const float ribbonZ = 20.9f;
            BookmarkRibbon(context, group, new Vector3(1.6f, ShelfLevels[1], ribbonZ), ShelfLevels[3] - ShelfLevels[1], context.materials.bookCovers[0]);
            BookmarkRibbon(context, group, new Vector3(3f, ShelfLevels[3], ribbonZ), ShelfTopPanel - ShelfLevels[3], context.materials.bookCovers[4]);
        }

        /// <summary>침대 동쪽 가장자리(x).</summary>
        private const float BedEdgeX = -10.7f;

        /// <summary>책 한 권 두께가 걸어서 오를 수 있는 단 높이라 책을 쌓아 계단으로 쓴다.</summary>
        private static void BookStairs(Context context, Transform group, Vector3 firstStep, Vector3 direction, int steps, float run, int seed)
        {
            var random = new System.Random(seed);
            float yaw = Mathf.Abs(direction.x) > 0.5f ? 90f : 0f;
            for (int step = 1; step <= steps; step++)
            {
                Vector3 center = firstStep + direction * (run * (step - 1));
                BookPile(context, group, center, step, yaw, random);
            }
        }

        /// <summary>기계가 놓은 것처럼 보이지 않도록 한 권마다 조금씩 비틀어 쌓는다.</summary>
        private static void BookPile(Context context, Transform group, Vector3 floorCenter, int count, float yaw, System.Random random)
        {
            for (int i = 0; i < count; i++)
            {
                var offset = new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f) * 0.15f;
                float twist = ((float)random.NextDouble() - 0.5f) * 6f;
                GameObject book = context.placer.Place(GiantRoom.Book, group, floorCenter + offset + Vector3.up * (BookThickness * i), yaw + twist,
                    scale: Vector3.one * Scale, name: "StairBook");
                PaintCover(book, context.materials.bookCovers[random.Next(context.materials.bookCovers.Length)]);
            }
        }

        /// <summary>책상 위로 오르는 길을 방에 어울리게 만들려고 스탠드 전선을 늘어뜨린다.</summary>
        private static void BuildDeskCord(Context context, Transform group)
        {
            Material cord = context.materials.cord;
            const float radius = 0.12f;
            var hang = new Vector3(18.5f, 0f, 1f);
            float onDesk = DeskTop + radius;

            Segment(group, "LampCord", new Vector3(22.5f, onDesk, 1.8f), new Vector3(18.75f, onDesk, hang.z), radius, cord);
            Segment(group, "LampCord", new Vector3(18.75f, onDesk, hang.z), new Vector3(hang.x, DeskTop - 0.2f, hang.z), radius, cord);
            Segment(group, "LampCord", new Vector3(hang.x, radius, hang.z), new Vector3(hang.x, radius, -6f), radius, cord);
            Segment(group, "LampCord", new Vector3(hang.x, radius, -6f), new Vector3(Half - 0.4f, radius, -6f), radius, cord);
            Segment(group, "LampCord", new Vector3(Half - 0.4f, radius, -6f), new Vector3(Half - 0.4f, 2.7f, -6f), radius, cord);
            MapLayoutTools.Box(group, "Plug", new Vector3(Half - 0.25f, 3f, -6f), new Vector3(0.3f, 0.7f, 0.5f), context.materials.plug);

            HangingLine(context, "LampCord", hang, DeskTop, yaw: -90f, PrimitiveType.Cylinder, new Vector2(radius * 2f, radius * 2f), cord, tassel: false);
        }

        /// <summary>책장 위 선반으로 오르는 길을 방에 어울리게 만들려고 책갈피 끈을 늘어뜨린다.</summary>
        private static void BookmarkRibbon(Context context, Transform group, Vector3 bottom, float height, Material color)
        {
            Vector3 top = bottom + Vector3.up * height;
            GameObject book = context.placer.Place(GiantRoom.Book, group, new Vector3(bottom.x, top.y, ShelfCenter.z - 0.3f), 90f,
                scale: Vector3.one * Scale, name: "RibbonBook");
            PaintCover(book, color);
            HangingLine(context, "BookmarkRibbon", bottom, height, yaw: 180f, PrimitiveType.Cube, new Vector2(0.35f, 0.05f), color, tassel: true);
        }

        /// <summary>전선·책갈피 끈이 같은 오르기 방식을 쓰도록 몸이 걸리지 않는 매달린 줄을 한곳에서 만든다.</summary>
        private static void HangingLine(Context context, string name, Vector3 bottom, float height, float yaw, PrimitiveType shape, Vector2 size,
            Material material, bool tassel)
        {
            Transform line = CreateClimbable(context, name, bottom, yaw, ClimbableKind.Rope, height, Vector3.back, holdOnFront: true);
            float length = height + 0.3f;
            Vector3 scale = shape == PrimitiveType.Cylinder ? new Vector3(size.x, length / 2f, size.y) : new Vector3(size.x, length, size.y);
            LocalPrimitive(line, shape, name + "Line", new Vector3(0f, length / 2f, 0f), scale, material);
            if (tassel)
            {
                LocalPrimitive(line, PrimitiveType.Sphere, "Tassel", new Vector3(0f, 0.05f, 0f), new Vector3(0.45f, 0.6f, 0.45f), material);
                LocalPrimitive(line, PrimitiveType.Cylinder, "TasselFringe", new Vector3(0f, -0.35f, 0f), new Vector3(0.3f, 0.25f, 0.3f), material);
            }

            CapsuleCollider zone = line.gameObject.AddComponent<CapsuleCollider>();
            zone.isTrigger = true;
            zone.radius = 1f;
            zone.height = height + 1.2f;
            zone.center = new Vector3(0f, height / 2f + 0.3f, 0f);
        }

        /// <summary>휘어진 선을 모델 없이 표현하려고 두 점을 잇는 원기둥 조각을 이어 쓴다.</summary>
        private static void Segment(Transform parent, string name, Vector3 from, Vector3 to, float radius, Material material)
        {
            Vector3 direction = to - from;
            float length = direction.magnitude;
            if (length < 0.01f)
                return;
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(segment.GetComponent<Collider>());
            segment.name = name;
            segment.transform.SetParent(parent, false);
            segment.transform.SetPositionAndRotation((from + to) / 2f, Quaternion.FromToRotation(Vector3.up, direction / length));
            segment.transform.localScale = new Vector3(radius * 2f, length / 2f, radius * 2f);
            segment.GetComponent<Renderer>().sharedMaterial = material;
        }

        /// <summary>같은 책이 반복되면 단조로워 원본을 고치지 않고 인스턴스마다 표지 색을 바꾼다.</summary>
        private static void PaintCover(GameObject book, Material cover)
        {
            if (book == null || cover == null)
                return;
            foreach (Renderer part in book.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = part.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != null && slots[i].name == BookCoverMaterial)
                    {
                        slots[i] = cover;
                        changed = true;
                    }
                }
                if (changed)
                    part.sharedMaterials = slots;
            }
        }

        /// <summary>오르는 물체가 총알이나 벽 통과 검사를 막지 않도록 Ignore Raycast 레이어에 둔다.</summary>
        private static Transform CreateClimbable(Context context, string name, Vector3 bottom, float yaw, ClimbableKind kind, float height, Vector3 topExit, bool holdOnFront)
        {
            var root = new GameObject(name);
            root.transform.SetParent(context.climbables, false);
            root.transform.SetPositionAndRotation(bottom, Quaternion.Euler(0f, yaw, 0f));
            root.layer = 2;
            root.AddComponent<Climbable>().Configure(kind, height, topExit, holdOnFront);
            return root.transform;
        }

        /// <summary>장식 조각이 걸리지 않도록 충돌체와 그림자를 끈 채 부모 좌표로 놓는다.</summary>
        private static GameObject LocalPrimitive(Transform parent, PrimitiveType type, string name, Vector3 localCenter, Vector3 localScale, Material material)
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(shape.GetComponent<Collider>());
            shape.name = name;
            shape.layer = 0;
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = localCenter;
            shape.transform.localScale = localScale;
            Renderer shapeRenderer = shape.GetComponent<Renderer>();
            shapeRenderer.sharedMaterial = material;
            shapeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            return shape;
        }

        #endregion

        #region 작은 물건 (변신 사물)

        /// <summary>오르는 길이 의미 있도록 가구 위에도 숨을 수 있는 작은 물건을 나눠 놓는다.</summary>
        private static void PlaceSmallItems(Context context)
        {
            Transform group = context.Group("SmallItems");

            // 책상 위.
            context.Small(group, GiantRoom.Pencil, 20.6f, 6f, 80f, DeskTop);
            context.Small(group, GiantRoom.Pencil, 21.4f, 6.6f, 95f, DeskTop);
            context.Small(group, GiantRoom.Eraser, 20.2f, 8.5f, 20f, DeskTop);
            context.Small(group, GiantRoom.Ruler, 23.3f, 7f, 90f, DeskTop);
            context.Small(group, GiantRoom.Sharpener, 21f, 9.6f, 30f, DeskTop);
            context.Small(group, GiantRoom.StickyNotes, 22.6f, 10.2f, 10f, DeskTop);
            context.Small(group, GiantRoom.Mug, 23.1f, 4.2f, 0f, DeskTop);
            context.Small(group, GiantRoom.Smartphone, 21f, 3.8f, 15f, DeskTop);
            context.Small(group, GiantRoom.GlueStick, 19.8f, 10.8f, 0f, DeskTop);
            context.Small(group, GiantRoom.Scissors, 22.2f, 5.2f, -40f, DeskTop);
            context.Small(group, GiantRoom.TapeRoll, 23.4f, 9.6f, 90f, DeskTop);
            context.Small(group, GiantRoom.Crayon, 19.8f, 5f, 60f, DeskTop);
            context.Small(group, GiantRoom.Coin, 20.4f, 2.6f, 0f, DeskTop);
            context.Small(group, GiantRoom.PaperClip, 22.2f, 8f, 35f, DeskTop);
            context.Small(group, GiantRoom.Battery, 19.9f, 7.4f, 10f, DeskTop);
            context.Small(group, GiantRoom.Apple, 23.4f, 11.1f, 0f, DeskTop);

            // 협탁·침대·의자·소파·장난감 상자 위
            context.Small(group, GiantRoom.AlarmClock, -8.6f, 22.8f, 180f, NightstandTop);
            context.Small(group, GiantRoom.TissueBox, -7.4f, 21.4f, 100f, NightstandTop);
            context.Small(group, GiantRoom.Smartphone, -15f, 9f, 40f, BedTop);
            context.Small(group, GiantRoom.Book, -20f, 7.5f, 20f, BedTop);
            context.Small(group, GiantRoom.Book, 14f, 7.5f, -10f, ChairSeat);
            context.Small(group, GiantRoom.Smartphone, -20.5f, -14f, 90f, SofaSeat);
            context.Small(group, GiantRoom.TissueBox, -20.6f, -1.8f, 0f, SofaSeat);
            context.Small(group, GiantRoom.ToyCar, 12.4f, -21.2f, 20f, ToyBoxTop);
            context.Small(group, GiantRoom.ToyBlock, 14.2f, -20.6f, 15f, ToyBoxTop);

            // 탁자 위.
            context.Small(group, GiantRoom.Mug, -10f, -11.5f, 120f, CoffeeTableTop);
            context.Small(group, GiantRoom.Apple, -12.2f, -9.6f, 0f, CoffeeTableTop);
            context.Small(group, GiantRoom.Dice, -10.4f, -7.6f, 25f, CoffeeTableTop);
            context.Small(group, GiantRoom.BottleCap, -12.4f, -5f, 0f, CoffeeTableTop);
            context.Small(group, GiantRoom.ToyCar, -10.8f, -4.6f, -70f, CoffeeTableTop);
            context.Small(group, GiantRoom.Battery, -12.6f, -12f, 45f, CoffeeTableTop);

            // 책장 선반 (책이 없는 쪽)
            context.Small(group, GiantRoom.ToyBlock, 6.4f, 23f, 10f, ShelfLevels[0]);
            context.Small(group, GiantRoom.AlarmClock, 1.6f, 23f, 180f, ShelfLevels[1]);
            context.Small(group, GiantRoom.RubberDuck, 6.8f, 23f, 200f, ShelfLevels[2]);
            context.Small(group, GiantRoom.TapeRoll, 3.1f, 23.8f, 0f, ShelfLevels[3]);
            context.Small(group, GiantRoom.LegoBrick, 6.2f, 23f, 30f, ShelfLevels[4]);
            context.Small(group, GiantRoom.Dice, 7.2f, 22.6f, 0f, ShelfTopPanel);
            context.Small(group, GiantRoom.Mug, 6f, 23.2f, 60f, ShelfTopPanel);

            // 바닥: 러그 위·장난감 상자 둘레·책상 밑·방 곳곳
            var floorItems = new (string path, float x, float z, float yaw)[]
            {
                (GiantRoom.Pencil, -4f, -4f, 30f), (GiantRoom.Pencil, 3f, -11.8f, 160f), (GiantRoom.Crayon, -2f, -12f, 75f),
                (GiantRoom.Crayon, 6f, 5f, 10f), (GiantRoom.LegoBrick, 10f, -18f, 20f), (GiantRoom.LegoBrick, 15.6f, -17.4f, 70f),
                (GiantRoom.ToyBlock, 9f, -21f, 0f), (GiantRoom.ToyBlock, 11f, -17f, 40f), (GiantRoom.ToyBlock, 17f, -21.5f, 15f),
                (GiantRoom.RubberDuck, 18f, -14f, 220f), (GiantRoom.ToyCar, 8f, -12f, 110f), (GiantRoom.Dice, 2f, 6f, 10f),
                (GiantRoom.Coin, 0f, -6f, 0f), (GiantRoom.Coin, 12f, 2f, 0f), (GiantRoom.BottleCap, -9f, 6f, 0f),
                (GiantRoom.Eraser, 20f, 6.5f, 50f), (GiantRoom.StickyNotes, 23f, 10f, 30f), (GiantRoom.PaperClip, 17f, 3f, 0f),
                (GiantRoom.Battery, 5f, -20f, 80f), (GiantRoom.Apple, -1f, 18f, 0f), (GiantRoom.Ruler, -6f, 17.5f, 15f),
                (GiantRoom.Scissors, 10f, 16f, -20f), (GiantRoom.Book, 12f, 9f, 35f), (GiantRoom.Book, -3f, 6f, -60f),
                (GiantRoom.Mug, -22f, 3.2f, 0f), (GiantRoom.TissueBox, 20f, -20f, 20f), (GiantRoom.GlueStick, -12f, 2f, 0f),
                (GiantRoom.Smartphone, 4f, 16.5f, 120f), (GiantRoom.AlarmClock, 22f, -10f, -60f), (GiantRoom.Sharpener, -7f, -15f, 30f)
            };
            foreach ((string path, float x, float z, float yaw) in floorItems)
                context.Small(group, path, x, z, yaw);
        }

        #endregion

        #region 조명·지점·분위기

        /// <summary>조명 기구가 실제로 빛을 내야 크기 착각이 살아서 사냥 시간에 꺼지는 조명들을 둔다.</summary>
        private static void BuildLights(Context context)
        {
            Lamp(context, "CeilingLight", new Vector3(0f, RoomHeight - 3f, 0f), new Vector3(4.2f, 0.1f, 4.2f), PrimitiveType.Cylinder,
                new Vector3(0f, RoomHeight - 4f, 0f), new Color(1f, 0.93f, 0.8f), 75f, 65f);
            Lamp(context, "DeskLampLight", new Vector3(21.6f, DeskTop + 3.3f, 2.6f), Vector3.one * 0.5f, PrimitiveType.Sphere,
                new Vector3(21.2f, DeskTop + 2.6f, 3.2f), new Color(1f, 0.82f, 0.55f), 22f, 20f);
            Lamp(context, "FloorLampLight", new Vector3(-6f, 14.2f, -22f), Vector3.one * 1f, PrimitiveType.Sphere,
                new Vector3(-6f, 13f, -21f), new Color(1f, 0.8f, 0.55f), 40f, 30f);
            MapPiecePlacer.CreateBareLight(context.lights, "MoonLight", new Vector3(-2f, 24f, Half - 4f), new Color(0.55f, 0.65f, 1f), 30f, 40f);

            // 구석까지 어둡지 않도록 그림자 없는 채움 빛을 둔다.
            var fill = new GameObject("RoomFill");
            fill.transform.SetParent(context.lights, false);
            fill.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            Light fillLight = fill.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = new Color(1f, 0.94f, 0.85f);
            fillLight.intensity = 0.35f;
            fillLight.shadows = LightShadows.None;
            fillLight.lightmapBakeType = LightmapBakeType.Realtime;
            fill.AddComponent<MapLight>();
        }

        /// <summary>사냥 시간에 빛만 꺼지고 전구가 빛나 보이지 않도록 전구와 조명을 한 MapLight로 묶는다.</summary>
        private static void Lamp(Context context, string name, Vector3 bulbCenter, Vector3 bulbScale, PrimitiveType bulbShape, Vector3 lightPosition,
            Color color, float intensity, float range)
        {
            var lamp = new GameObject(name);
            lamp.transform.SetParent(context.lights, false);
            lamp.transform.position = bulbCenter;

            GameObject bulb = GameObject.CreatePrimitive(bulbShape);
            bulb.name = "Bulb";
            Object.DestroyImmediate(bulb.GetComponent<Collider>());
            bulb.transform.SetParent(lamp.transform, false);
            bulb.transform.localScale = bulbShape == PrimitiveType.Cylinder ? new Vector3(bulbScale.x, bulbScale.y * 0.5f, bulbScale.z) : bulbScale;
            Renderer bulbRenderer = bulb.GetComponent<Renderer>();
            bulbRenderer.sharedMaterial = context.materials.lampOn;
            bulbRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var lightObject = new GameObject("PointLight");
            lightObject.transform.SetParent(lamp.transform, false);
            lightObject.transform.position = lightPosition;
            Light point = lightObject.AddComponent<Light>();
            point.type = LightType.Point;
            point.color = color;
            point.intensity = intensity;
            point.range = range;
            point.shadows = LightShadows.None;
            point.lightmapBakeType = LightmapBakeType.Realtime;

            MapLight mapLight = lamp.AddComponent<MapLight>();
            TestEnvironmentSetupTool.SetField(mapLight, "bulbRenderer", bulbRenderer);
            TestEnvironmentSetupTool.SetField(mapLight, "onMaterial", context.materials.lampOn);
            TestEnvironmentSetupTool.SetField(mapLight, "offMaterial", context.materials.lampOff);
        }

        /// <summary>높이 차를 살리도록 스폰·전원 장치를 바닥과 가구 위로 나누고 가드는 문 앞에서 시작하게 한다.</summary>
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

        /// <summary>실내라 하늘을 끄고 어두운 환경광으로 밤의 방 분위기를 낸다.</summary>
        private static void ApplyAtmosphere(Scene scene)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.17f, 0.15f, 0.15f);
            RenderSettings.fog = false;
            SceneLightingSetup.Apply(scene);
        }

        #endregion

        #region 확인용 결과

        /// <summary>
        /// Unity를 열지 않고도 결과를 확인할 수 있도록 배치도(천장 숨김)와 구역별 장면을 찍어 Temp/GiantRoom에 저장한다.
        /// </summary>
        private static void CapturePreviews(Context context, Bounds playArea)
        {
            const float eye = 1.7f;
            var shots = new List<MapPreviewCapture.Shot>
            {
                new MapPreviewCapture.Shot("01_Overview", new Vector3(18f, 26f, -22f), new Vector3(-6f, 4f, 8f)),
                new MapPreviewCapture.Shot("02_FloorFromDoor", new Vector3(4f, eye, -20f), new Vector3(0f, 6f, 10f)),
                new MapPreviewCapture.Shot("03_DeskCord", new Vector3(12f, eye, 9f), new Vector3(19f, 5f, 9f)),
                new MapPreviewCapture.Shot("04_DeskTop", new Vector3(19.5f, DeskTop + eye, 11f), new Vector3(23f, DeskTop, 3f)),
                new MapPreviewCapture.Shot("05_BookshelfRoute", new Vector3(4f, eye, 12f), new Vector3(4f, 9f, 23f)),
                new MapPreviewCapture.Shot("06_BedTop", new Vector3(-12f, BedTop + eye, 6f), new Vector3(-20f, BedTop, 20f)),
                new MapPreviewCapture.Shot("07_TableBookStairs", new Vector3(8f, eye, -14f), new Vector3(-6f, 2f, -8f)),
                new MapPreviewCapture.Shot("08_CoffeeTable", new Vector3(-9f, CoffeeTableTop + eye, -3.5f), new Vector3(-12f, CoffeeTableTop, -12f)),
                new MapPreviewCapture.Shot("09_ToyCorner", new Vector3(6f, eye, -12f), new Vector3(14f, 1f, -21f)),
                new MapPreviewCapture.Shot("10_UnderDesk", new Vector3(15f, eye, 8f), new Vector3(24f, 1f, 7f)),
            };
            MapPreviewCapture.Capture(PreviewFolder, playArea, context.roofs.Where(r => r != null).ToList(), shots);
        }

        /// <summary>
        /// 배치 좌표를 다듬을 때 에셋 실제 크기와 보고 내용을 참고하도록 Temp/GiantRoom/sizes.txt에 남긴다.
        /// </summary>
        private static void WriteReport(Context context, int converted, int colliders, int props, int skipped)
        {
            Directory.CreateDirectory(PreviewFolder);
            string text = $"변신 사물 {props}, 제외 {skipped}, 충돌체 추가 {colliders}, 머티리얼 변환 {converted}, 없는 에셋 {context.placer.MissingCount}\n" +
                          $"\n[에셋 크기 (×{Scale} 적용 후, 가로 x 높이 x 깊이, 회전 전)]\n" + context.placer.DescribeSizes();
            File.WriteAllText(Path.Combine(PreviewFolder, "sizes.txt"), text);
        }

        #endregion

        /// <summary>거대한 방 머티리얼을 없으면 URP Lit으로 만든다.</summary>
        private sealed class RoomMaterials
        {
            private const string Folder = "Assets/_Project/Art/MapKit/Materials/GiantRoom";

            public Material floor, floorSeam, wall, ceiling, baseboard, glass, nightSky, cord, plug, lampShade, screen, lampOn, lampOff;
            public Material[] bookCovers;

            /// <summary>소품 FBX 머티리얼을 맵 머티리얼로 바꿔 끼우는 표.</summary>
            public Dictionary<string, Material> PropRemap { get; private set; }

            /// <summary>
            /// 빌더를 다시 실행해도 GUID가 유지되어 씬 참조가 끊기지 않도록 있으면 불러오고, 없을 때만 URP Lit으로 만든다.
            /// </summary>
            public static RoomMaterials LoadOrCreate()
            {
                var factory = new MapMaterialFactory(Folder);
                var result = new RoomMaterials
                {
                    floor = factory.Opaque("M_GR_RoomFloor", new Color(0.55f, 0.38f, 0.24f), 0f, 0.45f),
                    floorSeam = factory.Opaque("M_GR_RoomFloorSeam", new Color(0.3f, 0.2f, 0.12f), 0f, 0.3f),
                    wall = factory.Opaque("M_GR_RoomWall", new Color(0.62f, 0.7f, 0.76f), 0f, 0.2f),
                    ceiling = factory.Opaque("M_GR_RoomCeiling", new Color(0.88f, 0.88f, 0.86f), 0f, 0.15f),
                    baseboard = factory.Opaque("M_GR_RoomBaseboard", new Color(0.92f, 0.92f, 0.9f), 0f, 0.4f),
                    glass = factory.Transparent("M_GR_RoomGlass", new Color(0.6f, 0.72f, 0.85f, 0.18f)),
                    nightSky = factory.Emissive("M_GR_RoomNightSky", new Color(0.05f, 0.07f, 0.16f), new Color(0.08f, 0.12f, 0.3f)),
                    cord = factory.Opaque("M_GR_Cord", new Color(0.08f, 0.08f, 0.09f), 0f, 0.45f),
                    plug = factory.Opaque("M_GR_Plug", new Color(0.9f, 0.9f, 0.88f), 0f, 0.4f),
                    lampShade = factory.Opaque("M_GR_LampShadeOff", new Color(0.92f, 0.88f, 0.78f), 0f, 0.3f),
                    screen = factory.Emissive("M_GR_ScreenLit", new Color(0.2f, 0.45f, 0.85f), new Color(0.25f, 0.55f, 1f) * 1.5f),
                    bookCovers = new[]
                    {
                        factory.Opaque("M_GR_BookRed", new Color(0.7f, 0.15f, 0.12f), 0f, 0.35f),
                        factory.Opaque("M_GR_BookBlue", new Color(0.14f, 0.3f, 0.65f), 0f, 0.35f),
                        factory.Opaque("M_GR_BookGreen", new Color(0.15f, 0.5f, 0.28f), 0f, 0.35f),
                        factory.Opaque("M_GR_BookYellow", new Color(0.9f, 0.72f, 0.18f), 0f, 0.35f),
                        factory.Opaque("M_GR_BookPurple", new Color(0.42f, 0.24f, 0.6f), 0f, 0.35f),
                        factory.Opaque("M_GR_BookBrown", new Color(0.4f, 0.26f, 0.16f), 0f, 0.35f),
                    },
                    lampOn = AssetDatabase.LoadAssetAtPath<Material>($"{TestEnvironmentSetupTool.MaterialFolder}/M_LampOn.mat"),
                    lampOff = AssetDatabase.LoadAssetAtPath<Material>($"{TestEnvironmentSetupTool.MaterialFolder}/M_LampOff.mat"),
                };
                result.PropRemap = new Dictionary<string, Material>
                {
                    { "M_GR_Glow", result.lampShade },
                    { "M_GR_Screen", result.screen },
                };
                AssetDatabase.SaveAssets();
                return result;
            }
        }
    }
}
