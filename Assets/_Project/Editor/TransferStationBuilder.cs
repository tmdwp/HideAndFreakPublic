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
    /// <summary>
    /// Scenes/Custom/Map_TransferStation 씬을 "환승역"으로 만든다. (지하 2호선 + 지상 1호선이 이어진 역 구조도 기반)
    /// 다시 실행하면 씬을 덮어써서 항상 같은 결과가 된다. 64×28m, 4개 층:
    /// - B2(−10m) 2호선 승강장: 섬식 승강장과 1m 아래 선로 2줄, 전동차 1편성, 양 끝 터널 입구. 계단 2개로 B1.
    /// - B1(−5m) 2호선 대합실: 가운데 유료 구역, 양 끝 개찰구 밖 비유료 구역(매표기·닫힌 출구),
    ///   역무실·화장실·기계실·창고, 1호선 승강장으로 올라가는 환승 계단 2개.
    /// - G(0~+1m) 1호선 승강장: 지상 선로 4줄과 섬식 승강장 2개, 지붕(11m), 전동차 1편성. 승강장 끝 계단으로 선로에 내려간다.
    /// - U(+6m) 1호선 대합실: 선로 위를 가로지르는 다리. 계단 4개로 두 승강장에 내려가고, 매표소·자판기가 있다.
    /// 마네킹은 지하 서쪽, 가드는 지상·다리에서 나온다.
    /// 구조물은 기본 도형 상자(구멍 뚫린 바닥판·계단·벽)로, 소품은 지하철 소품(MapKit/Models/Subway)과 MapKit 기본 소품으로 놓는다.
    /// 만든 뒤 맵 최적화(NavMesh·오클루전)와 맵 목록 갱신을 실행하고, Temp/TransferStation에 배치도·장면 사진을 남긴다.
    /// </summary>
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

        // 1호선: 선로 A(z −12) | 승강장 P1(z −10~−4) | 선로 B(−2)·C(2) | 승강장 P2(4~10) | 선로 D(12)
        private static readonly float[] L1TrackZs = { -12f, -2f, 2f, 12f };
        private const float P1Center = -7f, P2Center = 7f, L1PlatformHalf = 3f, L1PlatformEndX = 30f;
        private const float BridgeHalfX = 8f;

        // 계단: 단 높이 0.25m, 단 길이 0.35m, 폭 2.4m
        private const float StepRise = 0.25f, StepRun = 0.35f, StairWidth = 2.4f;

        // B1 개찰구 줄 (x ±24), 방 구역 (|z| 9~14)
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

        /// <summary>벽에 낸 출입구·창. center는 벽을 따라가는 좌표, bottom은 벽 바닥에서의 높이.</summary>
        private readonly struct Gap
        {
            public readonly float center, width, bottom, height;
            public readonly bool glass;

            public Gap(float center, float width, float bottom, float height, bool glass)
            {
                this.center = center;
                this.width = width;
                this.bottom = bottom;
                this.height = height;
                this.glass = glass;
            }

            /// <summary>문: 바닥부터 높이 2.4m, 폭 1.4m.</summary>
            public static Gap Door(float center) => new Gap(center, 1.4f, 0f, 2.4f, false);

            /// <summary>창: 1m 높이부터 1.2m, 유리.</summary>
            public static Gap Window(float center, float width = 3f) => new Gap(center, width, 1f, 1.2f, true);

            /// <summary>뚫린 통로(바닥부터 지정 높이).</summary>
            public static Gap Opening(float center, float width, float height) => new Gap(center, width, 0f, height, false);
        }

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

        [MenuItem(EditorMenus.Root + "맵 생성/환승역 (Map_TransferStation)")]
        private static void BuildFromMenu()
        {
            // 열린 씬의 파일이 이미 지워졌으면 묻지 않는다. (저장하면 지운 씬이 되살아나므로)
            Scene active = SceneManager.GetActiveScene();
            bool fileExists = !string.IsNullOrEmpty(active.path) && File.Exists(active.path);
            if (fileExists && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            BuildScene($"{MapCatalog.CustomMapFolder}/{SceneName}.unity", capturePreview: true);
            MapRegistrySync.Sync();
        }

        #endregion

        /// <summary>
        /// 환승역 씬을 새로 만들어 scenePath에 저장한다. (열린 씬은 닫힌다)
        /// </summary>
        /// <param name="capturePreview">true면 Temp/TransferStation에 배치도·장면 사진을 남긴다.</param>
        public static void BuildScene(string scenePath, bool capturePreview)
        {
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

            // 에셋 데모용 스크립트 제거 → URP 머티리얼 → 충돌체 → 변신 사물 등록 순서. (정리가 등록 컴포넌트를 지우지 않도록)
            MapSceneBuilder.CleanEnvironment(context.environment);
            ApplyPropMaterials(context.environment, materials.PropRemap);
            int converted = UrpMaterialConverter.ConvertRenderers(context.environment.GetComponentsInChildren<Renderer>(true));
            int colliders = MapSceneBuilder.AddMissingColliders(context.environment);
            context.placer.RegisterCandidates(out int propCount, out int wallCount, out int skipped);
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

            WriteReport(context, converted, colliders, propCount, wallCount, skipped);
            Debug.Log($"[TransferStationBuilder] 환승역 완료: {scenePath}\n" +
                      $"변신 사물 {propCount}개(크기·구성으로 제외 {skipped}개), 벽 {wallCount}개, 충돌체 추가 {colliders}개, 머티리얼 변환 {converted}개, " +
                      $"없는 에셋 {context.placer.MissingCount}개. 에셋 크기 목록: {PreviewFolder}/sizes.txt");
        }

        #region 구조: B2 2호선 승강장

        /// <summary>
        /// 선로 바닥판, 섬식 승강장(1m), 홀 벽(터널 입구 포함), 선로, 승강장 → 선로 점검 계단, B1로 올라가는 계단 2개.
        /// </summary>
        private static void BuildLine2Platform(Context c)
        {
            Transform group = c.Group("B2_Line2Platform");
            StationMaterials m = c.materials;

            Slab(group, "TrackBed", R(MinX, MaxX, -B2HallHalf, B2HallHalf), B2Track, SlabThickness, m.ballast);
            Slab(group, "Platform", R(MinX, MaxX, -B2PlatformHalf, B2PlatformHalf), B2, B2 - B2Track, m.platformFloor);
            foreach (float side in new[] { -1f, 1f })
            {
                MapLayoutTools.Box(group, "SafetyLine", new Vector3(0f, B2 + 0.005f, side * (B2PlatformHalf - 0.45f)), new Vector3(MaxX - MinX, 0.01f, 0.12f),
                    m.safetyYellow, collider: false);
                Tracks(group, m, side * B2TrackZ, B2Track);
            }

            // 홀 벽: 남·북은 노선 색 띠, 동·서는 선로 자리에 터널 입구
            float wallTop = B1 - SlabThickness;
            foreach (float side in new[] { -1f, 1f })
            {
                WallRun(c, group, alongX: true, side * B2HallHalf, MinX, MaxX, B2Track, wallTop, m.wall, false);
                Stripe(group, m.line2, alongX: true, side * (B2HallHalf - 0.11f), MinX, MaxX, B2 + 1.3f);
                float end = side * MaxX;
                WallRun(c, group, alongX: false, end, -B2HallHalf, B2HallHalf, B2Track, wallTop, m.wall, false,
                    Gap.Opening(-7f, 6f, 4f), Gap.Opening(7f, 6f, 4f));
                TunnelMouth(c, group, side, -7f, 6f, B2Track, 4f);
                TunnelMouth(c, group, side, 7f, 6f, B2Track, 4f);
            }

            // 선로 점검 계단 (승강장 동·서 끝, 양쪽 선로)
            foreach (float x in new[] { -29f, 29f })
            {
                foreach (float side in new[] { -1f, 1f })
                    Stairs(group, m.concrete, new Vector3(x, B2Track, side * (B2PlatformHalf + 4 * StepRun)), new Vector3(0f, 0f, -side), B2 - B2Track, 1.6f);
            }

            // B1으로 올라가는 계단 A(서, +x로 오름)·B(동, −x로 오름)
            Stairs(group, m.stair, new Vector3(-21f, B2, 0f), Vector3.right, B1 - B2, StairWidth);
            Stairs(group, m.stair, new Vector3(21f, B2, 0f), Vector3.left, B1 - B2, StairWidth);
        }

        #endregion

        #region 구조: B1 2호선 대합실

        /// <summary>
        /// 대합실 바닥판(계단 A·B 구멍), 둘레 벽(닫힌 출구), 개찰구 줄, 방 4개, 1호선 승강장으로 올라가는 환승 계단 C·D, 기둥.
        /// </summary>
        private static void BuildLine2Concourse(Context c)
        {
            Transform group = c.Group("B1_Line2Concourse");
            StationMaterials m = c.materials;

            Rect holeA = R(-21f, -14f, -StairWidth / 2f - 0.1f, StairWidth / 2f + 0.1f);
            Rect holeB = R(14f, 21f, -StairWidth / 2f - 0.1f, StairWidth / 2f + 0.1f);
            Slab(group, "Floor", R(MinX, MaxX, MinZ, MaxZ), B1, SlabThickness, m.floor, holeA, holeB);
            HoleRailing(group, m.rail, holeA, B1, openEnd: MapSide.East);
            HoleRailing(group, m.rail, holeB, B1, openEnd: MapSide.West);

            // 둘레 벽과 닫힌 출구(동·서 끝)
            float top = GroundTop - SlabThickness;
            foreach (float side in new[] { -1f, 1f })
            {
                WallRun(c, group, alongX: true, side * MaxZ, MinX, MaxX, B1, top, m.wall, false);
                WallRun(c, group, alongX: false, side * MaxX, MinZ, MaxZ, B1, top, m.wall, false);
                foreach (float z in new[] { -6f, 6f })
                    ExitShutter(group, m, side, z);
            }

            // 개찰구 (x ±24, 방 구역 사이)
            foreach (float x in new[] { -GateX, GateX })
                GateLine(group, m, alongX: false, x, -RoomLine, RoomLine, B1);

            // 방: 역무실(북서)·화장실(북)·기계실(남서)·창고(남동)
            WallRun(c, group, alongX: true, RoomLine, -GateX, -12f, B1, top, m.wall, true, Gap.Door(-14f), Gap.Window(-20f));
            WallRun(c, group, alongX: false, -GateX, RoomLine, MaxZ, B1, top, m.wall, true);
            WallRun(c, group, alongX: false, -12f, RoomLine, MaxZ, B1, top, m.wall, true);
            WallRun(c, group, alongX: true, RoomLine, -12f, -2f, B1, top, m.tile, true, Gap.Door(-10f), Gap.Door(-4f));
            WallRun(c, group, alongX: false, -7f, RoomLine, MaxZ, B1, top, m.tile, true);
            WallRun(c, group, alongX: false, -2f, RoomLine, MaxZ, B1, top, m.tile, true);
            WallRun(c, group, alongX: true, -RoomLine, -GateX, -10f, B1, top, m.wall, true, Gap.Door(-12f));
            WallRun(c, group, alongX: false, -GateX, MinZ, -RoomLine, B1, top, m.wall, true);
            WallRun(c, group, alongX: false, -10f, MinZ, -RoomLine, B1, top, m.wall, true);
            WallRun(c, group, alongX: true, -RoomLine, 10f, GateX, B1, top, m.wall, true, Gap.Door(12f));
            WallRun(c, group, alongX: false, 10f, MinZ, -RoomLine, B1, top, m.wall, true);
            WallRun(c, group, alongX: false, GateX, MinZ, -RoomLine, B1, top, m.wall, true);

            // 환승 계단 C(P1 쪽, +x로 오름)·D(P2 쪽, −x로 오름): B1 → 1호선 승강장(+1m)
            Stairs(group, m.stair, new Vector3(-6f, B1, -6.5f), Vector3.right, L1Platform - B1, StairWidth);
            Stairs(group, m.stair, new Vector3(6f, B1, 6.5f), Vector3.left, L1Platform - B1, StairWidth);

            // 기둥 (계단을 피해서)
            foreach (Vector2 p in new[] { new Vector2(-8f, -4f), new Vector2(8f, 4f), new Vector2(-8f, 4f), new Vector2(8f, -4f), new Vector2(0f, 0f) })
                MapLayoutTools.Box(group, "Column", new Vector3(p.x, (B1 + top) / 2f, p.y), new Vector3(0.7f, top - B1, 0.7f), m.wall);
        }

        #endregion

        #region 구조: G 1호선 승강장 · U 1호선 대합실(다리)

        /// <summary>
        /// 지상 바닥판(환승 계단 구멍), 섬식 승강장 2개(1m, 환승 계단 구멍), 선로 4줄, 둘레 벽(선로 자리 터널 입구), 지붕, 승강장 끝 계단.
        /// </summary>
        private static void BuildLine1Platforms(Context c)
        {
            Transform group = c.Group("G_Line1Platforms");
            StationMaterials m = c.materials;

            Rect holeC = R(-6f, 2.4f, -6.5f - StairWidth / 2f - 0.1f, -6.5f + StairWidth / 2f + 0.1f);
            Rect holeD = R(-2.4f, 6f, 6.5f - StairWidth / 2f - 0.1f, 6.5f + StairWidth / 2f + 0.1f);
            Slab(group, "Ground", R(MinX, MaxX, MinZ, MaxZ), GroundTop, SlabThickness, m.ballast, holeC, holeD);
            // B1 대합실 천장판 (바닥판 아랫면의 자갈색이 보이지 않게)
            Slab(group, "B1Ceiling", R(MinX, MaxX, MinZ, MaxZ), GroundTop - SlabThickness, 0.02f, m.ceiling, holeC, holeD);

            foreach (float zc in new[] { P1Center, P2Center })
            {
                Rect platform = R(-L1PlatformEndX, L1PlatformEndX, zc - L1PlatformHalf, zc + L1PlatformHalf);
                Slab(group, "Platform", platform, L1Platform, L1Platform - GroundTop, m.platformFloor, holeC, holeD);
                foreach (float edge in new[] { -1f, 1f })
                {
                    MapLayoutTools.Box(group, "SafetyLine", new Vector3(0f, L1Platform + 0.005f, zc + edge * (L1PlatformHalf - 0.45f)),
                        new Vector3(L1PlatformEndX * 2f, 0.01f, 0.12f), m.safetyYellow, collider: false);
                }
                // 승강장 끝 계단 (선로로 내려감)
                Stairs(group, m.concrete, new Vector3(L1PlatformEndX + 4 * StepRun, GroundTop, zc), Vector3.left, L1Platform - GroundTop, StairWidth);
                Stairs(group, m.concrete, new Vector3(-L1PlatformEndX - 4 * StepRun, GroundTop, zc), Vector3.right, L1Platform - GroundTop, StairWidth);
            }
            HoleRailing(group, m.rail, holeC, L1Platform, openEnd: MapSide.East);
            HoleRailing(group, m.rail, holeD, L1Platform, openEnd: MapSide.West);

            foreach (float z in L1TrackZs)
                Tracks(group, m, z, GroundTop);

            // 둘레 벽: 남·북은 1호선 색 띠, 동·서는 선로 자리 터널 입구 (A, B+C, D)
            foreach (float side in new[] { -1f, 1f })
            {
                WallRun(c, group, alongX: true, side * MaxZ, MinX, MaxX, GroundTop, Roof, m.wall, false);
                Stripe(group, m.line1, alongX: true, side * (MaxZ - 0.11f), MinX, MaxX, 2.4f);
                WallRun(c, group, alongX: false, side * MaxX, MinZ, MaxZ, GroundTop, Roof, m.wall, false,
                    Gap.Opening(-12f, 3.8f, 4.5f), Gap.Opening(0f, 8f, 4.5f), Gap.Opening(12f, 3.8f, 4.5f));
                TunnelMouth(c, group, side, -12f, 3.8f, GroundTop, 4.5f);
                TunnelMouth(c, group, side, 0f, 8f, GroundTop, 4.5f);
                TunnelMouth(c, group, side, 12f, 3.8f, GroundTop, 4.5f);
            }

            // 지붕 (배치도에서 숨김)
            GameObject roof = MapLayoutTools.Box(group, "Roof", new Vector3(0f, Roof + 0.2f, 0f), new Vector3(MaxX - MinX, 0.4f, MaxZ - MinZ), m.ceiling);
            c.roof.Add(roof.GetComponent<Renderer>());
            foreach (float x in new[] { -24f, -16f, 16f, 24f })
            {
                GameObject beam = MapLayoutTools.Box(group, "RoofBeam", new Vector3(x, Roof - 0.3f, 0f), new Vector3(0.4f, 0.6f, MaxZ - MinZ), m.trim);
                c.roof.Add(beam.GetComponent<Renderer>());
            }
        }

        /// <summary>
        /// 선로 위 다리형 대합실: 바닥판, 난간(계단 자리 비움), 기둥, 계단 4개(두 승강장 양쪽), 매표소.
        /// </summary>
        private static void BuildLine1Concourse(Context c)
        {
            Transform group = c.Group("U_Line1Concourse");
            StationMaterials m = c.materials;

            Slab(group, "Deck", R(-BridgeHalfX, BridgeHalfX, MinZ, MaxZ), Bridge, 0.3f, m.floor);
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * BridgeHalfX;
                // 계단 자리(z ±7)를 비운 난간
                foreach ((float from, float to) in new[] { (MinZ, P1Center - StairWidth / 2f), (P1Center + StairWidth / 2f, P2Center - StairWidth / 2f), (P2Center + StairWidth / 2f, MaxZ) })
                    RailingRun(group, m.rail, alongX: false, x, from, to, Bridge);

                // 계단: 승강장(+1m) → 다리(+6m)
                foreach (float zc in new[] { P1Center, P2Center })
                    Stairs(group, m.stair, new Vector3(side * (BridgeHalfX + 20 * StepRun), L1Platform, zc), new Vector3(-side, 0f, 0f), Bridge - L1Platform, StairWidth);

                // 기둥: 선로 B·C 사이(z 0)와 벽쪽(z ±13.5)
                foreach (float z in new[] { -13.5f, 0f, 13.5f })
                    MapLayoutTools.Box(group, "Column", new Vector3(side * (BridgeHalfX - 0.6f), (GroundTop + Bridge - 0.3f) / 2f, z),
                        new Vector3(0.6f, Bridge - 0.3f - GroundTop, 0.6f), m.trim);
            }

            // 개찰구 줄 (다리 가운데 z 0, 북쪽 매표소·남쪽 보관함 구역을 나눈다)
            GateLine(group, m, alongX: true, 0f, -BridgeHalfX, BridgeHalfX, Bridge);

            // 매표소 (x −6~−1, z 9~13, 높이 3m)
            const float boothTop = Bridge + 3f;
            WallRun(c, group, alongX: true, 9f, -6f, -1f, Bridge, boothTop, m.wall, true, Gap.Window(-3.5f));
            WallRun(c, group, alongX: false, -1f, 9f, 13f, Bridge, boothTop, m.wall, true, Gap.Door(11f));
            WallRun(c, group, alongX: false, -6f, 9f, 13f, Bridge, boothTop, m.wall, true);
            WallRun(c, group, alongX: true, 13f, -6f, -1f, Bridge, boothTop, m.wall, true);
            MapLayoutTools.Box(group, "BoothRoof", new Vector3(-3.5f, boothTop + 0.1f, 11f), new Vector3(5.2f, 0.2f, 4.2f), m.ceiling);
        }

        #endregion

        #region 전동차

        /// <summary>
        /// 2호선 전동차(B2, 북쪽 선로)와 1호선 전동차(G, 선로 B) 각 2량. 승강장 쪽 문이 열려 있다.
        /// </summary>
        private static void BuildTrains(Context c)
        {
            Transform group = c.Group("Trains");
            float half = CarLength / 2f + CarGap / 2f;

            // 2호선: 선로 z +5.7, 승강장은 −z 쪽
            BuildTrain(c, group, half, B2TrackZ, B2, -1f, c.materials.line2);
            // 1호선: 선로 B z −2, 승강장 P1은 −z 쪽
            BuildTrain(c, group, half, L1TrackZs[1], L1Platform, -1f, c.materials.line1);

            const float floorTop = 0.02f;
            c.Prop(group, Kit.Box, -half - 4.8f, B2TrackZ + 0.65f, 15f, 0.35f, baseY: B2 + floorTop);
            c.Prop(group, Kit.BoxStack, half + 5.2f, B2TrackZ + 0.6f, 0f, 0.7f, baseY: B2 + floorTop);
            c.Prop(group, Subway.Suitcase, half + 5.1f, L1TrackZs[1] + 0.6f, 90f, 0.95f, baseY: L1Platform + floorTop);
            c.Prop(group, Subway.Suitcase, -half - 2.2f, B2TrackZ + 0.65f, -70f, 0.95f, baseY: B2 + floorTop);
            c.Prop(group, Kit.Bucket, -half + 1.6f, L1TrackZs[1] - 0.3f, 0f, 0.3f, baseY: L1Platform + floorTop);
        }

        private const float CarLength = 12f, CarWidth = 3.2f, CarHeight = 2.33f, CarGap = 0.6f, CarDoorWidth = 1.3f;
        private static readonly float[] CarDoorOffsets = { -3.6f, 0f, 3.6f };

        /// <summary>
        /// 2량 편성: x ±half에 한 량씩, 가운데 연결 통로. doorSide는 문이 열린 쪽의 z 방향(−1/+1).
        /// </summary>
        private static void BuildTrain(Context c, Transform parent, float half, float cz, float floorY, float doorSide, Material band)
        {
            Transform train = MapLayoutTools.CreateChild(parent, "Train");
            BuildCar(c, train, -half, cz, floorY, doorSide, band, closedWest: true, closedEast: false);
            BuildCar(c, train, half, cz, floorY, doorSide, band, closedWest: false, closedEast: true);

            Material rubber = c.materials.rubber;
            MapLayoutTools.Box(train, "GangwayFloor", new Vector3(0f, floorY - 0.04f, cz), new Vector3(CarGap, 0.12f, 1.1f), rubber);
            MapLayoutTools.Box(train, "GangwaySide", new Vector3(0f, floorY + 1.02f, cz + 0.6f), new Vector3(CarGap, 2.05f, 0.06f), rubber);
            MapLayoutTools.Box(train, "GangwaySide", new Vector3(0f, floorY + 1.02f, cz - 0.6f), new Vector3(CarGap, 2.05f, 0.06f), rubber);
            MapLayoutTools.Box(train, "GangwayTop", new Vector3(0f, floorY + 2.1f, cz), new Vector3(CarGap, 0.1f, 1.26f), rubber);
        }

        /// <summary>
        /// 전동차 한 량: 바닥·하부·지붕, 옆면(문 쪽은 열림, 반대쪽은 닫힌 문), 끝면(운전실 또는 연결 통로), 긴 의자, 손잡이 기둥, 조명.
        /// </summary>
        private static void BuildCar(Context c, Transform parent, float centerX, float cz, float floorY, float doorSide, Material band,
            bool closedWest, bool closedEast)
        {
            Transform car = MapLayoutTools.CreateChild(parent, "TrainCar");
            StationMaterials m = c.materials;
            float halfLength = CarLength / 2f;
            float halfWidth = CarWidth / 2f;

            MapLayoutTools.Box(car, "Floor", new Vector3(centerX, floorY - 0.04f, cz), new Vector3(CarLength, 0.12f, CarWidth), m.carFloor);
            MapLayoutTools.Box(car, "Underframe", new Vector3(centerX, floorY - 0.55f, cz), new Vector3(CarLength - 1f, 0.9f, CarWidth - 0.6f), m.trim);
            MapLayoutTools.Box(car, "Roof", new Vector3(centerX, floorY + CarHeight + 0.075f, cz), new Vector3(CarLength, 0.15f, CarWidth), m.carBody);
            MapLayoutTools.Box(car, "LightStrip", new Vector3(centerX, floorY + CarHeight - 0.02f, cz), new Vector3(CarLength - 1f, 0.04f, 0.3f), m.lightStrip, collider: false);

            foreach (float side in new[] { -1f, 1f })
                BuildCarSide(c, car, centerX, cz + side * (halfWidth - 0.05f), floorY, side, doorsOpen: Mathf.Approximately(side, doorSide), band);
            BuildCarEnd(c, car, centerX - halfLength + 0.05f, cz, floorY, closedWest);
            BuildCarEnd(c, car, centerX + halfLength - 0.05f, cz, floorY, closedEast);

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
                    MapLayoutTools.Box(car, "Seat", new Vector3(x, floorY + 0.24f, wallZ - side * 0.25f), new Vector3(length, 0.44f, 0.5f), m.seat);
                    MapLayoutTools.Box(car, "SeatBack", new Vector3(x, floorY + 0.72f, wallZ - side * 0.04f), new Vector3(length, 0.52f, 0.08f), m.seat);
                }
            }

            foreach (float offset in CarDoorOffsets)
            {
                GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Pole";
                pole.transform.SetParent(car, false);
                pole.transform.position = new Vector3(centerX + offset, floorY + CarHeight / 2f, cz);
                pole.transform.localScale = new Vector3(0.05f, CarHeight / 2f, 0.05f);
                pole.GetComponent<Renderer>().sharedMaterial = m.rail;
            }

            foreach (float offset in new[] { -3f, 3f })
                MapPiecePlacer.CreateBareLight(c.lights, "TrainLight", new Vector3(centerX + offset, floorY + CarHeight - 0.3f, cz), new Color(0.88f, 0.95f, 1f), 1.4f, 5.5f);
        }

        /// <summary>
        /// 전동차 옆면: 아래 철판 / 창 / 위 철판, 문 자리는 열린 틈(문틀 위만) 또는 닫힌 문. 바깥에 노선 색 띠.
        /// </summary>
        private static void BuildCarSide(Context c, Transform car, float centerX, float wallZ, float floorY, float outward, bool doorsOpen, Material band)
        {
            StationMaterials m = c.materials;
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
                    MapLayoutTools.Box(car, "SideLower", new Vector3(x, floorY + 0.485f, wallZ), new Vector3(length, 0.93f, thickness), m.carBody);
                    GameObject glass = MapLayoutTools.Box(car, "SideWindow", new Vector3(x, floorY + 1.4f, wallZ), new Vector3(length, 0.9f, thickness * 0.5f), m.glass);
                    glass.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    MapLayoutTools.Box(car, "SideUpper", new Vector3(x, floorY + (1.85f + CarHeight) / 2f, wallZ), new Vector3(length, CarHeight - 1.85f, thickness), m.carBody);
                }
                if (door < halfLength)
                {
                    float doorX = centerX + door;
                    MapLayoutTools.Box(car, "DoorHeader", new Vector3(doorX, floorY + (doorTop + CarHeight) / 2f, wallZ), new Vector3(CarDoorWidth, CarHeight - doorTop, thickness), m.carBody);
                    if (!doorsOpen)
                        MapLayoutTools.Box(car, "DoorClosed", new Vector3(doorX, floorY + (0.02f + doorTop) / 2f, wallZ), new Vector3(CarDoorWidth, doorTop - 0.02f, thickness), m.door);
                }
                start = door + CarDoorWidth / 2f;
            }

            MapLayoutTools.Box(car, "LineBand", new Vector3(centerX, floorY + 0.8f, wallZ + outward * 0.06f), new Vector3(CarLength, 0.16f, 0.02f), band, collider: false);
        }

        /// <summary>
        /// 전동차 끝면: 운전실이면 막힌 벽 + 앞 유리, 아니면 가운데에 연결 통로 문틈.
        /// </summary>
        private static void BuildCarEnd(Context c, Transform car, float x, float cz, float floorY, bool closed)
        {
            StationMaterials m = c.materials;
            const float thickness = 0.1f;
            if (closed)
            {
                MapLayoutTools.Box(car, "EndLower", new Vector3(x, floorY + 0.5f, cz), new Vector3(thickness, 0.96f, CarWidth), m.carBody);
                GameObject glass = MapLayoutTools.Box(car, "EndWindow", new Vector3(x, floorY + 1.4f, cz), new Vector3(thickness * 0.5f, 0.84f, CarWidth - 0.4f), m.glass);
                glass.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                MapLayoutTools.Box(car, "EndUpper", new Vector3(x, floorY + (1.82f + CarHeight) / 2f, cz), new Vector3(thickness, CarHeight - 1.82f, CarWidth), m.carBody);
                foreach (float side in new[] { -1f, 1f })
                    MapLayoutTools.Box(car, "EndPillar", new Vector3(x, floorY + 1.4f, cz + side * (CarWidth / 2f - 0.1f)), new Vector3(thickness, 0.84f, 0.2f), m.carBody);
                return;
            }

            const float gap = 1f;
            float sideWidth = (CarWidth - gap) / 2f;
            foreach (float side in new[] { -1f, 1f })
                MapLayoutTools.Box(car, "EndWall", new Vector3(x, floorY + CarHeight / 2f, cz + side * (gap / 2f + sideWidth / 2f)), new Vector3(thickness, CarHeight, sideWidth), m.carBody);
            MapLayoutTools.Box(car, "EndHeader", new Vector3(x, floorY + (2.05f + CarHeight) / 2f, cz), new Vector3(thickness, CarHeight - 2.05f, gap), m.carBody);
        }

        #endregion

        #region 소품

        /// <summary>
        /// 2호선 승강장: 기둥, 스테인리스 벤치·분리수거함, 노선도 안내판, 역명판, 승강장 끝 소화전함·비상용품함, 청소 카트, 선로 작업 자재.
        /// 벤치는 선로 쪽을 보게 놓는다. (모델 앞면 +Z)
        /// </summary>
        private static void FurnishLine2Platform(Context c)
        {
            Transform group = c.Group("B2_Props");
            StationMaterials m = c.materials;
            float ceiling = B1 - SlabThickness;

            foreach (float x in new[] { -28f, -8f, 8f, 28f })
                MapLayoutTools.Box(group, "Column", new Vector3(x, (B2 + ceiling) / 2f, 0f), new Vector3(0.7f, ceiling - B2, 0.7f), m.wall);
            foreach (float x in new[] { -4f, 4f, -26f, 26f })
            {
                foreach (float z in new[] { -2.6f, 2.6f })
                    c.Prop(group, Subway.Bench, x, z, z > 0f ? 0f : 180f, 0.85f, baseY: B2);
            }
            c.Prop(group, Subway.RecycleBin, -10f, 2.7f, 0f, 1f, baseY: B2);
            c.Prop(group, Subway.RecycleBin, 10f, -2.7f, 180f, 1f, baseY: B2);
            c.Prop(group, Subway.InfoBoard, -23.5f, 0f, 90f, 2f, baseY: B2);
            c.Prop(group, Subway.InfoBoard, 23.5f, 0f, -90f, 2f, baseY: B2);
            foreach (float x in new[] { -12f, 12f })
                HangingSign(group, m, new Vector3(x, B2, -2.6f), ceiling, m.line2);

            c.Against(group, Subway.FireCabinet, MapSide.West, MinX, 2.5f, 1.5f, baseY: B2);
            c.Against(group, Subway.EmergencyBox, MapSide.West, MinX, -2.5f, 0.9f, baseY: B2);
            c.Against(group, Subway.FireCabinet, MapSide.East, MaxX, 2.4f, 1.5f, baseY: B2);
            c.Against(group, Kit.BoxStack, MapSide.East, MaxX, -2.4f, 0.7f, baseY: B2, yaw: 90f);
            c.Prop(group, Subway.CleaningCart, 6f, -2.8f, 0f, 1f, baseY: B2);
            c.Prop(group, Subway.WetFloorSign, 7.4f, -1.9f, 30f, 0.62f, baseY: B2);

            // 남쪽 선로: 작업용 팔레트·상자·드럼통과 고깔
            GameObject pallet = c.Prop(group, Kit.Pallet, 18f, -B2TrackZ, 90f, 0.15f, baseY: B2Track, transformable: false);
            c.Decorate(pallet, Kit.Crate, new Vector2(0f, 0.3f), 10f, 1f);
            c.Prop(group, Kit.Barrel, 20f, -B2TrackZ + 0.9f, 0f, 0.9f, baseY: B2Track);
            c.Prop(group, Kit.Cone, 15f, -B2TrackZ, 0f, 0.6f, baseY: B2Track);
            c.Prop(group, Kit.Barrel, -26f, B2TrackZ + 2.4f, 0f, 0.9f, baseY: B2Track);
        }

        /// <summary>
        /// 2호선 대합실: 비유료 구역(교통카드 발매기·물품보관함·벤치·화분), 유료 구역(벤치·분리수거함·광고판·소화전함·화분),
        /// 역무실(사무 책상·의자·모니터), 화장실(양변기·세면대·청소 표지), 기계실(공조기·배전반), 창고.
        /// </summary>
        private static void FurnishLine2Concourse(Context c)
        {
            Transform group = c.Group("B1_Props");

            // 비유료 구역 (서·동 끝)
            c.Against(group, Subway.TicketMachine, MapSide.North, MaxZ, -30.2f, 1.75f, baseY: B1);
            c.Against(group, Subway.TicketMachine, MapSide.North, MaxZ, -29.1f, 1.75f, baseY: B1);
            c.Against(group, VendingMachine, MapSide.North, MaxZ, -27.4f, 2f, baseY: B1);
            c.Against(group, Subway.CoinLocker, MapSide.South, MinZ, -29f, 1.9f, baseY: B1);
            c.Prop(group, Subway.Bench, -28f, 0f, 90f, 0.85f, baseY: B1);
            c.Prop(group, Subway.Planter, -30.8f, 10f, 0f, 1.2f, baseY: B1);
            c.Prop(group, Subway.Stanchion, -26.2f, -3.5f, 90f, 0.95f, baseY: B1);
            c.Against(group, Subway.TicketMachine, MapSide.South, MinZ, 26.9f, 1.75f, baseY: B1);
            c.Against(group, Subway.TicketMachine, MapSide.South, MinZ, 28f, 1.75f, baseY: B1);
            c.Against(group, VendingMachine, MapSide.South, MinZ, 29.7f, 2f, baseY: B1);
            c.Against(group, Subway.CoinLocker, MapSide.North, MaxZ, 28.5f, 1.9f, baseY: B1);
            c.Prop(group, Subway.RecycleBin, 26f, 2f, 90f, 1f, baseY: B1);
            c.Prop(group, Subway.Planter, 30.8f, -10f, 0f, 1.2f, baseY: B1);
            c.Prop(group, Subway.Stanchion, 26.2f, 3.5f, 90f, 0.95f, baseY: B1);

            // 유료 구역
            c.Prop(group, Subway.Bench, -16f, 6f, 0f, 0.85f, baseY: B1);
            c.Prop(group, Subway.Bench, 16f, -6f, 180f, 0.85f, baseY: B1);
            c.Prop(group, Subway.RecycleBin, -11f, -3.5f, 0f, 1f, baseY: B1);
            c.Prop(group, Subway.RecycleBin, 11f, 3.5f, 180f, 1f, baseY: B1);
            c.Against(group, Subway.AdBoard, MapSide.North, MaxZ, 5f, 2f, baseY: B1);
            c.Against(group, Subway.AdBoard, MapSide.South, MinZ, -4f, 2f, baseY: B1);
            c.Against(group, Subway.FireCabinet, MapSide.North, MaxZ, 12f, 1.5f, baseY: B1);
            c.Against(group, Subway.EmergencyBox, MapSide.South, MinZ, 4f, 0.9f, baseY: B1);
            c.Prop(group, Subway.Planter, -3f, -3f, 0f, 1.2f, baseY: B1);
            c.Prop(group, Subway.Planter, 3f, 3f, 0f, 1.2f, baseY: B1);
            c.Prop(group, Subway.WetFloorSign, 13f, -2f, 20f, 0.62f, baseY: B1);

            // 역무실 (x −24~−12, z 9~14): 책상은 창(남쪽)에 붙이고 의자·모니터는 북쪽(앉는 쪽)을 본다.
            foreach (float x in new[] { -21f, -16.5f })
            {
                GameObject desk = c.Prop(group, Subway.OfficeDesk, x, 10.4f, 180f, 0.74f, baseY: B1);
                c.Decorate(desk, Subway.Monitor, new Vector2(-0.2f, -0.1f), 0f, 0.45f);
                c.Prop(group, Subway.OfficeChair, x - 0.2f, 11.5f, 180f, 1f, baseY: B1);
            }
            c.Against(group, Kit.Cabinet, MapSide.West, -GateX, 12.5f, 1f, baseY: B1);
            c.Against(group, Kit.LockerRow, MapSide.North, MaxZ, -14.5f, 1.9f, baseY: B1);
            c.Against(group, Kit.Cabinet, MapSide.North, MaxZ, -22.5f, 1f, baseY: B1);

            // 화장실 (여: x −12~−7, 남: −7~−2)
            foreach (float roomX in new[] { -12f, -7f })
            {
                foreach (float offset in new[] { 1.5f, 3.5f })
                {
                    c.Against(group, Subway.Toilet, MapSide.North, MaxZ, roomX + offset, 0.8f, baseY: B1);
                    MapLayoutTools.Box(group, "StallPartition", new Vector3(roomX + offset + 1f, B1 + 0.95f, MaxZ - 0.85f), new Vector3(0.05f, 1.7f, 1.5f), c.materials.tile);
                }
            }
            c.Against(group, Subway.WashBasin, MapSide.East, -7f, 10.6f, 1.6f, baseY: B1);
            c.Against(group, Subway.WashBasin, MapSide.East, -2f, 10.6f, 1.6f, baseY: B1);
            c.Prop(group, Subway.WetFloorSign, -4.2f, 9.8f, -15f, 0.62f, baseY: B1);
            c.Prop(group, Kit.Bucket, -3f, 9.8f, 0f, 0.3f, baseY: B1);

            // 기계실 (x −24~−10, z −14~−9)
            c.Against(group, Subway.AirHandler, MapSide.South, MinZ, -21f, 1.85f, baseY: B1, transformable: false);
            c.Against(group, Subway.ElectricPanel, MapSide.South, MinZ, -17.4f, 2f, baseY: B1);
            c.Against(group, Subway.ElectricPanel, MapSide.South, MinZ, -16f, 2f, baseY: B1);
            c.Against(group, Subway.ElectricPanel, MapSide.West, -GateX, -11.5f, 2f, baseY: B1);
            c.Prop(group, Kit.Barrel, -11.2f, -12.8f, 0f, 0.9f, baseY: B1);
            c.Prop(group, Kit.Barrel, -11.4f, -11.6f, 0f, 0.9f, baseY: B1);
            float pipeYaw = MapLayoutTools.MeasureLengthYaw(Kit.Pipe);
            foreach (float x in new[] { -23f, -21f, -19f, -17f, -15f, -13f, -11f })
                c.placer.Place(Kit.Pipe, group, new Vector3(x, -1.2f, MinZ + 0.4f), pipeYaw, name: "Pipe");

            // 창고 (x 10~24, z −14~−9)
            c.Against(group, Kit.Shelf, MapSide.South, MinZ, 12f, 1.9f, baseY: B1);
            c.Against(group, Kit.Shelf, MapSide.South, MinZ, 14.4f, 1.9f, baseY: B1);
            c.Against(group, Kit.Shelf, MapSide.South, MinZ, 19f, 1.9f, baseY: B1);
            GameObject crate = c.Against(group, Kit.Crate, MapSide.East, GateX, -12.6f, 1f, baseY: B1);
            c.Decorate(crate, Kit.CrateSmall, Vector2.zero, 25f, 0.6f);
            GameObject pallet = c.Prop(group, Kit.Pallet, 17f, -10.6f, 0f, 0.15f, baseY: B1, transformable: false);
            c.Decorate(pallet, Kit.BoxStack, Vector2.zero, 0f, 0.7f);
            c.Prop(group, Kit.Crate, 21.5f, -10.5f, 20f, 1f, baseY: B1);
            c.Prop(group, Subway.CleaningCart, 22.6f, -12.8f, 90f, 1f, baseY: B1);
        }

        /// <summary>
        /// 1호선: 승강장 벤치·분리수거함·자판기·소화전함·안내판·역명판, 선로 작업 자재,
        /// 다리 위 매표소 집기·벤치·물품보관함·발매기·매점·화분·차단봉.
        /// </summary>
        private static void FurnishLine1(Context c)
        {
            Transform group = c.Group("G_U_Props");
            StationMaterials m = c.materials;

            foreach (float zc in new[] { P1Center, P2Center })
            {
                // 다리 계단(x ±8~15)·환승 계단 구멍(x −6~6) 옆 통로는 비운다. 벤치는 바깥 선로 쪽을 본다.
                float outward = zc > 0f ? 0f : 180f;
                foreach (float x in new[] { -22f, 18f, 22f })
                    c.Prop(group, Subway.Bench, x, zc + Mathf.Sign(zc) * 1.6f, outward, 0.85f, baseY: L1Platform);
                foreach (float x in new[] { -18f, 18f })
                    c.Prop(group, Subway.RecycleBin, x, zc - Mathf.Sign(zc) * 1.6f, outward + 180f, 1f, baseY: L1Platform);
                c.Prop(group, VendingMachine, -27f, zc - 1.5f, 90f, 2f, baseY: L1Platform);
                c.Prop(group, Subway.FireCabinet, 27.5f, zc + 1.6f, -90f, 1.5f, baseY: L1Platform);
                c.Prop(group, Subway.InfoBoard, 25f, zc - 0.6f, 90f, 2f, baseY: L1Platform);
                foreach (float x in new[] { -20f, 20f })
                    HangingSign(group, m, new Vector3(x, L1Platform, zc), Roof, m.line1);
            }

            // 선로 D: 작업 자재, 선로 A: 고깔·드럼통
            GameObject pallet = c.Prop(group, Kit.Pallet, -14f, L1TrackZs[3], 90f, 0.15f, baseY: GroundTop, transformable: false);
            c.Decorate(pallet, Kit.Crate, new Vector2(0f, 0.3f), 10f, 1f);
            c.Prop(group, Kit.Cone, -11f, L1TrackZs[3], 0f, 0.6f, baseY: GroundTop);
            c.Prop(group, Kit.Barrel, 24f, L1TrackZs[0], 0f, 0.9f, baseY: GroundTop);
            c.Prop(group, Kit.Cone, 21f, L1TrackZs[0] + 0.5f, 0f, 0.6f, baseY: GroundTop);

            // 다리: 매표소 집기 (책상은 창 쪽, 의자·모니터는 북쪽)
            GameObject desk = c.Prop(group, Subway.OfficeDesk, -3.5f, 9.9f, 180f, 0.74f, baseY: Bridge);
            c.Decorate(desk, Subway.Monitor, new Vector2(-0.2f, -0.1f), 0f, 0.45f);
            c.Prop(group, Subway.OfficeChair, -3.7f, 11f, 180f, 1f, baseY: Bridge);
            c.Against(group, Kit.Cabinet, MapSide.North, 13f, -5f, 1f, baseY: Bridge);

            // 다리: 발매기·자판기·물품보관함·광고판·매점 선반
            c.Against(group, Subway.TicketMachine, MapSide.North, MaxZ, 2.4f, 1.75f, baseY: Bridge);
            c.Against(group, Subway.TicketMachine, MapSide.North, MaxZ, 3.5f, 1.75f, baseY: Bridge);
            c.Against(group, VendingMachine, MapSide.North, MaxZ, 5.4f, 2f, baseY: Bridge);
            c.Against(group, Subway.CoinLocker, MapSide.South, MinZ, -4f, 1.9f, baseY: Bridge);
            c.Against(group, Subway.AdBoard, MapSide.South, MinZ, -1.2f, 2f, baseY: Bridge);
            c.Against(group, Kit.Shelf, MapSide.South, MinZ, 2.5f, 1.9f, baseY: Bridge);
            c.Against(group, Kit.Shelf, MapSide.South, MinZ, 4.4f, 1.9f, baseY: Bridge);
            c.Prop(group, Kit.BoxStack, 5.5f, -11.5f, 90f, 0.7f, baseY: Bridge);

            // 다리: 벤치·분리수거함·화분·차단봉·비상용품함
            c.Prop(group, Subway.Bench, 2f, 4f, 90f, 0.85f, baseY: Bridge);
            c.Prop(group, Subway.Bench, -2f, -4f, -90f, 0.85f, baseY: Bridge);
            c.Prop(group, Subway.RecycleBin, 0f, 1.5f, 0f, 1f, baseY: Bridge);
            c.Prop(group, Subway.RecycleBin, 0.8f, -1.6f, 180f, 1f, baseY: Bridge);
            c.Prop(group, Subway.Planter, 6.5f, 2f, 0f, 1.2f, baseY: Bridge);
            c.Prop(group, Subway.Planter, -6.5f, -2f, 0f, 1.2f, baseY: Bridge);
            c.Prop(group, Subway.Stanchion, -5.2f, 2.2f, 0f, 0.95f, baseY: Bridge);
            c.Prop(group, Subway.EmergencyBox, 6.5f, -3f, -90f, 0.9f, baseY: Bridge);

            // 다리 위 숨을 곳: 상자 더미·고깔(공사 중 구역)·여행 가방
            c.Prop(group, Kit.BoxStack, -6f, 4.5f, 0f, 0.7f, baseY: Bridge);
            c.Prop(group, Kit.Crate, 6.2f, 10.5f, 15f, 1f, baseY: Bridge);
            c.Prop(group, Kit.Cone, 5f, 9f, 0f, 0.6f, baseY: Bridge);
            c.Prop(group, Kit.Cone, 6.6f, 8.6f, 0f, 0.6f, baseY: Bridge);
            c.Prop(group, Subway.Suitcase, -6.4f, -8.8f, 30f, 0.95f, baseY: Bridge);
        }

        #endregion

        #region 도형 도우미

        /// <summary>
        /// 이름표에 있는 머티리얼(지하철 소품의 화면·발광판·도기)을 역 머티리얼로 바꿔 끼운다.
        /// </summary>
        private static void ApplyPropMaterials(Transform environment, Dictionary<string, Material> remap)
        {
            foreach (Renderer target in environment.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = target.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != null && remap.TryGetValue(slots[i].name, out Material glow))
                    {
                        slots[i] = glow;
                        changed = true;
                    }
                }
                if (changed)
                    target.sharedMaterials = slots;
            }
        }

        /// <summary>x·z 범위 사각형. (Rect의 y를 z로 쓴다)</summary>
        private static Rect R(float xMin, float xMax, float zMin, float zMax) => Rect.MinMaxRect(xMin, zMin, xMax, zMax);

        /// <summary>
        /// 구멍 뚫린 바닥판. 구멍 가장자리로 영역을 격자로 나눠 구멍이 아닌 칸만 상자로 만들고, 같은 줄의 이웃 칸은 합친다.
        /// </summary>
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

        /// <summary>
        /// 계단: bottom(첫 단 바닥 중심선의 시작점)에서 direction(±x 또는 ±z)으로 올라간다. 단마다 아래까지 채운 상자.
        /// </summary>
        private static void Stairs(Transform parent, Material material, Vector3 bottom, Vector3 direction, float rise, float width)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(rise / StepRise));
            float stepRise = rise / count;
            bool alongX = Mathf.Abs(direction.x) > 0.5f;
            for (int i = 0; i < count; i++)
            {
                float top = bottom.y + stepRise * (i + 1);
                Vector3 center = bottom + direction * (StepRun * (i + 0.5f));
                center.y = (bottom.y + top) / 2f;
                Vector3 size = alongX ? new Vector3(StepRun, top - bottom.y, width) : new Vector3(width, top - bottom.y, StepRun);
                MapLayoutTools.Box(parent, "Step", center, size, material);
            }
        }

        /// <summary>
        /// 벽 한 줄: alongX면 z = line에서 x from~to, 아니면 x = line에서 z from~to. 출입구·창을 비우고(문틀 위·창 아래는 채움),
        /// 막힌 구간은 4m 이하 조각으로 나눈다. registerAsWall이면 1층 높이 조각을 벽 변신 후보로 등록한다.
        /// </summary>
        private static void WallRun(Context c, Transform parent, bool alongX, float line, float from, float to, float yBottom, float yTop,
            Material material, bool registerAsWall, params Gap[] gaps)
        {
            float height = yTop - yBottom;
            Vector3 Center(float along, float y) => alongX ? new Vector3(along, y, line) : new Vector3(line, y, along);
            Vector3 Size(float length, float h, float thick) => alongX ? new Vector3(length, h, thick) : new Vector3(thick, h, length);

            void Solid(float a, float b)
            {
                int pieces = Mathf.Max(1, Mathf.CeilToInt((b - a) / 4f - 0.01f));
                float step = (b - a) / pieces;
                for (int i = 0; i < pieces; i++)
                {
                    float s = a + step * i;
                    GameObject piece = MapLayoutTools.Box(parent, "Wall", Center(s + step / 2f, yBottom + height / 2f), Size(step, height, WallThickness), material);
                    if (registerAsWall)
                        c.placer.AsWall(piece);
                }
            }

            float cursor = from;
            foreach (Gap gap in gaps.OrderBy(g => g.center))
            {
                float g0 = Mathf.Max(from, gap.center - gap.width / 2f);
                float g1 = Mathf.Min(to, gap.center + gap.width / 2f);
                if (g0 - cursor > 0.01f)
                    Solid(cursor, g0);
                float length = g1 - g0;
                if (gap.bottom > 0f)
                    MapLayoutTools.Box(parent, "Sill", Center((g0 + g1) / 2f, yBottom + gap.bottom / 2f), Size(length, gap.bottom, WallThickness), material);
                float gapTop = gap.bottom + gap.height;
                if (gapTop < height - 0.01f)
                    MapLayoutTools.Box(parent, "Lintel", Center((g0 + g1) / 2f, yBottom + (gapTop + height) / 2f), Size(length, height - gapTop, WallThickness), material);
                if (gap.glass)
                {
                    GameObject glass = MapLayoutTools.Box(parent, "Glass", Center((g0 + g1) / 2f, yBottom + gap.bottom + gap.height / 2f),
                        Size(length, gap.height, 0.05f), c.materials.glass);
                    glass.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
                cursor = g1;
            }
            if (to - cursor > 0.01f)
                Solid(cursor, to);
        }

        /// <summary>
        /// 개찰구 줄: alongX면 z = line에서 x from~to, 아니면 x = line에서 z from~to.
        /// 1.5m 간격 기계(높이 1m, 줄 방향 두께 0.3m, 통과 방향 길이 1.4m), 사이 틈 1.2m. 위에 초록 표시등.
        /// </summary>
        private static void GateLine(Transform parent, StationMaterials m, bool alongX, float line, float from, float to, float floorY)
        {
            for (float along = from + 0.75f; along <= to - 0.75f + 0.01f; along += 1.5f)
            {
                Vector3 center = alongX ? new Vector3(along, floorY + 0.5f, line) : new Vector3(line, floorY + 0.5f, along);
                Vector3 size = alongX ? new Vector3(0.3f, 1f, 1.4f) : new Vector3(1.4f, 1f, 0.3f);
                MapLayoutTools.Box(parent, "TicketGate", center, size, m.gate);
                Vector3 lampSize = alongX ? new Vector3(0.2f, 0.01f, 0.12f) : new Vector3(0.12f, 0.01f, 0.2f);
                MapLayoutTools.Box(parent, "GateLamp", center + Vector3.up * 0.505f, lampSize, m.gateGlow, collider: false);
            }
        }

        /// <summary>
        /// 계단 구멍 둘레 난간(높이 1m). openEnd 쪽(계단이 올라와 나가는 쪽)은 비운다.
        /// </summary>
        private static void HoleRailing(Transform parent, Material material, Rect hole, float floorY, MapSide openEnd)
        {
            RailingRun(parent, material, alongX: true, hole.yMin, hole.xMin, hole.xMax, floorY);
            RailingRun(parent, material, alongX: true, hole.yMax, hole.xMin, hole.xMax, floorY);
            if (openEnd != MapSide.West)
                RailingRun(parent, material, alongX: false, hole.xMin, hole.yMin, hole.yMax, floorY);
            if (openEnd != MapSide.East)
                RailingRun(parent, material, alongX: false, hole.xMax, hole.yMin, hole.yMax, floorY);
        }

        /// <summary>
        /// 난간 한 줄: 위 손잡이 + 아래 판 (충돌체 있음, 높이 1m).
        /// </summary>
        private static void RailingRun(Transform parent, Material material, bool alongX, float line, float from, float to, float floorY)
        {
            float length = to - from;
            float mid = (from + to) / 2f;
            Vector3 Center(float y) => alongX ? new Vector3(mid, y, line) : new Vector3(line, y, mid);
            Vector3 Size(float h, float t) => alongX ? new Vector3(length, h, t) : new Vector3(t, h, length);
            MapLayoutTools.Box(parent, "RailTop", Center(floorY + 1f), Size(0.06f, 0.08f), material);
            MapLayoutTools.Box(parent, "RailPanel", Center(floorY + 0.5f), Size(0.9f, 0.04f), material);
        }

        /// <summary>
        /// 선로 한 줄: 레일 2개 + 침목 (충돌체 없음). bedY는 선로 바닥 높이.
        /// </summary>
        private static void Tracks(Transform parent, StationMaterials m, float z, float bedY)
        {
            float length = MaxX - MinX + 2f;
            foreach (float offset in new[] { -0.72f, 0.72f })
                MapLayoutTools.Box(parent, "Rail", new Vector3(0f, bedY + 0.075f, z + offset), new Vector3(length, 0.15f, 0.07f), m.rail, collider: false);
            for (float x = MinX - 0.6f; x <= MaxX + 0.6f; x += 1.2f)
                MapLayoutTools.Box(parent, "Sleeper", new Vector3(x, bedY + 0.04f, z), new Vector3(0.25f, 0.08f, 2.2f), m.sleeper, collider: false);
        }

        /// <summary>
        /// 벽 바깥 1m 깊이의 어두운 터널 입구(바닥·안벽·옆벽·윗판)와 위쪽 붉은 비상등.
        /// </summary>
        private static void TunnelMouth(Context c, Transform parent, float side, float zCenter, float width, float bedY, float height)
        {
            Material dark = c.materials.tunnel;
            float x = side * (MaxX + 0.5f);
            float centerY = bedY + height / 2f;
            MapLayoutTools.Box(parent, "TunnelFloor", new Vector3(x, bedY - 0.1f, zCenter), new Vector3(1f, 0.2f, width), dark);
            MapLayoutTools.Box(parent, "TunnelBack", new Vector3(side * (MaxX + 0.95f), centerY, zCenter), new Vector3(0.1f, height, width), dark);
            foreach (float edge in new[] { -1f, 1f })
                MapLayoutTools.Box(parent, "TunnelSide", new Vector3(x, centerY, zCenter + edge * (width / 2f + 0.05f)), new Vector3(1f, height, 0.1f), dark);
            MapLayoutTools.Box(parent, "TunnelTop", new Vector3(x, bedY + height + 0.05f, zCenter), new Vector3(1f, 0.1f, width + 0.2f), dark);
            MapPiecePlacer.CreatePanelLamp(c.lights, new Vector3(side * (MaxX - 0.4f), bedY + height + 0.3f, zCenter), new Color(1f, 0.22f, 0.15f), 1.6f, 6f,
                c.materials.emergency, c.materials.lampOff, 90f).name = "EmergencyLamp";
        }

        /// <summary>
        /// 동·서 끝 벽의 닫힌 출구 셔터와 비상구 표시. side는 벽 쪽(−1 서, +1 동).
        /// </summary>
        private static void ExitShutter(Transform parent, StationMaterials m, float side, float z)
        {
            float x = side * (MaxX - 0.14f);
            MapLayoutTools.Box(parent, "ExitShutter", new Vector3(x, B1 + 1.3f, z), new Vector3(0.06f, 2.6f, 3.6f), m.shutter);
            for (float y = 0.3f; y < 2.6f; y += 0.3f)
                MapLayoutTools.Box(parent, "ShutterRib", new Vector3(side * (MaxX - 0.18f), B1 + y, z), new Vector3(0.02f, 0.04f, 3.6f), m.trim, collider: false);
            MapLayoutTools.Box(parent, "ExitSign", new Vector3(side * (MaxX - 0.15f), B1 + 2.85f, z), new Vector3(0.05f, 0.3f, 1.2f), m.exitGreen, collider: false);
        }

        /// <summary>
        /// 벽면 노선 색 띠 (충돌체 없음). line은 띠를 붙일 면의 좌표.
        /// </summary>
        private static void Stripe(Transform parent, Material material, bool alongX, float line, float from, float to, float y)
        {
            float length = to - from;
            Vector3 center = alongX ? new Vector3((from + to) / 2f, y, line) : new Vector3(line, y, (from + to) / 2f);
            Vector3 size = alongX ? new Vector3(length, 0.25f, 0.02f) : new Vector3(0.02f, 0.25f, length);
            MapLayoutTools.Box(parent, "LineStripe", center, size, material, collider: false);
        }

        /// <summary>
        /// 천장(ceilingY)에 매단 역명판: 흰 발광판 + 노선 색 띠 + 매단 줄. 판 바닥은 바닥에서 2.9m. 충돌체 없음.
        /// </summary>
        private static void HangingSign(Transform parent, StationMaterials m, Vector3 floorPoint, float ceilingY, Material band)
        {
            float signY = floorPoint.y + 3.2f;
            MapLayoutTools.Box(parent, "StationSign", new Vector3(floorPoint.x, signY, floorPoint.z), new Vector3(3f, 0.6f, 0.08f), m.signWhite, collider: false);
            MapLayoutTools.Box(parent, "StationSignBand", new Vector3(floorPoint.x, signY - 0.2f, floorPoint.z), new Vector3(3.02f, 0.12f, 0.1f), band, collider: false);
            float rodHeight = ceilingY - (signY + 0.3f);
            if (rodHeight <= 0f)
                return;
            foreach (float offset in new[] { -1.3f, 1.3f })
            {
                MapLayoutTools.Box(parent, "SignRod", new Vector3(floorPoint.x + offset, signY + 0.3f + rodHeight / 2f, floorPoint.z), new Vector3(0.03f, rodHeight, 0.03f),
                    m.rail, collider: false);
            }
        }

        #endregion

        #region 조명·지점·분위기

        /// <summary>
        /// 층별 천장 형광등(B2 차가운 빛, B1 따뜻한 빛, 지상 지붕 아래 흰빛, 다리 아래 보조등)과 방 조명. 모두 사냥 시간에 꺼지는 MapLight다.
        /// </summary>
        private static void BuildLights(Context c)
        {
            Material on = c.materials.lampOn;
            Material off = c.materials.lampOff;
            var cool = new Color(0.85f, 0.93f, 1f);
            var warm = new Color(1f, 0.9f, 0.76f);
            var orange = new Color(1f, 0.72f, 0.45f);

            void Panel(float x, float y, float z, Color color, float intensity, float range, float yaw = 0f)
                => MapPiecePlacer.CreatePanelLamp(c.lights, new Vector3(x, y, z), color, intensity, range, on, off, yaw);

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

        /// <summary>
        /// 마네킹 스폰(지하 서쪽)·가드 스폰 후보(지상·다리)·전원 장치 후보(층마다)를 사물과 겹치지 않는 빈자리에 만든다.
        /// </summary>
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

        /// <summary>
        /// 역 분위기: 하늘 없음, 어두운 푸른 환경광, 실시간 조명만 쓰는 조명 설정.
        /// </summary>
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
        /// 배치도(지붕을 숨긴 지상층)와 층별 눈높이 장면을 찍어 Temp/TransferStation에 저장한다.
        /// </summary>
        private static void CapturePreviews(Context c, Bounds playArea)
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
            MapPreviewCapture.Capture(PreviewFolder, playArea, c.roof.Where(r => r != null).ToList(), shots);
        }

        /// <summary>
        /// 에셋 실제 크기와 크기 보정 내역을 Temp/TransferStation/sizes.txt에 남긴다.
        /// </summary>
        private static void WriteReport(Context c, int converted, int colliders, int props, int walls, int skipped)
        {
            Directory.CreateDirectory(PreviewFolder);
            string text = $"변신 사물 {props}, 제외 {skipped}, 벽 {walls}, 충돌체 추가 {colliders}, 머티리얼 변환 {converted}, 없는 에셋 {c.placer.MissingCount}\n" +
                          "\n[크기 보정]\n" + string.Join("\n", c.sizeFixes) +
                          "\n\n[에셋 크기 (가로 x 높이 x 깊이, 회전 전)]\n" + c.placer.DescribeSizes();
            File.WriteAllText(Path.Combine(PreviewFolder, "sizes.txt"), text);
        }

        #endregion

        /// <summary>
        /// 환승역 머티리얼(Art/MapKit/Materials/TransferStation). 없으면 URP Lit으로 만든다.
        /// </summary>
        private sealed class StationMaterials
        {
            private const string Folder = "Assets/_Project/Art/MapKit/Materials/TransferStation";

            public Material floor, platformFloor, ballast, ceiling, wall, tile, trim, concrete, stair, metal, line1, line2, safetyYellow,
                rail, sleeper, tunnel, carBody, carFloor, seat, door, rubber, glass, lightStrip, gate, gateGlow, shutter,
                signWhite, exitGreen, emergency, screen, adGlow, infoGlow, ceramic, lampOn, lampOff;

            /// <summary>MapKit 머티리얼 이름 → 역 머티리얼. (소품에는 적용하지 않음, 구조물 배치용)</summary>
            public Dictionary<string, Material> KitRemap { get; private set; }

            /// <summary>
            /// 지하철 소품 FBX 머티리얼 이름 → 역 머티리얼. 발광 부분(FBX 가져오기는 발광을 옮기지 않음)과
            /// 도기(어두운 역에서 매끈한 면이 검게 보이지 않게)를 바꿔 끼운다.
            /// </summary>
            public Dictionary<string, Material> PropRemap { get; private set; }

            /// <summary>
            /// 머티리얼을 불러오거나 만든다.
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
