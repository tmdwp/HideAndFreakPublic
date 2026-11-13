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

    /// <summary>지하 2호선과 지상 1호선이 이어진 "환승역" 맵을 항상 같은 결과로 만든다.</summary>
    public static class TransferStationBuilder
    {
        private const string MapName = "Transfer Station";
        private const string SceneName = "Map_TransferStation";
        internal const string PreviewFolder = "Temp/TransferStation";

        private const float MinX = -32f, MaxX = 32f, MinZ = -14f, MaxZ = 14f;

        // 층 높이 (바닥 윗면)
        private const float B2 = -10f;          // 2호선 승강장
        private const float B2Track = -11f;     // 2호선 선로 바닥
        private const float B1 = -5f;           // 2호선 대합실
        private const float GroundTop = 0f;     // 1호선 선로 바닥(지상)
        private const float L1Platform = 1f;    // 1호선 승강장
        private const float Bridge = 6f;        // 1호선 대합실(다리)
        private const float Roof = 11f;
        private const float SlabThickness = 0.4f;

        // 2호선 승강장 홀 (z ±10), 섬식 승강장 z ±4, 선로 중심 z ±5.7
        private const float B2HallHalf = 10f;
        private const float B2PlatformHalf = 4f;
        private const float B2TrackZ = 5.7f;

        // 1호선 선로와 승강장 위치.
        private static readonly float[] L1TrackZs = { -12f, -2f, 2f, 12f };
        private const float P1Center = -7f, P2Center = 7f, L1PlatformHalf = 3f, L1PlatformEndX = 30f;
        private const float BridgeHalfX = 8f;

        // 계단: 단 높이 0.25m, 단 길이 0.35m, 폭 2.4m
        private const float StepRise = 0.25f, StepRun = 0.35f, StairWidth = 2.4f;

        // B1 개찰구 줄과 방 구역.
        private const float GateX = 24f;
        private const float RoomLine = 9f;
        private const float WallThickness = 0.2f;

        private static readonly Vector3[] MannequinSpawns =
        {
            new Vector3(-28f, B2, 0f), new Vector3(-24f, B2, 2.6f), new Vector3(-6f, B2, -2.6f), new Vector3(4f, B2, 2.6f),
            new Vector3(-28f, B1, 10f), new Vector3(-28f, B1, -10f), new Vector3(-18f, B1, 11.5f), new Vector3(-9.5f, B1, 11.5f),
            new Vector3(-18f, B1, -11.5f), new Vector3(-16f, B1, 4f)
        };

        private static readonly Vector3[] GuardSpawns =
        {
            new Vector3(24f, L1Platform, 7f), new Vector3(24f, L1Platform, -7f), new Vector3(2f, Bridge, 11f),
            new Vector3(2f, Bridge, -11f), new Vector3(5f, Bridge, 0f)
        };

        private static readonly Vector3[] GeneratorSpots =
        {
            new Vector3(-16f, B2, 2.6f), new Vector3(16f, B2, -2.6f), new Vector3(28f, B2, 0f),
            new Vector3(-28f, B1, 0f), new Vector3(28f, B1, 10f), new Vector3(17f, B1, -11.5f), new Vector3(-20f, B1, -11.5f),
            new Vector3(-24f, L1Platform, -7f), new Vector3(-24f, L1Platform, 7f), new Vector3(20f, L1Platform, 7f),
            new Vector3(-4f, Bridge, 11f), new Vector3(4f, Bridge, -11f)
        };

        /// <summary>
        /// 만드는 동안 함께 쓰는 상태.
        /// </summary>
        private sealed class Context : MapLayoutTools
        {
            public StationMaterials materials;
            public Transform lights;
            public readonly List<Renderer> roof = new List<Renderer>();
        }

        #region 메뉴

        /// <summary>씬을 덮어쓰므로 작업 중인 씬을 잃지 않도록 저장 여부를 먼저 묻고, 배치 확인용 사진까지 남긴다.</summary>
        [MenuItem(EditorMenus.Root + "맵 생성/환승역 (Map_TransferStation)")]
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
        /// <param name="capturePreview">true면 Temp/TransferStation에 배치도·장면 사진을 남긴다.</param>
        public static void BuildScene(string scenePath, bool capturePreview)
        {
            // 빌더는 프리팹을 놓으므로 빠진 맵 물체 프리팹을 먼저 만든다.
            MapObjectPrefabBuilder.EnsurePrefabs();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Transform mapRoot = new GameObject(MapAuthoringTools.MapRootName).transform;

            StationMaterials materials = StationMaterials.LoadOrCreate();
            var context = new Context
            {
                materials = materials,
                placer = new MapPiecePlacer(materials.KitRemap),
                environment = MapLayoutTools.CreateChild(mapRoot, MapAuthoringTools.EnvironmentName),
                lights = MapLayoutTools.CreateChild(mapRoot, "Lights")
            };

            BuildLine2Platform(context);
            BuildLine2Concourse(context);
            BuildLine1Platforms(context);
            BuildLine1Concourse(context);
            BuildTrains(context);
            FurnishLine2Platform(context);
            FurnishLine2Concourse(context);
            FurnishLine1(context);

            // 정리가 등록 컴포넌트를 지우지 않도록 변신 사물 등록을 마지막에 한다.
            MapSceneBuilder.CleanEnvironment(context.environment);
            ApplyPropMaterials(context.environment, materials.PropRemap);
            int converted = UrpMaterialConverter.ConvertRenderers(context.environment.GetComponentsInChildren<Renderer>(true));
            int colliders = MapSceneBuilder.AddMissingColliders(context.environment);
            context.placer.RegisterCandidates(out int propCount, out int skipped);
            Physics.SyncTransforms();

            BuildLights(context);
            CreatePoints(mapRoot);

            NavMeshSurface surface = mapRoot.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            // 경계는 가장 낮은 바닥(B2 선로)부터 지붕까지
            var playArea = new Bounds(new Vector3(0f, (B2Track + Roof) / 2f, 0f), new Vector3(MaxX - MinX, Roof - B2Track, MaxZ - MinZ));
            float ceilingHeight = MapSceneBuilder.CreateBoundary(playArea, context.environment);

            MapInfo mapInfo = mapRoot.gameObject.AddComponent<MapInfo>();
            var info = new SerializedObject(mapInfo);
            info.FindProperty("mapName").stringValue = MapName;
            info.FindProperty("ceilingHeight").floatValue = ceilingHeight;
            info.ApplyModifiedPropertiesWithoutUndo();
            // 대기 화면: 지상 1호선 승강장 서쪽 위에서 다리와 선로를 내려다본다.
            var overviewPosition = new Vector3(-30f, 8.5f, -12.5f);
            GameSystemsSceneBuilder.SetOverview(mapInfo, overviewPosition, Quaternion.LookRotation(new Vector3(0f, 0f, 4f) - overviewPosition));

            ApplyAtmosphere(scene);

            // 최적화(오클루전 굽기)에 저장된 경로가 필요하므로 먼저 저장한다.
            EditorSceneManager.SaveScene(scene, scenePath);
            MapOptimizer.Optimize(scene, indoor: true);
            if (capturePreview)
                CapturePreviews(context, playArea);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            WriteReport(context, converted, colliders, propCount, skipped);
            Debug.Log($"[TransferStationBuilder] 환승역 완료: {scenePath}\n" +
                      $"변신 사물 {propCount}개(크기·구성으로 제외 {skipped}개), 충돌체 추가 {colliders}개, 머티리얼 변환 {converted}개, " +
                      $"없는 에셋 {context.placer.MissingCount}개. 에셋 크기 목록: {PreviewFolder}/sizes.txt");
        }

        #region 구조: B2 2호선 승강장

        /// <summary>B2 2호선 승강장 층의 구조를 만든다.</summary>
        private static void BuildLine2Platform(Context context)
        {
            Transform group = context.Group("B2_Line2Platform");
            StationMaterials materials = context.materials;

            Slab(group, "TrackBed", XzRect(MinX, MaxX, -B2HallHalf, B2HallHalf), B2Track, SlabThickness, materials.ballast);
            Slab(group, "Platform", XzRect(MinX, MaxX, -B2PlatformHalf, B2PlatformHalf), B2, B2 - B2Track, materials.platformFloor);
            foreach (float side in new[] { -1f, 1f })
            {
                MapLayoutTools.Box(group, "SafetyLine", new Vector3(0f, B2 + 0.005f, side * (B2PlatformHalf - 0.45f)), new Vector3(MaxX - MinX, 0.01f, 0.12f),
                    materials.safetyYellow, collider: false);
                Tracks(group, materials, side * B2TrackZ, B2Track);
            }

            // 홀 벽: 남·북은 노선 색 띠, 동·서는 선로 자리에 터널 입구
            float wallTop = B1 - SlabThickness;
            foreach (float side in new[] { -1f, 1f })
            {
                WallRun(context, group, alongX: true, side * B2HallHalf, MinX, MaxX, B2Track, wallTop, materials.wall);
                Stripe(group, materials.line2, alongX: true, side * (B2HallHalf - 0.11f), MinX, MaxX, B2 + 1.3f);
                float end = side * MaxX;
                WallRun(context, group, alongX: false, end, -B2HallHalf, B2HallHalf, B2Track, wallTop, materials.wall,
                    Gap.Opening(-7f, 6f, 4f), Gap.Opening(7f, 6f, 4f));
                TunnelMouth(context, group, side, -7f, 6f, B2Track, 4f);
                TunnelMouth(context, group, side, 7f, 6f, B2Track, 4f);
            }

            // 선로 점검 계단.
            foreach (float x in new[] { -29f, 29f })
            {
                foreach (float side in new[] { -1f, 1f })
                    Stairs(group, materials.concrete, new Vector3(x, B2Track, side * (B2PlatformHalf + 4 * StepRun)), new Vector3(0f, 0f, -side), B2 - B2Track, 1.6f);
            }

            // B1으로 올라가는 계단.
            Stairs(group, materials.stair, new Vector3(-21f, B2, 0f), Vector3.right, B1 - B2, StairWidth);
            Stairs(group, materials.stair, new Vector3(21f, B2, 0f), Vector3.left, B1 - B2, StairWidth);
        }

        #endregion

        #region 구조: B1 2호선 대합실

        /// <summary>B1 대합실 층의 구조를 만든다.</summary>
        private static void BuildLine2Concourse(Context context)
        {
            Transform group = context.Group("B1_Line2Concourse");
            StationMaterials materials = context.materials;

            Rect holeA = XzRect(-21f, -14f, -StairWidth / 2f - 0.1f, StairWidth / 2f + 0.1f);
            Rect holeB = XzRect(14f, 21f, -StairWidth / 2f - 0.1f, StairWidth / 2f + 0.1f);
            Slab(group, "Floor", XzRect(MinX, MaxX, MinZ, MaxZ), B1, SlabThickness, materials.floor, holeA, holeB);
            HoleRailing(group, materials.rail, holeA, B1, openEnd: MapSide.East);
            HoleRailing(group, materials.rail, holeB, B1, openEnd: MapSide.West);

            // 둘레 벽과 닫힌 출구(동·서 끝)
            float top = GroundTop - SlabThickness;
            foreach (float side in new[] { -1f, 1f })
            {
                WallRun(context, group, alongX: true, side * MaxZ, MinX, MaxX, B1, top, materials.wall);
                WallRun(context, group, alongX: false, side * MaxX, MinZ, MaxZ, B1, top, materials.wall);
                foreach (float z in new[] { -6f, 6f })
                    ExitShutter(group, materials, side, z);
            }

            // 개찰구.
            foreach (float x in new[] { -GateX, GateX })
                GateLine(group, materials, alongX: false, x, -RoomLine, RoomLine, B1);

            // 역무실·화장실·기계실·창고.
            WallRun(context, group, alongX: true, RoomLine, -GateX, -12f, B1, top, materials.wall, Gap.Door(-14f), Gap.Window(-20f));
            WallRun(context, group, alongX: false, -GateX, RoomLine, MaxZ, B1, top, materials.wall);
            WallRun(context, group, alongX: false, -12f, RoomLine, MaxZ, B1, top, materials.wall);
            WallRun(context, group, alongX: true, RoomLine, -12f, -2f, B1, top, materials.tile, Gap.Door(-10f), Gap.Door(-4f));
            WallRun(context, group, alongX: false, -7f, RoomLine, MaxZ, B1, top, materials.tile);
            WallRun(context, group, alongX: false, -2f, RoomLine, MaxZ, B1, top, materials.tile);
            WallRun(context, group, alongX: true, -RoomLine, -GateX, -10f, B1, top, materials.wall, Gap.Door(-12f));
            WallRun(context, group, alongX: false, -GateX, MinZ, -RoomLine, B1, top, materials.wall);
            WallRun(context, group, alongX: false, -10f, MinZ, -RoomLine, B1, top, materials.wall);
            WallRun(context, group, alongX: true, -RoomLine, 10f, GateX, B1, top, materials.wall, Gap.Door(12f));
            WallRun(context, group, alongX: false, 10f, MinZ, -RoomLine, B1, top, materials.wall);
            WallRun(context, group, alongX: false, GateX, MinZ, -RoomLine, B1, top, materials.wall);

            // 1호선 승강장으로 올라가는 환승 계단.
            Stairs(group, materials.stair, new Vector3(-6f, B1, -6.5f), Vector3.right, L1Platform - B1, StairWidth);
            Stairs(group, materials.stair, new Vector3(6f, B1, 6.5f), Vector3.left, L1Platform - B1, StairWidth);

            // 기둥 (계단을 피해서)
            foreach (Vector2 p in new[] { new Vector2(-8f, -4f), new Vector2(8f, 4f), new Vector2(-8f, 4f), new Vector2(8f, -4f), new Vector2(0f, 0f) })
                MapLayoutTools.Box(group, "Column", new Vector3(p.x, (B1 + top) / 2f, p.y), new Vector3(0.7f, top - B1, 0.7f), materials.wall);
        }

        #endregion

        #region 구조: G 1호선 승강장 · U 1호선 대합실(다리)

        /// <summary>지상 1호선 승강장 층의 구조를 만든다.</summary>
        private static void BuildLine1Platforms(Context context)
        {
            Transform group = context.Group("G_Line1Platforms");
            StationMaterials materials = context.materials;

            Rect holeC = XzRect(-6f, 2.4f, -6.5f - StairWidth / 2f - 0.1f, -6.5f + StairWidth / 2f + 0.1f);
            Rect holeD = XzRect(-2.4f, 6f, 6.5f - StairWidth / 2f - 0.1f, 6.5f + StairWidth / 2f + 0.1f);
            Slab(group, "Ground", XzRect(MinX, MaxX, MinZ, MaxZ), GroundTop, SlabThickness, materials.ballast, holeC, holeD);
            // 바닥판 아랫면이 보이지 않도록 B1 천장판을 둔다.
            Slab(group, "B1Ceiling", XzRect(MinX, MaxX, MinZ, MaxZ), GroundTop - SlabThickness, 0.02f, materials.ceiling, holeC, holeD);

            foreach (float zc in new[] { P1Center, P2Center })
            {
                Rect platform = XzRect(-L1PlatformEndX, L1PlatformEndX, zc - L1PlatformHalf, zc + L1PlatformHalf);
                Slab(group, "Platform", platform, L1Platform, L1Platform - GroundTop, materials.platformFloor, holeC, holeD);
                foreach (float edge in new[] { -1f, 1f })
                {
                    MapLayoutTools.Box(group, "SafetyLine", new Vector3(0f, L1Platform + 0.005f, zc + edge * (L1PlatformHalf - 0.45f)),
                        new Vector3(L1PlatformEndX * 2f, 0.01f, 0.12f), materials.safetyYellow, collider: false);
                }
                // 승강장 끝 계단 (선로로 내려감)
                Stairs(group, materials.concrete, new Vector3(L1PlatformEndX + 4 * StepRun, GroundTop, zc), Vector3.left, L1Platform - GroundTop, StairWidth);
                Stairs(group, materials.concrete, new Vector3(-L1PlatformEndX - 4 * StepRun, GroundTop, zc), Vector3.right, L1Platform - GroundTop, StairWidth);
            }
            HoleRailing(group, materials.rail, holeC, L1Platform, openEnd: MapSide.East);
            HoleRailing(group, materials.rail, holeD, L1Platform, openEnd: MapSide.West);

            foreach (float z in L1TrackZs)
                Tracks(group, materials, z, GroundTop);

            // 둘레 벽: 남·북은 1호선 색 띠, 동·서는 선로 자리 터널 입구 (A, B+C, D)
            foreach (float side in new[] { -1f, 1f })
            {
                WallRun(context, group, alongX: true, side * MaxZ, MinX, MaxX, GroundTop, Roof, materials.wall);
                Stripe(group, materials.line1, alongX: true, side * (MaxZ - 0.11f), MinX, MaxX, 2.4f);
                WallRun(context, group, alongX: false, side * MaxX, MinZ, MaxZ, GroundTop, Roof, materials.wall,
                    Gap.Opening(-12f, 3.8f, 4.5f), Gap.Opening(0f, 8f, 4.5f), Gap.Opening(12f, 3.8f, 4.5f));
                TunnelMouth(context, group, side, -12f, 3.8f, GroundTop, 4.5f);
                TunnelMouth(context, group, side, 0f, 8f, GroundTop, 4.5f);
                TunnelMouth(context, group, side, 12f, 3.8f, GroundTop, 4.5f);
            }

            // 지붕 (배치도에서 숨김)
            GameObject roof = MapLayoutTools.Box(group, "Roof", new Vector3(0f, Roof + 0.2f, 0f), new Vector3(MaxX - MinX, 0.4f, MaxZ - MinZ), materials.ceiling);
            context.roof.Add(roof.GetComponent<Renderer>());
            foreach (float x in new[] { -24f, -16f, 16f, 24f })
            {
                GameObject beam = MapLayoutTools.Box(group, "RoofBeam", new Vector3(x, Roof - 0.3f, 0f), new Vector3(0.4f, 0.6f, MaxZ - MinZ), materials.trim);
                context.roof.Add(beam.GetComponent<Renderer>());
            }
        }

        /// <summary>선로 위 다리형 대합실을 만든다.</summary>
        private static void BuildLine1Concourse(Context context)
        {
            Transform group = context.Group("U_Line1Concourse");
            StationMaterials materials = context.materials;

            Slab(group, "Deck", XzRect(-BridgeHalfX, BridgeHalfX, MinZ, MaxZ), Bridge, 0.3f, materials.floor);
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * BridgeHalfX;
                // 계단 자리(z ±7)를 비운 난간
                foreach ((float from, float to) in new[] { (MinZ, P1Center - StairWidth / 2f), (P1Center + StairWidth / 2f, P2Center - StairWidth / 2f), (P2Center + StairWidth / 2f, MaxZ) })
                    RailingRun(group, materials.rail, alongX: false, x, from, to, Bridge);

                // 승강장에서 다리로 오르는 계단.
                foreach (float zc in new[] { P1Center, P2Center })
                    Stairs(group, materials.stair, new Vector3(side * (BridgeHalfX + 20 * StepRun), L1Platform, zc), new Vector3(-side, 0f, 0f), Bridge - L1Platform, StairWidth);

                // 다리 기둥.
                foreach (float z in new[] { -13.5f, 0f, 13.5f })
                    MapLayoutTools.Box(group, "Column", new Vector3(side * (BridgeHalfX - 0.6f), (GroundTop + Bridge - 0.3f) / 2f, z),
                        new Vector3(0.6f, Bridge - 0.3f - GroundTop, 0.6f), materials.trim);
            }

            // 매표소와 보관함 구역을 나누는 개찰구 줄.
            GateLine(group, materials, alongX: true, 0f, -BridgeHalfX, BridgeHalfX, Bridge);

            // 매표소.
            const float boothTop = Bridge + 3f;
            WallRun(context, group, alongX: true, 9f, -6f, -1f, Bridge, boothTop, materials.wall, Gap.Window(-3.5f));
            WallRun(context, group, alongX: false, -1f, 9f, 13f, Bridge, boothTop, materials.wall, Gap.Door(11f));
            WallRun(context, group, alongX: false, -6f, 9f, 13f, Bridge, boothTop, materials.wall);
            WallRun(context, group, alongX: true, 13f, -6f, -1f, Bridge, boothTop, materials.wall);
            MapLayoutTools.Box(group, "BoothRoof", new Vector3(-3.5f, boothTop + 0.1f, 11f), new Vector3(5.2f, 0.2f, 4.2f), materials.ceiling);
        }

        #endregion

        #region 전동차

        /// <summary>2호선과 1호선 전동차를 승강장 쪽 문이 열린 채로 놓는다.</summary>
        private static void BuildTrains(Context context)
        {
            Transform group = context.Group("Trains");
            float half = CarLength / 2f + CarGap / 2f;

            // 2호선: 선로 z +5.7, 승강장은 −z 쪽
            BuildTrain(context, group, half, B2TrackZ, B2, -1f, context.materials.line2);
            // 1호선: 선로 B z −2, 승강장 P1은 −z 쪽
            BuildTrain(context, group, half, L1TrackZs[1], L1Platform, -1f, context.materials.line1);

            const float floorTop = 0.02f;
            context.Prop(group, Kit.Box, -half - 4.8f, B2TrackZ + 0.65f, 15f, 0.35f, baseY: B2 + floorTop);
            context.Prop(group, Kit.BoxStack, half + 5.2f, B2TrackZ + 0.6f, 0f, 0.7f, baseY: B2 + floorTop);
            context.Prop(group, Subway.Suitcase, half + 5.1f, L1TrackZs[1] + 0.6f, 90f, 0.95f, baseY: L1Platform + floorTop);
            context.Prop(group, Subway.Suitcase, -half - 2.2f, B2TrackZ + 0.65f, -70f, 0.95f, baseY: B2 + floorTop);
            context.Prop(group, Kit.Bucket, -half + 1.6f, L1TrackZs[1] - 0.3f, 0f, 0.3f, baseY: L1Platform + floorTop);
        }

        private const float CarLength = 12f, CarWidth = 3.2f, CarHeight = 2.33f, CarGap = 0.6f, CarDoorWidth = 1.3f;
        private static readonly float[] CarDoorOffsets = { -3.6f, 0f, 3.6f };

        /// <summary>연결 통로로 이어진 2량 편성을 만든다.</summary>
        private static void BuildTrain(Context context, Transform parent, float half, float cz, float floorY, float doorSide, Material band)
        {
            Transform train = MapLayoutTools.CreateChild(parent, "Train");
            BuildCar(context, train, -half, cz, floorY, doorSide, band, closedWest: true, closedEast: false);
            BuildCar(context, train, half, cz, floorY, doorSide, band, closedWest: false, closedEast: true);

            Material rubber = context.materials.rubber;
            MapLayoutTools.Box(train, "GangwayFloor", new Vector3(0f, floorY - 0.04f, cz), new Vector3(CarGap, 0.12f, 1.1f), rubber);
            MapLayoutTools.Box(train, "GangwaySide", new Vector3(0f, floorY + 1.02f, cz + 0.6f), new Vector3(CarGap, 2.05f, 0.06f), rubber);
            MapLayoutTools.Box(train, "GangwaySide", new Vector3(0f, floorY + 1.02f, cz - 0.6f), new Vector3(CarGap, 2.05f, 0.06f), rubber);
            MapLayoutTools.Box(train, "GangwayTop", new Vector3(0f, floorY + 2.1f, cz), new Vector3(CarGap, 0.1f, 1.26f), rubber);
        }

        /// <summary>전동차 한 량을 기본 도형으로 만든다.</summary>
        private static void BuildCar(Context context, Transform parent, float centerX, float cz, float floorY, float doorSide, Material band,
            bool closedWest, bool closedEast)
        {
            Transform car = MapLayoutTools.CreateChild(parent, "TrainCar");
            StationMaterials materials = context.materials;
            float halfLength = CarLength / 2f;
            float halfWidth = CarWidth / 2f;

            MapLayoutTools.Box(car, "Floor", new Vector3(centerX, floorY - 0.04f, cz), new Vector3(CarLength, 0.12f, CarWidth), materials.carFloor);
            MapLayoutTools.Box(car, "Underframe", new Vector3(centerX, floorY - 0.55f, cz), new Vector3(CarLength - 1f, 0.9f, CarWidth - 0.6f), materials.trim);
            MapLayoutTools.Box(car, "Roof", new Vector3(centerX, floorY + CarHeight + 0.075f, cz), new Vector3(CarLength, 0.15f, CarWidth), materials.carBody);
            MapLayoutTools.Box(car, "LightStrip", new Vector3(centerX, floorY + CarHeight - 0.02f, cz), new Vector3(CarLength - 1f, 0.04f, 0.3f), materials.lightStrip, collider: false);

            foreach (float side in new[] { -1f, 1f })
                BuildCarSide(context, car, centerX, cz + side * (halfWidth - 0.05f), floorY, side, doorsOpen: Mathf.Approximately(side, doorSide), band);
            BuildCarEnd(context, car, centerX - halfLength + 0.05f, cz, floorY, closedWest);
            BuildCarEnd(context, car, centerX + halfLength - 0.05f, cz, floorY, closedEast);

            // 긴 의자: 문 사이 구간을 따라 양쪽 벽에 붙인다.
            var starts = new List<float> { -halfLength + 0.25f };
            var ends = new List<float>();
            foreach (float door in CarDoorOffsets)
            {
                ends.Add(door - CarDoorWidth / 2f - 0.15f);
                starts.Add(door + CarDoorWidth / 2f + 0.15f);
            }
            ends.Add(halfLength - 0.25f);
            for (int i = 0; i < starts.Count; i++)
            {
                float length = ends[i] - starts[i];
                if (length < 0.6f)
                    continue;
                float x = centerX + (starts[i] + ends[i]) / 2f;
                foreach (float side in new[] { -1f, 1f })
                {
                    float wallZ = cz + side * (halfWidth - 0.1f);
                    MapLayoutTools.Box(car, "Seat", new Vector3(x, floorY + 0.24f, wallZ - side * 0.25f), new Vector3(length, 0.44f, 0.5f), materials.seat);
                    MapLayoutTools.Box(car, "SeatBack", new Vector3(x, floorY + 0.72f, wallZ - side * 0.04f), new Vector3(length, 0.52f, 0.08f), materials.seat);
                }
            }

            foreach (float offset in CarDoorOffsets)
            {
                GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Pole";
                pole.transform.SetParent(car, false);
                pole.transform.position = new Vector3(centerX + offset, floorY + CarHeight / 2f, cz);
                pole.transform.localScale = new Vector3(0.05f, CarHeight / 2f, 0.05f);
                pole.GetComponent<Renderer>().sharedMaterial = materials.rail;
            }

            foreach (float offset in new[] { -3f, 3f })
                MapPiecePlacer.CreateBareLight(context.lights, "TrainLight", new Vector3(centerX + offset, floorY + CarHeight - 0.3f, cz), new Color(0.88f, 0.95f, 1f), 1.4f, 5.5f);
        }

        /// <summary>전동차 옆면을 문 자리만 열거나 닫아 만든다.</summary>
        private static void BuildCarSide(Context context, Transform car, float centerX, float wallZ, float floorY, float outward, bool doorsOpen, Material band)
        {
            StationMaterials materials = context.materials;
            float halfLength = CarLength / 2f;
            const float thickness = 0.1f;
            const float doorTop = 2f;

            float start = -halfLength;
            foreach (float door in CarDoorOffsets.Append(halfLength + CarDoorWidth / 2f))
            {
                float end = door - CarDoorWidth / 2f;
                if (end - start > 0.01f)
                {
                    float x = centerX + (start + end) / 2f;
                    float length = end - start;
                    MapLayoutTools.Box(car, "SideLower", new Vector3(x, floorY + 0.485f, wallZ), new Vector3(length, 0.93f, thickness), materials.carBody);
                    GameObject glass = MapLayoutTools.Box(car, "SideWindow", new Vector3(x, floorY + 1.4f, wallZ), new Vector3(length, 0.9f, thickness * 0.5f), materials.glass);
                    glass.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    MapLayoutTools.Box(car, "SideUpper", new Vector3(x, floorY + (1.85f + CarHeight) / 2f, wallZ), new Vector3(length, CarHeight - 1.85f, thickness), materials.carBody);
                }
                if (door < halfLength)
                {
                    float doorX = centerX + door;
                    MapLayoutTools.Box(car, "DoorHeader", new Vector3(doorX, floorY + (doorTop + CarHeight) / 2f, wallZ), new Vector3(CarDoorWidth, CarHeight - doorTop, thickness), materials.carBody);
                    if (!doorsOpen)
                        MapLayoutTools.Box(car, "DoorClosed", new Vector3(doorX, floorY + (0.02f + doorTop) / 2f, wallZ), new Vector3(CarDoorWidth, doorTop - 0.02f, thickness), materials.door);
                }
                start = door + CarDoorWidth / 2f;
            }

            MapLayoutTools.Box(car, "LineBand", new Vector3(centerX, floorY + 0.8f, wallZ + outward * 0.06f), new Vector3(CarLength, 0.16f, 0.02f), band, collider: false);
        }

        /// <summary>
        /// 전동차 끝면: 운전실이면 막힌 벽 + 앞 유리, 아니면 가운데에 연결 통로 문틈.
        /// </summary>
        private static void BuildCarEnd(Context context, Transform car, float x, float cz, float floorY, bool closed)
        {
            StationMaterials materials = context.materials;
            const float thickness = 0.1f;
            if (closed)
            {
                MapLayoutTools.Box(car, "EndLower", new Vector3(x, floorY + 0.5f, cz), new Vector3(thickness, 0.96f, CarWidth), materials.carBody);
                GameObject glass = MapLayoutTools.Box(car, "EndWindow", new Vector3(x, floorY + 1.4f, cz), new Vector3(thickness * 0.5f, 0.84f, CarWidth - 0.4f), materials.glass);
                glass.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                MapLayoutTools.Box(car, "EndUpper", new Vector3(x, floorY + (1.82f + CarHeight) / 2f, cz), new Vector3(thickness, CarHeight - 1.82f, CarWidth), materials.carBody);
                foreach (float side in new[] { -1f, 1f })
                    MapLayoutTools.Box(car, "EndPillar", new Vector3(x, floorY + 1.4f, cz + side * (CarWidth / 2f - 0.1f)), new Vector3(thickness, 0.84f, 0.2f), materials.carBody);
                return;
            }

            const float gap = 1f;
            float sideWidth = (CarWidth - gap) / 2f;
            foreach (float side in new[] { -1f, 1f })
                MapLayoutTools.Box(car, "EndWall", new Vector3(x, floorY + CarHeight / 2f, cz + side * (gap / 2f + sideWidth / 2f)), new Vector3(thickness, CarHeight, sideWidth), materials.carBody);
            MapLayoutTools.Box(car, "EndHeader", new Vector3(x, floorY + (2.05f + CarHeight) / 2f, cz), new Vector3(thickness, CarHeight - 2.05f, gap), materials.carBody);
        }

        #endregion

        #region 소품

        /// <summary>2호선 승강장 소품을 놓고, 벤치는 선로 쪽을 보게 한다.</summary>
        private static void FurnishLine2Platform(Context context)
        {
            Transform group = context.Group("B2_Props");
            StationMaterials materials = context.materials;
            float ceiling = B1 - SlabThickness;

            foreach (float x in new[] { -28f, -8f, 8f, 28f })
                MapLayoutTools.Box(group, "Column", new Vector3(x, (B2 + ceiling) / 2f, 0f), new Vector3(0.7f, ceiling - B2, 0.7f), materials.wall);
            foreach (float x in new[] { -4f, 4f, -26f, 26f })
            {
                foreach (float z in new[] { -2.6f, 2.6f })
                    context.Prop(group, Subway.Bench, x, z, z > 0f ? 0f : 180f, 0.85f, baseY: B2);
            }
            context.Prop(group, Subway.RecycleBin, -10f, 2.7f, 0f, 1f, baseY: B2);
            context.Prop(group, Subway.RecycleBin, 10f, -2.7f, 180f, 1f, baseY: B2);
            context.Prop(group, Subway.InfoBoard, -23.5f, 0f, 90f, 2f, baseY: B2);
            context.Prop(group, Subway.InfoBoard, 23.5f, 0f, -90f, 2f, baseY: B2);
            foreach (float x in new[] { -12f, 12f })
                HangingSign(group, materials, new Vector3(x, B2, -2.6f), ceiling, materials.line2);

            context.Against(group, Subway.FireCabinet, MapSide.West, MinX, 2.5f, 1.5f, baseY: B2);
            context.Against(group, Subway.EmergencyBox, MapSide.West, MinX, -2.5f, 0.9f, baseY: B2);
            context.Against(group, Subway.FireCabinet, MapSide.East, MaxX, 2.4f, 1.5f, baseY: B2);
            context.Against(group, Kit.BoxStack, MapSide.East, MaxX, -2.4f, 0.7f, baseY: B2, yaw: 90f);
            context.Prop(group, Subway.CleaningCart, 6f, -2.8f, 0f, 1f, baseY: B2);
            context.Prop(group, Subway.WetFloorSign, 7.4f, -1.9f, 30f, 0.62f, baseY: B2);

            // 남쪽 선로: 작업용 팔레트·상자·드럼통과 고깔
            GameObject pallet = context.Prop(group, Kit.Pallet, 18f, -B2TrackZ, 90f, 0.15f, baseY: B2Track, transformable: false);
            context.Decorate(pallet, Kit.Crate, new Vector2(0f, 0.3f), 10f, 1f);
            context.Prop(group, Kit.Barrel, 20f, -B2TrackZ + 0.9f, 0f, 0.9f, baseY: B2Track);
            context.Prop(group, Kit.Cone, 15f, -B2TrackZ, 0f, 0.6f, baseY: B2Track);
            context.Prop(group, Kit.Barrel, -26f, B2TrackZ + 2.4f, 0f, 0.9f, baseY: B2Track);
        }

        /// <summary>2호선 대합실과 방들의 소품을 놓는다.</summary>
        private static void FurnishLine2Concourse(Context context)
        {
            Transform group = context.Group("B1_Props");

            // 비유료 구역 (서·동 끝)
            context.Against(group, Subway.TicketMachine, MapSide.North, MaxZ, -30.2f, 1.75f, baseY: B1);
            context.Against(group, Subway.TicketMachine, MapSide.North, MaxZ, -29.1f, 1.75f, baseY: B1);
            context.Against(group, VendingMachine, MapSide.North, MaxZ, -27.4f, 2f, baseY: B1);
            context.Against(group, Subway.CoinLocker, MapSide.South, MinZ, -29f, 1.9f, baseY: B1);
            context.Prop(group, Subway.Bench, -28f, 0f, 90f, 0.85f, baseY: B1);
            context.Prop(group, Subway.Planter, -30.8f, 10f, 0f, 1.2f, baseY: B1);
            context.Prop(group, Subway.Stanchion, -26.2f, -3.5f, 90f, 0.95f, baseY: B1);
            context.Against(group, Subway.TicketMachine, MapSide.South, MinZ, 26.9f, 1.75f, baseY: B1);
            context.Against(group, Subway.TicketMachine, MapSide.South, MinZ, 28f, 1.75f, baseY: B1);
            context.Against(group, VendingMachine, MapSide.South, MinZ, 29.7f, 2f, baseY: B1);
            context.Against(group, Subway.CoinLocker, MapSide.North, MaxZ, 28.5f, 1.9f, baseY: B1);
            context.Prop(group, Subway.RecycleBin, 26f, 2f, 90f, 1f, baseY: B1);
            context.Prop(group, Subway.Planter, 30.8f, -10f, 0f, 1.2f, baseY: B1);
            context.Prop(group, Subway.Stanchion, 26.2f, 3.5f, 90f, 0.95f, baseY: B1);

            // 유료 구역
            context.Prop(group, Subway.Bench, -16f, 6f, 0f, 0.85f, baseY: B1);
            context.Prop(group, Subway.Bench, 16f, -6f, 180f, 0.85f, baseY: B1);
            context.Prop(group, Subway.RecycleBin, -11f, -3.5f, 0f, 1f, baseY: B1);
            context.Prop(group, Subway.RecycleBin, 11f, 3.5f, 180f, 1f, baseY: B1);
            context.Against(group, Subway.AdBoard, MapSide.North, MaxZ, 5f, 2f, baseY: B1);
            context.Against(group, Subway.AdBoard, MapSide.South, MinZ, -4f, 2f, baseY: B1);
            context.Against(group, Subway.FireCabinet, MapSide.North, MaxZ, 12f, 1.5f, baseY: B1);
            context.Against(group, Subway.EmergencyBox, MapSide.South, MinZ, 4f, 0.9f, baseY: B1);
            context.Prop(group, Subway.Planter, -3f, -3f, 0f, 1.2f, baseY: B1);
            context.Prop(group, Subway.Planter, 3f, 3f, 0f, 1.2f, baseY: B1);
            context.Prop(group, Subway.WetFloorSign, 13f, -2f, 20f, 0.62f, baseY: B1);

            // 역무실: 책상은 창에 붙이고 의자·모니터는 앉는 쪽을 본다.
            foreach (float x in new[] { -21f, -16.5f })
            {
                GameObject desk = context.Prop(group, Subway.OfficeDesk, x, 10.4f, 180f, 0.74f, baseY: B1);
                context.Decorate(desk, Subway.Monitor, new Vector2(-0.2f, -0.1f), 0f, 0.45f);
                context.Prop(group, Subway.OfficeChair, x - 0.2f, 11.5f, 180f, 1f, baseY: B1);
            }
            context.Against(group, Kit.Cabinet, MapSide.West, -GateX, 12.5f, 1f, baseY: B1);
            context.Against(group, Kit.LockerRow, MapSide.North, MaxZ, -14.5f, 1.9f, baseY: B1);
            context.Against(group, Kit.Cabinet, MapSide.North, MaxZ, -22.5f, 1f, baseY: B1);

            // 화장실.
            foreach (float roomX in new[] { -12f, -7f })
            {
                foreach (float offset in new[] { 1.5f, 3.5f })
                {
                    context.Against(group, Subway.Toilet, MapSide.North, MaxZ, roomX + offset, 0.8f, baseY: B1);
                    MapLayoutTools.Box(group, "StallPartition", new Vector3(roomX + offset + 1f, B1 + 0.95f, MaxZ - 0.85f), new Vector3(0.05f, 1.7f, 1.5f), context.materials.tile);
                }
            }
            context.Against(group, Subway.WashBasin, MapSide.East, -7f, 10.6f, 1.6f, baseY: B1);
            context.Against(group, Subway.WashBasin, MapSide.East, -2f, 10.6f, 1.6f, baseY: B1);
            context.Prop(group, Subway.WetFloorSign, -4.2f, 9.8f, -15f, 0.62f, baseY: B1);
            context.Prop(group, Kit.Bucket, -3f, 9.8f, 0f, 0.3f, baseY: B1);

            // 기계실.
            context.Against(group, Subway.AirHandler, MapSide.South, MinZ, -21f, 1.85f, baseY: B1, transformable: false);
            context.Against(group, Subway.ElectricPanel, MapSide.South, MinZ, -17.4f, 2f, baseY: B1);
            context.Against(group, Subway.ElectricPanel, MapSide.South, MinZ, -16f, 2f, baseY: B1);
            context.Against(group, Subway.ElectricPanel, MapSide.West, -GateX, -11.5f, 2f, baseY: B1);
            context.Prop(group, Kit.Barrel, -11.2f, -12.8f, 0f, 0.9f, baseY: B1);
            context.Prop(group, Kit.Barrel, -11.4f, -11.6f, 0f, 0.9f, baseY: B1);
            float pipeYaw = MapLayoutTools.MeasureLengthYaw(Kit.Pipe);
            foreach (float x in new[] { -23f, -21f, -19f, -17f, -15f, -13f, -11f })
                context.placer.Place(Kit.Pipe, group, new Vector3(x, -1.2f, MinZ + 0.4f), pipeYaw, name: "Pipe");

            // 창고.
            context.Against(group, Kit.Shelf, MapSide.South, MinZ, 12f, 1.9f, baseY: B1);
            context.Against(group, Kit.Shelf, MapSide.South, MinZ, 14.4f, 1.9f, baseY: B1);
            context.Against(group, Kit.Shelf, MapSide.South, MinZ, 19f, 1.9f, baseY: B1);
            GameObject crate = context.Against(group, Kit.Crate, MapSide.East, GateX, -12.6f, 1f, baseY: B1);
            context.Decorate(crate, Kit.CrateSmall, Vector2.zero, 25f, 0.6f);
            GameObject pallet = context.Prop(group, Kit.Pallet, 17f, -10.6f, 0f, 0.15f, baseY: B1, transformable: false);
            context.Decorate(pallet, Kit.BoxStack, Vector2.zero, 0f, 0.7f);
            context.Prop(group, Kit.Crate, 21.5f, -10.5f, 20f, 1f, baseY: B1);
            context.Prop(group, Subway.CleaningCart, 22.6f, -12.8f, 90f, 1f, baseY: B1);
        }

        /// <summary>1호선 승강장과 다리 위 소품을 놓는다.</summary>
        private static void FurnishLine1(Context context)
        {
            Transform group = context.Group("G_U_Props");
            StationMaterials materials = context.materials;

            foreach (float zc in new[] { P1Center, P2Center })
            {
                // 계단 옆 통로는 비우고 벤치는 바깥 선로 쪽을 본다.
                float outward = zc > 0f ? 0f : 180f;
                foreach (float x in new[] { -22f, 18f, 22f })
                    context.Prop(group, Subway.Bench, x, zc + Mathf.Sign(zc) * 1.6f, outward, 0.85f, baseY: L1Platform);
                foreach (float x in new[] { -18f, 18f })
                    context.Prop(group, Subway.RecycleBin, x, zc - Mathf.Sign(zc) * 1.6f, outward + 180f, 1f, baseY: L1Platform);
                context.Prop(group, VendingMachine, -27f, zc - 1.5f, 90f, 2f, baseY: L1Platform);
                context.Prop(group, Subway.FireCabinet, 27.5f, zc + 1.6f, -90f, 1.5f, baseY: L1Platform);
                context.Prop(group, Subway.InfoBoard, 25f, zc - 0.6f, 90f, 2f, baseY: L1Platform);
                foreach (float x in new[] { -20f, 20f })
                    HangingSign(group, materials, new Vector3(x, L1Platform, zc), Roof, materials.line1);
            }

            // 선로 D: 작업 자재, 선로 A: 고깔·드럼통
            GameObject pallet = context.Prop(group, Kit.Pallet, -14f, L1TrackZs[3], 90f, 0.15f, baseY: GroundTop, transformable: false);
            context.Decorate(pallet, Kit.Crate, new Vector2(0f, 0.3f), 10f, 1f);
            context.Prop(group, Kit.Cone, -11f, L1TrackZs[3], 0f, 0.6f, baseY: GroundTop);
            context.Prop(group, Kit.Barrel, 24f, L1TrackZs[0], 0f, 0.9f, baseY: GroundTop);
            context.Prop(group, Kit.Cone, 21f, L1TrackZs[0] + 0.5f, 0f, 0.6f, baseY: GroundTop);

            // 다리 매표소 집기.
            GameObject desk = context.Prop(group, Subway.OfficeDesk, -3.5f, 9.9f, 180f, 0.74f, baseY: Bridge);
            context.Decorate(desk, Subway.Monitor, new Vector2(-0.2f, -0.1f), 0f, 0.45f);
            context.Prop(group, Subway.OfficeChair, -3.7f, 11f, 180f, 1f, baseY: Bridge);
            context.Against(group, Kit.Cabinet, MapSide.North, 13f, -5f, 1f, baseY: Bridge);

            // 다리: 발매기·자판기·물품보관함·광고판·매점 선반
            context.Against(group, Subway.TicketMachine, MapSide.North, MaxZ, 2.4f, 1.75f, baseY: Bridge);
            context.Against(group, Subway.TicketMachine, MapSide.North, MaxZ, 3.5f, 1.75f, baseY: Bridge);
            context.Against(group, VendingMachine, MapSide.North, MaxZ, 5.4f, 2f, baseY: Bridge);
            context.Against(group, Subway.CoinLocker, MapSide.South, MinZ, -4f, 1.9f, baseY: Bridge);
            context.Against(group, Subway.AdBoard, MapSide.South, MinZ, -1.2f, 2f, baseY: Bridge);
            context.Against(group, Kit.Shelf, MapSide.South, MinZ, 2.5f, 1.9f, baseY: Bridge);
            context.Against(group, Kit.Shelf, MapSide.South, MinZ, 4.4f, 1.9f, baseY: Bridge);
            context.Prop(group, Kit.BoxStack, 5.5f, -11.5f, 90f, 0.7f, baseY: Bridge);

            // 다리: 벤치·분리수거함·화분·차단봉·비상용품함
            context.Prop(group, Subway.Bench, 2f, 4f, 90f, 0.85f, baseY: Bridge);
            context.Prop(group, Subway.Bench, -2f, -4f, -90f, 0.85f, baseY: Bridge);
            context.Prop(group, Subway.RecycleBin, 0f, 1.5f, 0f, 1f, baseY: Bridge);
            context.Prop(group, Subway.RecycleBin, 0.8f, -1.6f, 180f, 1f, baseY: Bridge);
            context.Prop(group, Subway.Planter, 6.5f, 2f, 0f, 1.2f, baseY: Bridge);
            context.Prop(group, Subway.Planter, -6.5f, -2f, 0f, 1.2f, baseY: Bridge);
            context.Prop(group, Subway.Stanchion, -5.2f, 2.2f, 0f, 0.95f, baseY: Bridge);
            context.Prop(group, Subway.EmergencyBox, 6.5f, -3f, -90f, 0.9f, baseY: Bridge);

            // 다리 위 숨을 곳: 상자 더미·고깔(공사 중 구역)·여행 가방
            context.Prop(group, Kit.BoxStack, -6f, 4.5f, 0f, 0.7f, baseY: Bridge);
            context.Prop(group, Kit.Crate, 6.2f, 10.5f, 15f, 1f, baseY: Bridge);
            context.Prop(group, Kit.Cone, 5f, 9f, 0f, 0.6f, baseY: Bridge);
            context.Prop(group, Kit.Cone, 6.6f, 8.6f, 0f, 0.6f, baseY: Bridge);
            context.Prop(group, Subway.Suitcase, -6.4f, -8.8f, 30f, 0.95f, baseY: Bridge);
        }

        #endregion

        #region 도형 도우미

        /// <summary>
        /// FBX 가져오기는 발광을 옮기지 않아 화면·발광판이 꺼져 보이고 도기는 어두운 역에서 검게 보이므로, 머티리얼 이름으로 찾아 역 머티리얼로 바꿔 끼운다.
        /// </summary>
        private static void ApplyPropMaterials(Transform environment, Dictionary<string, Material> remap)
            => MapLayoutTools.ReplaceMaterials(environment, remap);

        /// <summary>바닥판·구멍을 x·z 범위로 적으면 읽기 쉬워서, Rect의 y를 z로 쓰는 사각형을 min/max 순서로 만든다.</summary>
        private static Rect XzRect(float xMin, float xMax, float zMin, float zMax) => Rect.MinMaxRect(xMin, zMin, xMax, zMax);

        /// <summary>메시 편집 없이 계단 구멍이 있는 바닥을 만들려고 구멍이 아닌 칸만 상자로 만들고 이웃 칸은 합친다.</summary>
        private static void Slab(Transform parent, string name, Rect area, float yTop, float thickness, Material material, params Rect[] holes)
        {
            var xs = new SortedSet<float> { area.xMin, area.xMax };
            var zs = new SortedSet<float> { area.yMin, area.yMax };
            foreach (Rect hole in holes)
            {
                if (!hole.Overlaps(area))
                    continue;
                xs.Add(Mathf.Clamp(hole.xMin, area.xMin, area.xMax));
                xs.Add(Mathf.Clamp(hole.xMax, area.xMin, area.xMax));
                zs.Add(Mathf.Clamp(hole.yMin, area.yMin, area.yMax));
                zs.Add(Mathf.Clamp(hole.yMax, area.yMin, area.yMax));
            }
            float[] xl = xs.ToArray();
            float[] zl = zs.ToArray();
            for (int zi = 0; zi < zl.Length - 1; zi++)
            {
                int runStart = -1;
                for (int xi = 0; xi <= xl.Length - 1; xi++)
                {
                    bool solid = xi < xl.Length - 1
                                 && !holes.Any(h => h.Contains(new Vector2((xl[xi] + xl[xi + 1]) / 2f, (zl[zi] + zl[zi + 1]) / 2f)));
                    if (solid && runStart < 0)
                        runStart = xi;
                    if (!solid && runStart >= 0)
                    {
                        float x0 = xl[runStart], x1 = xl[xi], z0 = zl[zi], z1 = zl[zi + 1];
                        MapLayoutTools.Box(parent, name, new Vector3((x0 + x1) / 2f, yTop - thickness / 2f, (z0 + z1) / 2f),
                            new Vector3(x1 - x0, thickness, z1 - z0), material);
                        runStart = -1;
                    }
                }
            }
        }

        /// <summary>역 계단이 모두 같은 폭·단 크기를 쓰도록 공용 계단 함수에 기본값을 넘긴다.</summary>
        private static void Stairs(Transform parent, Material material, Vector3 bottom, Vector3 direction, float rise, float width)
            => MapLayoutTools.Stairs(parent, material, bottom, direction, rise, width, StepRise, StepRun);

        /// <summary>벽 두께·머티리얼을 매번 넘기지 않도록 감싸고, 건물이 뚫리지 않게 벽은 변신 대상으로 등록하지 않는다.</summary>
        private static void WallRun(Context context, Transform parent, bool alongX, float line, float from, float to, float yBottom, float yTop,
            Material material, params Gap[] gaps)
            => MapLayoutTools.WallRun(parent, alongX, line, from, to, yBottom, yTop, material, context.materials.glass, WallThickness,
                null, gaps);

        /// <summary>구역을 나누되 사람이 지나가도록 개찰구 기계 사이에 틈을 두고 초록 표시등을 단다.</summary>
        private static void GateLine(Transform parent, StationMaterials materials, bool alongX, float line, float from, float to, float floorY)
        {
            for (float along = from + 0.75f; along <= to - 0.75f + 0.01f; along += 1.5f)
            {
                Vector3 center = alongX ? new Vector3(along, floorY + 0.5f, line) : new Vector3(line, floorY + 0.5f, along);
                Vector3 size = alongX ? new Vector3(0.3f, 1f, 1.4f) : new Vector3(1.4f, 1f, 0.3f);
                MapLayoutTools.Box(parent, "TicketGate", center, size, materials.gate);
                Vector3 lampSize = alongX ? new Vector3(0.2f, 0.01f, 0.12f) : new Vector3(0.12f, 0.01f, 0.2f);
                MapLayoutTools.Box(parent, "GateLamp", center + Vector3.up * 0.505f, lampSize, materials.gateGlow, collider: false);
            }
        }

        /// <summary>계단 구멍에 빠지지 않도록 난간을 두되 계단이 나가는 쪽은 비운다.</summary>
        private static void HoleRailing(Transform parent, Material material, Rect hole, float floorY, MapSide openEnd)
        {
            RailingRun(parent, material, alongX: true, hole.yMin, hole.xMin, hole.xMax, floorY);
            RailingRun(parent, material, alongX: true, hole.yMax, hole.xMin, hole.xMax, floorY);
            if (openEnd != MapSide.West)
                RailingRun(parent, material, alongX: false, hole.xMin, hole.yMin, hole.yMax, floorY);
            if (openEnd != MapSide.East)
                RailingRun(parent, material, alongX: false, hole.xMax, hole.yMin, hole.yMax, floorY);
        }

        /// <summary>이 맵 안에서 짧게 부르도록 공용 난간 함수를 넘긴다.</summary>
        private static void RailingRun(Transform parent, Material material, bool alongX, float line, float from, float to, float floorY)
            => MapLayoutTools.RailingRun(parent, material, alongX, line, from, to, floorY);

        /// <summary>빈 판은 선로로 읽히지 않아 걸리지 않는 레일과 침목을 놓는다.</summary>
        private static void Tracks(Transform parent, StationMaterials materials, float z, float bedY)
        {
            float length = MaxX - MinX + 2f;
            foreach (float offset in new[] { -0.72f, 0.72f })
                MapLayoutTools.Box(parent, "Rail", new Vector3(0f, bedY + 0.075f, z + offset), new Vector3(length, 0.15f, 0.07f), materials.rail, collider: false);
            for (float x = MinX - 0.6f; x <= MaxX + 0.6f; x += 1.2f)
                MapLayoutTools.Box(parent, "Sleeper", new Vector3(x, bedY + 0.04f, z), new Vector3(0.25f, 0.08f, 2.2f), materials.sleeper, collider: false);
        }

        /// <summary>선로가 벽에서 끊겨 보이지 않도록 벽 바깥에 어두운 터널 입구를 만든다.</summary>
        private static void TunnelMouth(Context context, Transform parent, float side, float zCenter, float width, float bedY, float height)
        {
            Material dark = context.materials.tunnel;
            float x = side * (MaxX + 0.5f);
            float centerY = bedY + height / 2f;
            MapLayoutTools.Box(parent, "TunnelFloor", new Vector3(x, bedY - 0.1f, zCenter), new Vector3(1f, 0.2f, width), dark);
            MapLayoutTools.Box(parent, "TunnelBack", new Vector3(side * (MaxX + 0.95f), centerY, zCenter), new Vector3(0.1f, height, width), dark);
            foreach (float edge in new[] { -1f, 1f })
                MapLayoutTools.Box(parent, "TunnelSide", new Vector3(x, centerY, zCenter + edge * (width / 2f + 0.05f)), new Vector3(1f, height, 0.1f), dark);
            MapLayoutTools.Box(parent, "TunnelTop", new Vector3(x, bedY + height + 0.05f, zCenter), new Vector3(1f, 0.1f, width + 0.2f), dark);
            MapPiecePlacer.CreatePanelLamp(context.lights, new Vector3(side * (MaxX - 0.4f), bedY + height + 0.3f, zCenter), new Color(1f, 0.22f, 0.15f), 1.6f, 6f,
                context.materials.emergency, context.materials.lampOff, 90f).name = "EmergencyLamp";
        }

        /// <summary>출구가 있어 보이되 맵 밖으로 나갈 수 없도록 닫힌 셔터와 비상구 표시로 만든다.</summary>
        private static void ExitShutter(Transform parent, StationMaterials materials, float side, float z)
        {
            float x = side * (MaxX - 0.14f);
            MapLayoutTools.Box(parent, "ExitShutter", new Vector3(x, B1 + 1.3f, z), new Vector3(0.06f, 2.6f, 3.6f), materials.shutter);
            for (float y = 0.3f; y < 2.6f; y += 0.3f)
                MapLayoutTools.Box(parent, "ShutterRib", new Vector3(side * (MaxX - 0.18f), B1 + y, z), new Vector3(0.02f, 0.04f, 3.6f), materials.trim, collider: false);
            MapLayoutTools.Box(parent, "ExitSign", new Vector3(side * (MaxX - 0.15f), B1 + 2.85f, z), new Vector3(0.05f, 0.3f, 1.2f), materials.exitGreen, collider: false);
        }

        /// <summary>지금 어느 층인지 알 수 있도록 벽면에 노선 색 띠를 붙인다.</summary>
        private static void Stripe(Transform parent, Material material, bool alongX, float line, float from, float to, float y)
        {
            float length = to - from;
            Vector3 center = alongX ? new Vector3((from + to) / 2f, y, line) : new Vector3(line, y, (from + to) / 2f);
            Vector3 size = alongX ? new Vector3(length, 0.25f, 0.02f) : new Vector3(0.02f, 0.25f, length);
            MapLayoutTools.Box(parent, "LineStripe", center, size, material, collider: false);
        }

        /// <summary>어두운 역에서도 위치를 알 수 있도록 머리에 걸리지 않는 높이에 발광 역명판을 매단다.</summary>
        private static void HangingSign(Transform parent, StationMaterials materials, Vector3 floorPoint, float ceilingY, Material band)
        {
            float signY = floorPoint.y + 3.2f;
            MapLayoutTools.Box(parent, "StationSign", new Vector3(floorPoint.x, signY, floorPoint.z), new Vector3(3f, 0.6f, 0.08f), materials.signWhite, collider: false);
            MapLayoutTools.Box(parent, "StationSignBand", new Vector3(floorPoint.x, signY - 0.2f, floorPoint.z), new Vector3(3.02f, 0.12f, 0.1f), band, collider: false);
            float rodHeight = ceilingY - (signY + 0.3f);
            if (rodHeight <= 0f)
                return;
            foreach (float offset in new[] { -1.3f, 1.3f })
            {
                MapLayoutTools.Box(parent, "SignRod", new Vector3(floorPoint.x + offset, signY + 0.3f + rodHeight / 2f, floorPoint.z), new Vector3(0.03f, rodHeight, 0.03f),
                    materials.rail, collider: false);
            }
        }

        #endregion

        #region 조명·지점·분위기

        /// <summary>사냥 시간에 꺼지는 층별 천장 형광등과 방 조명을 둔다.</summary>
        private static void BuildLights(Context context)
        {
            Material on = context.materials.lampOn;
            Material off = context.materials.lampOff;
            var cool = new Color(0.85f, 0.93f, 1f);
            var warm = new Color(1f, 0.9f, 0.76f);
            var orange = new Color(1f, 0.72f, 0.45f);

            // 지정 위치에 패널 조명 하나를 놓는다.
            void Panel(float x, float y, float z, Color color, float intensity, float range, float yaw = 0f)
                => MapPiecePlacer.CreatePanelLamp(context.lights, new Vector3(x, y, z), color, intensity, range, on, off, yaw);

            float b2Ceiling = B1 - SlabThickness;
            float b1Ceiling = GroundTop - SlabThickness;

            // B2: 승강장 위 두 줄, 선로 위 한 줄씩
            foreach (float x in new[] { -24f, -16f, -8f, 0f, 8f, 16f, 24f })
            {
                Panel(x, b2Ceiling, -2.4f, cool, 2.4f, 9f);
                Panel(x, b2Ceiling, 2.4f, cool, 2.4f, 9f);
            }
            foreach (float x in new[] { -20f, 0f, 20f })
            {
                Panel(x, b2Ceiling, B2TrackZ + 1.5f, cool, 1.2f, 7f);
                Panel(x, b2Ceiling, -B2TrackZ - 1.5f, cool, 1.2f, 7f);
            }

            // B1: 유료·비유료 구역, 방
            foreach (float x in new[] { -28f, -18f, -10f, 0f, 10f, 18f, 28f })
            {
                Panel(x, b1Ceiling, -4f, warm, 2.2f, 9f);
                Panel(x, b1Ceiling, 4f, warm, 2.2f, 9f);
            }
            Panel(-28f, b1Ceiling, 11f, warm, 1.8f, 7f); Panel(28f, b1Ceiling, -11f, warm, 1.8f, 7f);
            Panel(-18f, b1Ceiling, 11.5f, cool, 2f, 8f);
            // 화장실: 좁고 천장이 높아(4.6m) 범위가 짧으면 바닥 근처가 어둡다.
            Panel(-9.5f, b1Ceiling, 11.5f, cool, 3.5f, 9.5f, 90f); Panel(-4.5f, b1Ceiling, 11.5f, cool, 3.5f, 9.5f, 90f);
            Panel(-21f, b1Ceiling, -11.5f, orange, 1.8f, 8f); Panel(-13f, b1Ceiling, -11.5f, orange, 1.8f, 8f);
            Panel(13f, b1Ceiling, -11.5f, warm, 1.6f, 8f); Panel(21f, b1Ceiling, -11.5f, warm, 1.6f, 8f);

            // 지상: 지붕 아래 (승강장·선로), 다리 아래 보조등, 매표소
            foreach (float x in new[] { -26f, -18f, -10f, 10f, 18f, 26f })
            {
                foreach (float z in new[] { P1Center, P2Center })
                    Panel(x, Roof, z, cool, 3.2f, 14f);
                Panel(x, Roof, 0f, cool, 2f, 12f);
            }
            foreach (float z in new[] { P1Center, P2Center })
            {
                Panel(-4f, Bridge - 0.3f, z, cool, 2f, 8f);
                Panel(4f, Bridge - 0.3f, z, cool, 2f, 8f);
            }
            foreach (float z in new[] { -10f, -3f, 3f, 10f })
                Panel(0f, Roof, z, warm, 2.6f, 12f, 90f);
            Panel(-3.5f, Bridge + 3f, 11f, warm, 1.6f, 6f);
        }

        /// <summary>시작하자마자 마주치지 않게 역할별 스폰을 나누고, 모든 층을 돌도록 전원 장치 후보는 층마다 둔다.</summary>
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

        /// <summary>지하가 대부분이라 하늘을 끄고 어두운 푸른 환경광을 쓴다.</summary>
        private static void ApplyAtmosphere(Scene scene)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.16f, 0.18f);
            RenderSettings.fog = false;
            SceneLightingSetup.Apply(scene);
        }

        #endregion

        #region 확인용 결과

        /// <summary>
        /// Unity를 열지 않고도 결과를 확인할 수 있도록 배치도(지붕을 숨긴 지상층)와 층별 눈높이 장면을 찍어 Temp/TransferStation에 저장한다.
        /// </summary>
        private static void CapturePreviews(Context context, Bounds playArea)
        {
            const float eye = 1.7f;
            var shots = new List<MapPreviewCapture.Shot>
            {
                new MapPreviewCapture.Shot("01_B2_Platform", new Vector3(-30f, B2 + eye, 2.6f), new Vector3(10f, B2 + 1f, -2f)),
                new MapPreviewCapture.Shot("02_B2_TrainInterior", new Vector3(-11.5f, B2 + 1.6f, B2TrackZ), new Vector3(10f, B2 + 1.2f, B2TrackZ)),
                new MapPreviewCapture.Shot("03_B1_PaidArea", new Vector3(-22f, B1 + eye, -6f), new Vector3(10f, B1 + 1f, 4f)),
                new MapPreviewCapture.Shot("04_B1_GatesWest", new Vector3(-31f, B1 + eye, -12f), new Vector3(-20f, B1 + 1f, 6f)),
                new MapPreviewCapture.Shot("05_B1_Office", new Vector3(-13f, B1 + eye, 13f), new Vector3(-23f, B1 + 0.8f, 9.5f)),
                new MapPreviewCapture.Shot("06_B1_Restroom", new Vector3(-2.6f, B1 + eye, 9.6f), new Vector3(-6f, B1 + 0.6f, 13.5f)),
                new MapPreviewCapture.Shot("07_B1_MachineRoom", new Vector3(-11f, B1 + eye, -9.6f), new Vector3(-23f, B1 + 1f, -13.5f)),
                new MapPreviewCapture.Shot("08_B1_Storage", new Vector3(11f, B1 + eye, -9.6f), new Vector3(23f, B1 + 1f, -13.5f)),
                new MapPreviewCapture.Shot("09_TransferStairs", new Vector3(-12f, B1 + eye, -6.5f), new Vector3(3f, L1Platform, -6.5f)),
                new MapPreviewCapture.Shot("10_G_Overview", new Vector3(-31f, 8f, -12f), new Vector3(0f, 1f, 4f)),
                new MapPreviewCapture.Shot("11_G_Platform1", new Vector3(28f, L1Platform + eye, -7f), new Vector3(-10f, L1Platform + 1f, -6f)),
                new MapPreviewCapture.Shot("12_U_Bridge", new Vector3(-7f, Bridge + eye, -13f), new Vector3(6f, Bridge, 10f)),
                new MapPreviewCapture.Shot("13_G_TrainInterior", new Vector3(-11.5f, L1Platform + 1.6f, L1TrackZs[1]), new Vector3(10f, L1Platform + 1.2f, L1TrackZs[1])),
            };
            MapPreviewCapture.Capture(PreviewFolder, playArea, context.roof.Where(r => r != null).ToList(), shots);
        }

        /// <summary>
        /// 배치 좌표를 다듬을 때 에셋 실제 크기와 크기 보정 내역을 참고하도록 Temp/TransferStation/sizes.txt에 남긴다.
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

        /// <summary>환승역 머티리얼을 없으면 URP Lit으로 만든다.</summary>
        private sealed class StationMaterials
        {
            private const string Folder = "Assets/_Project/Art/MapKit/Materials/TransferStation";

            public Material floor, platformFloor, ballast, ceiling, wall, tile, trim, concrete, stair, metal, line1, line2, safetyYellow,
                rail, sleeper, tunnel, carBody, carFloor, seat, door, rubber, glass, lightStrip, gate, gateGlow, shutter,
                signWhite, exitGreen, emergency, screen, adGlow, infoGlow, ceramic, lampOn, lampOff;

            /// <summary>구조물 배치용 MapKit 머티리얼을 역 머티리얼로 바꿔 끼우는 표.</summary>
            public Dictionary<string, Material> KitRemap { get; private set; }

            /// <summary>FBX 가져오기는 발광을 옮기지 않으므로 지하철 소품 머티리얼을 역 머티리얼로 바꿔 끼운다.</summary>
            public Dictionary<string, Material> PropRemap { get; private set; }

            /// <summary>
            /// 빌더를 다시 실행해도 GUID가 유지되어 씬 참조가 끊기지 않도록 있으면 불러오고, 없을 때만 URP Lit으로 만든다.
            /// </summary>
            public static StationMaterials LoadOrCreate()
            {
                var factory = new MapMaterialFactory(Folder);
                var result = new StationMaterials
                {
                    floor = factory.Opaque("M_Station_Floor", new Color(0.6f, 0.58f, 0.54f), 0f, 0.5f),
                    platformFloor = factory.Opaque("M_Station_PlatformFloor", new Color(0.44f, 0.44f, 0.46f), 0f, 0.4f),
                    ballast = factory.Opaque("M_Station_Ballast", new Color(0.2f, 0.19f, 0.18f), 0f, 0.05f),
                    ceiling = factory.Opaque("M_Station_Ceiling", new Color(0.5f, 0.52f, 0.53f), 0f, 0.2f),
                    wall = factory.Opaque("M_Station_Wall", new Color(0.8f, 0.78f, 0.72f), 0f, 0.5f),
                    tile = factory.Opaque("M_Station_Tile", new Color(0.72f, 0.8f, 0.82f), 0f, 0.65f),
                    trim = factory.Opaque("M_Station_Trim", new Color(0.12f, 0.14f, 0.15f), 0.4f, 0.45f),
                    concrete = factory.Opaque("M_Station_Concrete", new Color(0.4f, 0.4f, 0.4f), 0f, 0.15f),
                    stair = factory.Opaque("M_Station_Stair", new Color(0.52f, 0.5f, 0.47f), 0f, 0.35f),
                    metal = factory.Opaque("M_Station_Metal", new Color(0.55f, 0.58f, 0.6f), 0.6f, 0.5f),
                    line1 = factory.Opaque("M_Station_Line1Blue", new Color(0.1f, 0.3f, 0.75f), 0f, 0.5f),
                    line2 = factory.Opaque("M_Station_Line2Green", new Color(0.12f, 0.62f, 0.28f), 0f, 0.5f),
                    safetyYellow = factory.Emissive("M_Station_SafetyYellow", new Color(0.95f, 0.78f, 0.1f), new Color(0.35f, 0.28f, 0.02f)),
                    rail = factory.Opaque("M_Station_Rail", new Color(0.4f, 0.38f, 0.36f), 0.85f, 0.55f),
                    sleeper = factory.Opaque("M_Station_Sleeper", new Color(0.3f, 0.28f, 0.26f), 0f, 0.1f),
                    tunnel = factory.Opaque("M_Station_Tunnel", new Color(0.02f, 0.02f, 0.025f), 0f, 0f),
                    carBody = factory.Opaque("M_Station_CarBody", new Color(0.72f, 0.74f, 0.76f), 0.7f, 0.6f),
                    carFloor = factory.Opaque("M_Station_CarFloor", new Color(0.32f, 0.33f, 0.35f), 0f, 0.3f),
                    seat = factory.Opaque("M_Station_Seat", new Color(0.16f, 0.3f, 0.58f), 0f, 0.25f),
                    door = factory.Opaque("M_Station_CarDoor", new Color(0.5f, 0.52f, 0.55f), 0.6f, 0.5f),
                    rubber = factory.Opaque("M_Station_Rubber", new Color(0.07f, 0.07f, 0.08f), 0f, 0.1f),
                    glass = factory.Transparent("M_Station_Glass", new Color(0.55f, 0.7f, 0.75f, 0.25f)),
                    lightStrip = factory.Emissive("M_Station_LightStrip", new Color(0.9f, 0.95f, 1f), new Color(0.9f, 0.95f, 1f) * 1.2f),
                    gate = factory.Opaque("M_Station_Gate", new Color(0.62f, 0.64f, 0.66f), 0.7f, 0.65f),
                    gateGlow = factory.Emissive("M_Station_GateGlow", new Color(0.1f, 0.6f, 0.25f), new Color(0.2f, 1f, 0.4f) * 1.5f),
                    shutter = factory.Opaque("M_Station_Shutter", new Color(0.3f, 0.31f, 0.33f), 0.5f, 0.35f),
                    signWhite = factory.Emissive("M_Station_SignWhite", new Color(0.92f, 0.94f, 0.95f), new Color(0.6f, 0.62f, 0.65f)),
                    exitGreen = factory.Emissive("M_Station_ExitGreen", new Color(0.05f, 0.5f, 0.2f), new Color(0.1f, 1f, 0.35f) * 1.6f),
                    emergency = factory.Emissive("M_Station_Emergency", new Color(0.6f, 0.05f, 0.03f), new Color(1f, 0.15f, 0.1f) * 2f),
                    screen = factory.Emissive("M_Station_Screen", new Color(0.1f, 0.3f, 0.5f), new Color(0.25f, 0.6f, 1f) * 2f),
                    adGlow = factory.Emissive("M_Station_AdGlow", new Color(0.95f, 0.8f, 0.6f), new Color(1f, 0.82f, 0.6f) * 1.6f),
                    infoGlow = factory.Emissive("M_Station_InfoGlow", new Color(0.95f, 0.96f, 0.98f), new Color(0.95f, 0.97f, 1f) * 1.4f),
                    ceramic = factory.Opaque("M_Station_Ceramic", new Color(0.95f, 0.95f, 0.94f), 0f, 0.45f),
                    lampOn = AssetDatabase.LoadAssetAtPath<Material>($"{TestEnvironmentSetupTool.MaterialFolder}/M_LampOn.mat"),
                    lampOff = AssetDatabase.LoadAssetAtPath<Material>($"{TestEnvironmentSetupTool.MaterialFolder}/M_LampOff.mat"),
                };
                result.KitRemap = new Dictionary<string, Material>
                {
                    { "M_Kit_Plaster", result.wall },
                    { "M_Kit_WoodDark", result.trim },
                    { "M_Kit_Wood", result.metal },
                    { "M_Kit_Concrete", result.concrete },
                    { "M_Kit_ConcreteDark", result.concrete },
                    { "M_Kit_Metal", result.metal },
                };
                result.PropRemap = new Dictionary<string, Material>
                {
                    { "M_Sub_Screen", result.screen },
                    { "M_Sub_LightPanel", result.infoGlow },
                    { "M_Sub_AdPanel", result.adGlow },
                    { "M_Sub_Ceramic", result.ceramic },
                };
                AssetDatabase.SaveAssets();
                return result;
            }
        }
    }
}
