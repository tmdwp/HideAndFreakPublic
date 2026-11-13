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
    /// <summary>지하 연구소 맵과 같은 구성의 테스트 맵을 항상 같은 결과로 만든다.</summary>
    public static class UndergroundLabBuilder
    {
        private const string MapName = "Underground Lab";
        internal const string PreviewFolder = "Temp/UndergroundLab";
        private static string LabScenePath => $"{MapCatalog.CustomMapFolder}/Map_Laboratory.unity";

        private const float Cell = 4f;
        private const float MinX = -24f, MaxX = 24f, MinZ = -20f, MaxZ = 20f;
        private const float RoomHeight = 3f;
        private const float AtriumHalf = 8f;
        private const float AtriumHeight = 6f;

        // 벽이 놓이는 격자선.
        private static readonly float[] VerticalLines = { -24f, -12f, -8f, 8f, 12f, 24f };
        private static readonly float[] HorizontalLines = { -20f, -12f, -8f, 8f, 12f, 20f };
        // 복도가 지나가 벽을 비우는 중심선.
        private static readonly float[] CorridorCenters = { -10f, 10f };

        private enum Opening { Wall, Open, Door, Window }

        // 벽 구간의 출입구(없으면 막힌 벽).
        private static readonly Dictionary<string, Opening> Openings = new Dictionary<string, Opening>
        {
            // 표본 연구실(북서)
            { "H12:-18", Opening.Open }, { "V-12:14", Opening.Door },
            // 기계실(북)
            { "H12:2", Opening.Open }, { "V-8:18", Opening.Door }, { "V8:14", Opening.Door },
            // 수술실(북동)
            { "H12:18", Opening.Open }, { "V12:18", Opening.Door },
            // 격리 병동(서)
            { "V-12:-2", Opening.Open }, { "V-12:6", Opening.Window }, { "V-12:-6", Opening.Window }, { "H8:-18", Opening.Door },
            // 중앙 실험실
            { "V-8:2", Opening.Open }, { "V8:-2", Opening.Open }, { "H-8:2", Opening.Open }, { "H-8:-6", Opening.Window },
            // 사무·서버실(동)
            { "V12:-6", Opening.Door }, { "V12:2", Opening.Open }, { "V12:6", Opening.Window }, { "H8:14", Opening.Door },
            // 창고(남서)
            { "H-12:-14", Opening.Open }, { "V-12:-18", Opening.Door },
            // 휴게실(남)
            { "H-12:-6", Opening.Door }, { "H-12:6", Opening.Open }, { "V8:-18", Opening.Door },
            // 안치실(남동)
            { "H-12:18", Opening.Door }, { "V12:-14", Opening.Open },
        };

        // 방 안 칸막이.
        private static readonly (bool vertical, float line, float center, Opening kind)[] Partitions =
        {
            // 격리 병동 독방.
            (true, -20f, -6f, Opening.Door), (true, -20f, -2f, Opening.Door), (true, -20f, 2f, Opening.Door), (true, -20f, 6f, Opening.Door),
            (false, -4f, -22f, Opening.Wall), (false, 0f, -22f, Opening.Wall), (false, 4f, -22f, Opening.Wall),
            // 사무실과 서버실 사이.
            (true, 20f, -6f, Opening.Wall), (true, 20f, -2f, Opening.Window), (true, 20f, 2f, Opening.Door), (true, 20f, 6f, Opening.Wall),
        };

        private static readonly Vector3[] MannequinSpawns =
        {
            new Vector3(-18f, 0f, 16.5f), new Vector3(-14.5f, 0f, 13.5f), new Vector3(-15f, 0f, 1f), new Vector3(-14f, 0f, -5.5f),
            new Vector3(-18f, 0f, -16.5f), new Vector3(-4f, 0f, -16f), new Vector3(-10f, 0f, -2f), new Vector3(-16f, 0f, 10f),
            new Vector3(-16f, 0f, -10f), new Vector3(-5f, 0f, 2f)
        };

        private static readonly Vector3[] GuardSpawns =
        {
            new Vector3(10f, 0f, 16.5f), new Vector3(16f, 0f, 4f), new Vector3(17f, 0f, 13.5f), new Vector3(18f, 0f, -16f), new Vector3(21.5f, 0f, -1.5f)
        };

        private static readonly Vector3[] GeneratorSpots =
        {
            new Vector3(-17f, 0f, 15.5f), new Vector3(-1f, 0f, 15f), new Vector3(16.5f, 0f, 18f), new Vector3(-16.5f, 0f, -2f),
            new Vector3(6f, 0f, 2.5f), new Vector3(18f, 0f, 1.5f), new Vector3(-19.5f, 0f, -17.5f), new Vector3(5f, 0f, -16f),
            new Vector3(21f, 0f, -16.5f), new Vector3(-2f, 0f, 10f), new Vector3(14f, 0f, -10f), new Vector3(10f, 0f, -16f)
        };

        /// <summary>
        /// 만드는 동안 함께 쓰는 상태.
        /// </summary>
        private sealed class Context : MapLayoutTools
        {
            public LabMaterials materials;
            public Transform lights;
            public readonly List<Renderer> ceilings = new List<Renderer>();
        }

        #region 메뉴

        /// <summary>테스트 맵이 게임 맵과 달라지면 테스트를 믿을 수 없으므로 두 맵을 같은 구성으로 함께 만든다.</summary>
        [MenuItem(EditorMenus.Root + "맵 생성/지하 연구소 (Map_Laboratory)")]
        private static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            BuildScene(LabScenePath, capturePreview: true);
            BuildScene(TestEnvironmentSetupTool.ScenePath, capturePreview: false);
            EditorSceneManager.OpenScene(LabScenePath, OpenSceneMode.Single);
        }

        #endregion

        /// <summary>손으로 고친 배치와 섞이지 않도록 빈 씬부터 새로 만든다.</summary>
        /// <param name="capturePreview">true면 Temp/UndergroundLab에 배치도·장면 사진을 남긴다.</param>
        public static void BuildScene(string scenePath, bool capturePreview)
        {
            // 빌더는 프리팹을 놓으므로 빠진 맵 물체 프리팹을 먼저 만든다.
            MapObjectPrefabBuilder.EnsurePrefabs();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Transform mapRoot = new GameObject(MapAuthoringTools.MapRootName).transform;

            LabMaterials materials = LabMaterials.LoadOrCreate();
            var context = new Context
            {
                materials = materials,
                placer = new MapPiecePlacer(materials.KitRemap),
                environment = MapLayoutTools.CreateChild(mapRoot, MapAuthoringTools.EnvironmentName),
                lights = MapLayoutTools.CreateChild(mapRoot, "Lights")
            };
            context.lengthYaw = MapLayoutTools.MeasureLengthYaw(Kit.Wall);

            BuildShell(context);
            BuildAtrium(context);
            BuildSpecimenLab(context);
            BuildMachineRoom(context);
            BuildSurgery(context);
            BuildContainment(context);
            BuildOffice(context);
            BuildStorage(context);
            BuildBreakRoom(context);
            BuildMorgue(context);
            BuildCorridors(context);

            // 정리가 등록 컴포넌트를 지우지 않도록 변신 사물 등록을 마지막에 한다.
            MapSceneBuilder.CleanEnvironment(context.environment);
            int converted = UrpMaterialConverter.ConvertRenderers(context.environment.GetComponentsInChildren<Renderer>(true));
            int colliders = MapSceneBuilder.AddMissingColliders(context.environment);
            context.placer.RegisterCandidates(out int propCount, out int skipped);
            Physics.SyncTransforms();

            BuildLights(context);
            CreatePoints(mapRoot);

            NavMeshSurface surface = mapRoot.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            var playArea = new Bounds(Vector3.zero, new Vector3(MaxX - MinX, 0f, MaxZ - MinZ));
            float ceilingHeight = MapSceneBuilder.CreateBoundary(playArea, context.environment);

            MapInfo mapInfo = mapRoot.gameObject.AddComponent<MapInfo>();
            var info = new SerializedObject(mapInfo);
            info.FindProperty("mapName").stringValue = MapName;
            info.FindProperty("ceilingHeight").floatValue = ceilingHeight;
            info.ApplyModifiedPropertiesWithoutUndo();
            // 위에서 보면 천장만 보이므로 대기 화면은 발코니에서 내려다본다.
            var overviewPosition = new Vector3(0f, 4.6f, 7.4f);
            GameSystemsSceneBuilder.SetOverview(mapInfo, overviewPosition, Quaternion.LookRotation(new Vector3(0f, 0.5f, -6f) - overviewPosition));

            ApplyUndergroundAtmosphere(scene);

            // 최적화(오클루전 굽기)에 저장된 경로가 필요하므로 먼저 저장한다.
            EditorSceneManager.SaveScene(scene, scenePath);
            MapOptimizer.Optimize(scene, indoor: true);
            if (capturePreview)
                CapturePreviews(context, playArea);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            WriteReport(context, converted, colliders, propCount, skipped);
            Debug.Log($"[UndergroundLabBuilder] 지하 연구소 완료: {scenePath}\n" +
                      $"변신 사물 {propCount}개(크기·구성으로 제외 {skipped}개), 충돌체 추가 {colliders}개, 머티리얼 변환 {converted}개, " +
                      $"없는 에셋 {context.placer.MissingCount}개. 에셋 크기 목록: {PreviewFolder}/sizes.txt");
        }

        #region 뼈대 (바닥·천장·벽)

        /// <summary>MapKit 모델이 4m 단위라 4m 격자선 위에 벽을 세워 이음매 없이 맞춘다.</summary>
        private static void BuildShell(Context context)
        {
            Transform floors = context.Group("Floors");
            Transform ceilings = context.Group("Ceilings");
            for (float x = MinX + Cell / 2f; x < MaxX; x += Cell)
            {
                for (float z = MinZ + Cell / 2f; z < MaxZ; z += Cell)
                {
                    GameObject floor = context.placer.Place(Kit.Floor, floors, new Vector3(x, -0.2f, z), 0f, name: "Floor");
                    Paint(floor, context.materials.floor);

                    float height = IsInAtrium(x, z) ? AtriumHeight : RoomHeight;
                    GameObject ceiling = context.placer.Place(Kit.Floor, ceilings, new Vector3(x, height, z), 0f, name: "Ceiling");
                    Paint(ceiling, context.materials.ceiling);
                    if (ceiling != null)
                        context.ceilings.AddRange(ceiling.GetComponentsInChildren<Renderer>());
                }
            }

            Transform walls = context.Group("Walls");
            foreach (float x in VerticalLines)
            {
                for (float z = MinZ + Cell / 2f; z < MaxZ; z += Cell)
                    AddGridWall(context, walls, vertical: true, x, z);
            }
            foreach (float z in HorizontalLines)
            {
                for (float x = MinX + Cell / 2f; x < MaxX; x += Cell)
                    AddGridWall(context, walls, vertical: false, z, x);
            }
            foreach ((bool vertical, float line, float center, Opening kind) in Partitions)
                PlaceWall(context, walls, kind, vertical, line, center, 0f);
        }

        /// <summary>복도가 막히지 않도록 교차 구간은 비우고, 6m 천장에 맞게 중앙 실험실 둘레는 한 단 더 쌓는다.</summary>
        private static void AddGridWall(Context context, Transform parent, bool vertical, float line, float center)
        {
            bool perimeter = vertical ? Mathf.Approximately(Mathf.Abs(line), MaxX) : Mathf.Approximately(Mathf.Abs(line), MaxZ);
            if (!perimeter && CorridorCenters.Any(corridor => Mathf.Approximately(corridor, center)))
                return;

            Opening kind = Openings.TryGetValue(Key(vertical, line, center), out Opening found) ? found : Opening.Wall;
            if (kind != Opening.Open)
                PlaceWall(context, parent, kind, vertical, line, center, 0f);

            bool atriumEdge = Mathf.Approximately(Mathf.Abs(line), AtriumHalf) && Mathf.Abs(center) < AtriumHalf;
            if (atriumEdge)
                PlaceWall(context, parent, Opening.Wall, vertical, line, center, RoomHeight);
        }

        /// <summary>출입구 종류마다 다른 벽 모델을 쓰도록 종류를 받아 모델을 고른다.</summary>
        private static void PlaceWall(Context context, Transform parent, Opening kind, bool vertical, float line, float center, float baseY)
        {
            string path = kind switch
            {
                Opening.Door => Kit.WallDoor,
                Opening.Window => Kit.WallWindow,
                _ => Kit.Wall
            };
            Vector3 position = vertical ? new Vector3(line, baseY, center) : new Vector3(center, baseY, line);
            float yaw = (vertical ? 90f : 0f) + context.lengthYaw;
            context.placer.Place(path, parent, position, yaw, remap: true, name: $"Wall_{Key(vertical, line, center)}");
        }

        /// <summary>실수 좌표는 비교 오차가 있어 표의 키로 쓸 수 없으므로 방향 + 반올림한 정수 좌표로 키를 만든다.</summary>
        private static string Key(bool vertical, float line, float center)
        {
            return $"{(vertical ? 'V' : 'H')}{Mathf.RoundToInt(line)}:{Mathf.RoundToInt(center)}";
        }

        /// <summary>중앙 실험실(천장 6m) 안은 천장 높이와 벽 단 수가 달라 칸마다 구별해야 한다.</summary>
        private static bool IsInAtrium(float x, float z)
        {
            return Mathf.Abs(x) < AtriumHalf && Mathf.Abs(z) < AtriumHalf;
        }

        #endregion

        #region 구역

        /// <summary>
        /// 중앙 실험실: 북쪽 2m 높이 통제실 발코니(계단·난간·관제 책상), 가운데 표본 수조 3개, 실험대·이동 침대.
        /// </summary>
        private static void BuildAtrium(Context context)
        {
            Transform group = context.Group("Atrium");

            // 발코니 받침과 앞 난간(가운데는 계단 자리).
            for (float x = -6f; x <= 6f; x += 4f)
            {
                GameObject block = context.placer.Place(Kit.Platform, group, new Vector3(x, 0f, 6f), 0f, scale: new Vector3(2f, 2f, 2f), name: "Balcony");
                Paint(block, context.materials.concrete);
            }
            foreach (float x in new[] { -7f, -5f, -3f, -1.6f, 1.6f, 3f, 5f, 7f })
                context.placer.Place(Kit.Railing, group, new Vector3(x, 2f, 4.1f), context.lengthYaw, remap: true, name: "Railing");
            PlaceStairsRisingNorth(context, group, new Vector3(0f, 0f, 2.5f));

            // 발코니 위 관제 책상 (앉는 쪽이 남쪽)
            GameObject desk = context.Prop(group, Subway.OfficeDesk, -4f, 6.6f, 0f, 0.8f, baseY: 2f);
            context.Decorate(desk, Subway.Monitor, Vector2.zero, 180f, 0.45f);
            context.Prop(group, Subway.OfficeChair, -4f, 5.3f, 0f, 1f, baseY: 2f);
            GameObject desk2 = context.Prop(group, Subway.OfficeDesk, 3.5f, 6.6f, 0f, 0.8f, baseY: 2f);
            context.Decorate(desk2, Subway.Monitor, new Vector2(-0.3f, 0f), 180f, 0.45f);
            context.Prop(group, Kit.Cabinet, 6.8f, 7.4f, 180f, 1f, baseY: 2f);
            context.Prop(group, Subway.ElectricPanel, 0f, 7.6f, 180f, 1.5f, baseY: 2f, transformable: false);

            // 표본 수조
            CreateSpecimenTube(context, group, new Vector3(-3.5f, 0f, -1f));
            CreateSpecimenTube(context, group, new Vector3(3.5f, 0f, -1f));
            CreateSpecimenTube(context, group, new Vector3(0f, 0f, -4.5f));

            // 바닥 소품
            GameObject bench = context.Prop(group, Kit.Table, -6f, -5.5f, 90f, 0.8f);
            context.Decorate(bench, Subway.Monitor, Vector2.zero, 90f, 0.45f);
            context.Prop(group, Subway.CleaningCart, 5.5f, -6f, 0f, 1f);
            context.Prop(group, Kit.Cabinet, -6.5f, -2.5f, 90f, 1f);
            context.Against(group, Kit.Cabinet, MapSide.South, -AtriumHalf, -2f, 1f);
            context.Prop(group, Kit.BoxStack, 2f, -7f, 15f, 0.7f);
        }

        /// <summary>
        /// 표본 연구실(북서): 벽을 따라 실험대와 약품장, 유리 기구와 표본 병.
        /// </summary>
        private static void BuildSpecimenLab(Context context)
        {
            Transform group = context.Group("SpecimenLab");
            GameObject table1 = context.Against(group, Kit.Table, MapSide.North, MaxZ, -22f, 0.8f);
            context.Decorate(table1, Subway.Monitor, new Vector2(-0.2f, 0f), 180f, 0.45f);
            context.Decorate(table1, Kit.Bucket, new Vector2(0.45f, 0f), 180f, 0.3f);
            GameObject table2 = context.Against(group, Kit.Table, MapSide.North, MaxZ, -18.5f, 0.8f);
            context.Decorate(table2, Kit.CrateSmall, Vector2.zero, 180f, 0.6f);
            context.Decorate(table2, Kit.Box, new Vector2(0.5f, 0.1f), 0f, 0.35f);

            context.Against(group, Kit.Shelf, MapSide.West, MinX, 16f, 1.9f);
            context.Against(group, Kit.Shelf, MapSide.West, MinX, 13.4f, 1.9f);
            GameObject table3 = context.Prop(group, Kit.Table, -15f, 17.5f, 90f, 0.75f);
            context.Decorate(table3, Kit.Bucket, new Vector2(0f, 0.3f), 0f, 0.3f);
            context.Decorate(table3, Kit.Box, new Vector2(0.1f, -0.4f), 0f, 0.35f);
            context.Prop(group, Subway.OfficeChair, -21f, 17.4f, 0f, 1f);
            context.Against(group, Kit.Cabinet, MapSide.East, -12f, 18.5f, 1f);
            context.Against(group, Kit.Cabinet, MapSide.South, 12f, -22.5f, 1f);
        }

        /// <summary>
        /// 기계실(북): 큰 보일러, 배전반, 드럼통, 벽을 따라 지나는 배관.
        /// </summary>
        private static void BuildMachineRoom(Context context)
        {
            Transform group = context.Group("MachineRoom");
            context.Against(group, Subway.AirHandler, MapSide.North, MaxZ, -4f, 2.5f, transformable: false);
            context.Against(group, Subway.ElectricPanel, MapSide.North, MaxZ, 1.5f, 1.5f);
            context.Against(group, Subway.EmergencyBox, MapSide.North, MaxZ, 4f, 1.5f);
            context.Against(group, Subway.ElectricPanel, MapSide.North, MaxZ, 6.5f, 1.5f);
            context.Prop(group, Kit.Barrel, -7f, 13f, 0f, 0.9f);
            context.Prop(group, Kit.Barrel, -6.1f, 13.4f, 40f, 0.9f);
            context.Prop(group, Kit.Barrel, -7.1f, 14.2f, 0f, 0.9f);
            GameObject crate = context.Prop(group, Kit.Crate, 5f, 13.6f, 10f, 1f);
            context.Decorate(crate, Kit.CrateSmall, Vector2.zero, 35f, 0.6f);
            foreach (float x in new[] { 0f, 2f, 4f, 6f })
                context.placer.Place(Kit.Pipe, group, new Vector3(x, 2.45f, 19.6f), context.lengthYaw, name: "Pipe");
        }

        /// <summary>
        /// 수술실(북동): 가운데 수술대, 벽을 따라 약품장·세면대·들것, 이동 침대.
        /// </summary>
        private static void BuildSurgery(Context context)
        {
            Transform group = context.Group("Surgery");
            context.Prop(group, Kit.Bed, 19.5f, 16.5f, 90f, 1f);
            context.Against(group, Kit.Shelf, MapSide.North, MaxZ, 22f, 1.9f);
            context.Against(group, Subway.WashBasin, MapSide.North, MaxZ, 15f, 0.9f);
            context.Against(group, Kit.Bed, MapSide.East, MaxX, 14.5f, 1f);
            context.Prop(group, Subway.CleaningCart, 21.5f, 14.2f, 0f, 1f);
            context.Prop(group, Kit.Bed, 14.5f, 13.6f, 90f, 1f);
            context.Prop(group, Kit.TrashBin, 23.3f, 19.3f, 0f, 0.65f);
            context.Against(group, Kit.Bed, MapSide.East, MaxX, 17.8f, 1f);
            context.Against(group, Kit.Shelf, MapSide.West, 12f, 13.6f, 1.9f);
            context.Against(group, Kit.Cabinet, MapSide.North, MaxZ, 18.5f, 1f);
        }

        /// <summary>서쪽 격리 병동의 소품을 놓는다.</summary>
        private static void BuildContainment(Context context)
        {
            Transform group = context.Group("Containment");
            float[] cells = { -6f, -2f, 2f, 6f };
            for (int i = 0; i < cells.Length; i++)
            {
                float cz = cells[i];
                context.Against(group, Kit.Bed, MapSide.West, MinX, cz - 0.8f, 1f, yaw: 90f);
                if (i == 1)
                    context.Against(group, Subway.Toilet, MapSide.West, MinX, cz + 1.3f, 0.8f);
                else
                    context.Prop(group, Kit.Bucket, -21f, cz + 1.3f, 0f, 0.3f);
            }

            GameObject desk = context.Prop(group, Subway.OfficeDesk, -15f, 5.6f, 0f, 0.8f);
            context.Decorate(desk, Subway.Monitor, Vector2.zero, 180f, 0.45f);
            context.Prop(group, Subway.OfficeChair, -15f, 4.4f, 0f, 1f);
            context.Against(group, Kit.LockerRow, MapSide.North, 8f, -13.9f, 1.9f);
            context.Prop(group, Kit.Chair, -17f, -6f, 30f, 1f);
            context.Prop(group, Subway.Bench, -18.6f, 0f, 90f, 0.5f);
            context.Prop(group, Kit.Bed, -16.5f, -2.8f, 0f, 1f);
            context.Against(group, Kit.Locker, MapSide.South, -8f, -14f, 2f);
            context.Prop(group, Kit.BoxStack, -18.5f, -6.8f, -10f, 0.7f);
        }

        /// <summary>동쪽 사무실·서버실의 소품을 놓는다.</summary>
        private static void BuildOffice(Context context)
        {
            Transform group = context.Group("Office");
            foreach (float z in new[] { 5.5f, -1.5f })
            {
                GameObject desk = context.Prop(group, Subway.OfficeDesk, 16.5f, z, 0f, 0.8f);
                context.Decorate(desk, Subway.Monitor, new Vector2(0.2f, 0f), 180f, 0.45f);
                context.Prop(group, Subway.OfficeChair, 16.5f, z - 1.1f, 0f, 1f);
            }
            context.Against(group, Kit.Cabinet, MapSide.South, -8f, 15f, 1f);
            context.Against(group, Kit.Cabinet, MapSide.South, -8f, 17f, 1f);
            context.Against(group, Kit.Cabinet, MapSide.North, 8f, 18f, 1f);
            context.Prop(group, Kit.Box, 18.8f, -6.8f, 20f, 0.35f);

            context.Against(group, Kit.LockerRow, MapSide.East, MaxX, 4.5f, 1.9f);
            context.Against(group, Kit.LockerRow, MapSide.East, MaxX, -4.5f, 1.9f);
            context.Against(group, Subway.ElectricPanel, MapSide.East, MaxX, 0f, 1.5f);
            context.Against(group, Subway.AirHandler, MapSide.North, 8f, 22f, 1f);
        }

        /// <summary>
        /// 창고(남서): 선반, 쌓인 나무 상자, 팔레트 위 상자, 포대 팔레트, 드럼통.
        /// </summary>
        private static void BuildStorage(Context context)
        {
            Transform group = context.Group("Storage");
            context.Against(group, Kit.Shelf, MapSide.West, MinX, -14.5f, 1.9f);
            context.Against(group, Kit.Shelf, MapSide.West, MinX, -17.5f, 1.9f);
            context.Against(group, Kit.Shelf, MapSide.South, MinZ, -21f, 2.2f);
            GameObject crate = context.Against(group, Kit.Crate, MapSide.South, MinZ, -17f, 1f);
            context.Decorate(crate, Kit.Crate, Vector2.zero, 8f, 1f);
            context.Against(group, Kit.CrateSmall, MapSide.South, MinZ, -15.8f, 0.6f);
            GameObject pallet = context.Prop(group, Kit.Pallet, -19.5f, -15.5f, 0f, 0.15f, transformable: false);
            context.Decorate(pallet, Kit.BoxStack, Vector2.zero, 0f, 0.7f);
            context.Prop(group, Kit.Crate, -17.5f, -13.6f, 90f, 1f);
            context.Prop(group, Kit.Barrel, -14.6f, -19.1f, 0f, 0.9f);
            context.Against(group, Kit.SandbagWall, MapSide.North, -12f, -21f, 1.2f, yaw: 0f);
        }

        /// <summary>
        /// 휴게실(남): 자판기·냉장고·소파, 식탁과 의자, 사물함.
        /// </summary>
        private static void BuildBreakRoom(Context context)
        {
            Transform group = context.Group("BreakRoom");
            context.Against(group, VendingMachine, MapSide.South, MinZ, -6f, 2f);
            context.Against(group, Kit.Locker, MapSide.South, MinZ, -3.6f, 1.8f);
            context.Against(group, Subway.Bench, MapSide.South, MinZ, 3f, 0.9f);
            context.Prop(group, Kit.Table, -1f, -15.5f, 0f, 0.8f);
            context.Prop(group, Kit.Chair, -2.2f, -15.5f, 90f, 0.9f);
            context.Prop(group, Kit.Chair, 0.2f, -15.5f, -90f, 0.9f);
            context.Against(group, Kit.LockerRow, MapSide.East, 8f, -14f, 1.9f);
            context.Prop(group, Kit.TrashBin, -7.3f, -17.2f, 0f, 0.65f);
            context.Prop(group, Kit.Stool, 2.6f, -13.6f, 0f, 0.65f);
        }

        /// <summary>
        /// 시체 안치실(남동): 벽면 냉동 보관함, 이동 침대·들것, 서랍장.
        /// </summary>
        private static void BuildMorgue(Context context)
        {
            Transform group = context.Group("Morgue");
            // 맞는 냉동 보관함 에셋이 없어 사물함 묶음으로 대신한다.
            context.Against(group, Kit.LockerRow, MapSide.South, MinZ, 15.5f, 1.9f);
            context.Against(group, Kit.LockerRow, MapSide.South, MinZ, 20.5f, 1.9f);
            context.Against(group, Kit.Shelf, MapSide.East, MaxX, -14f, 1.9f);
            context.Prop(group, Kit.Bed, 15f, -15f, 90f, 1f);
            context.Prop(group, Kit.Bed, 22.4f, -13.8f, 0f, 1f);
            context.Against(group, Kit.Bed, MapSide.East, MaxX, -17.5f, 1f);
            context.Against(group, Kit.Cabinet, MapSide.West, 12f, -18.5f, 1f);
            context.Prop(group, Kit.Bucket, 13.2f, -12.9f, 0f, 0.3f);
        }

        /// <summary>
        /// 복도: 막다른 끝의 엘리베이터(가드 진입로)와 상자 더미, 벽을 따라 놓인 소품, 천장 덕트·배관.
        /// </summary>
        private static void BuildCorridors(Context context)
        {
            Transform group = context.Group("Corridors");

            // 서쪽 복도
            context.Against(group, Kit.Crate, MapSide.North, MaxZ, -10.8f, 1f);
            context.Against(group, Kit.CrateSmall, MapSide.North, MaxZ, -9.2f, 0.6f);
            context.Prop(group, Kit.Barrel, -9.3f, 17.6f, 0f, 0.9f);
            context.Against(group, Kit.BoxStack, MapSide.West, -12f, 5f, 0.7f, yaw: 90f);
            context.Against(group, Subway.CleaningCart, MapSide.West, -12f, -5.5f, 1f);
            context.Prop(group, Kit.Pallet, -10f, -19f, 0f, 0.15f);
            context.Prop(group, Kit.Cone, -9f, -14f, 0f, 0.6f);

            // 막힌 벽 앞에 문 달린 벽을 세워 엘리베이터 문처럼 보이게 한다.
            context.Against(group, Kit.WallDoor, MapSide.North, MaxZ, 10f, 3f, yaw: 180f + context.lengthYaw, transformable: false);
            context.Against(group, Kit.CrateSmall, MapSide.East, 12f, -2.5f, 0.6f);
            context.Against(group, Kit.Box, MapSide.East, 12f, 5.5f, 0.35f);
            context.Against(group, Kit.Barrel, MapSide.East, 12f, -19f, 0.9f);

            // 북쪽 복도
            context.Against(group, Kit.Shelf, MapSide.West, MinX, 10f, 1.9f);
            context.Against(group, Subway.Bench, MapSide.North, 12f, -5f, 0.5f);
            context.Against(group, Kit.CrateSmall, MapSide.South, 8f, 6f, 0.6f);
            context.Prop(group, Kit.Cone, 16f, 11.2f, 0f, 0.6f);
            context.Against(group, Kit.Barrel, MapSide.East, MaxX, 10f, 0.9f);

            // 남쪽 복도
            context.Against(group, Kit.Locker, MapSide.West, MinX, -10f, 1.9f);
            context.Against(group, Kit.BoxStack, MapSide.North, -8f, -4f, 0.7f);
            context.Against(group, Kit.Crate, MapSide.South, -12f, 22.3f, 1f);

            // 천장 설비 (변신 대상 아님)
            foreach (float x in new[] { -20f, -15f, -5f, 5f, 15f, 20f })
                context.placer.Place(Kit.Duct, group, new Vector3(x, 2.5f, 10f), context.lengthYaw, name: "Duct");
            foreach (float x in new[] { -21f, -19f, -17f, -3f, -1f, 1f, 3f, 17f, 19f, 21f })
                context.placer.Place(Kit.Pipe, group, new Vector3(x, 2.55f, -11.6f), context.lengthYaw, name: "Pipe");
        }

        #endregion

        #region 배치 도우미

        /// <summary>
        /// 계단 모델은 임포트 축에 따라 높은 쪽 방향이 달라 발코니 반대로 오를 수 있으므로, 정점으로 높은 쪽을 확인해 북쪽(+Z)을 향하도록 필요하면 돌려 놓는다.
        /// </summary>
        private static void PlaceStairsRisingNorth(Context context, Transform group, Vector3 floorCenter)
        {
            GameObject stairs = context.placer.Place(Kit.Stairs, group, floorCenter, 0f, remap: true, name: "Stairs");
            if (stairs != null && HighSideDirectionZ(stairs) < 0f)
            {
                Object.DestroyImmediate(stairs);
                stairs = context.placer.Place(Kit.Stairs, group, floorCenter, 180f, remap: true, name: "Stairs");
            }
            Paint(stairs, context.materials.concrete);
        }

        /// <summary>
        /// 경계만으로는 어느 쪽이 높은지 알 수 없어, 위쪽 20% 높이에 있는 정점들의 평균이 경계 중심보다 +Z 쪽이면 양수를 돌려준다.
        /// </summary>
        private static float HighSideDirectionZ(GameObject target)
        {
            Bounds bounds = MapPiecePlacer.GetBounds(target);
            float threshold = bounds.max.y - bounds.size.y * 0.2f;
            float sum = 0f;
            int count = 0;
            foreach (MeshFilter filter in target.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                    continue;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 world = filter.transform.TransformPoint(vertex);
                    if (world.y < threshold)
                        continue;
                    sum += world.z - bounds.center.z;
                    count++;
                }
            }
            return count > 0 ? sum / count : 0f;
        }

        /// <summary>맞는 에셋이 없어 기본 도형으로 유리 수조를 만들고, 발광 액체를 어두운 연구소의 기준점으로 쓴다.</summary>
        private static void CreateSpecimenTube(Context context, Transform group, Vector3 floorPoint)
        {
            var root = new GameObject("SpecimenTube");
            root.transform.SetParent(group, false);
            root.transform.position = floorPoint;

            CreateCylinder(root.transform, "Base", new Vector3(0f, 0.2f, 0f), new Vector3(1.5f, 0.2f, 1.5f), context.materials.trim);
            GameObject liquid = CreateCylinder(root.transform, "Liquid", new Vector3(0f, 1.65f, 0f), new Vector3(1f, 1.25f, 1f), context.materials.liquid);
            Object.DestroyImmediate(liquid.GetComponent<Collider>());
            GameObject glass = CreateCylinder(root.transform, "Glass", new Vector3(0f, 1.65f, 0f), new Vector3(1.2f, 1.25f, 1.2f), context.materials.glass);
            glass.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            CreateCylinder(root.transform, "Cap", new Vector3(0f, 3.05f, 0f), new Vector3(1.5f, 0.15f, 1.5f), context.materials.trim);

            MapPiecePlacer.CreateBareLight(context.lights, "TubeGlow", floorPoint + Vector3.up * 1.6f, new Color(0.35f, 1f, 0.55f), 2f, 4.5f);
        }

        /// <summary>수조 조각마다 같은 생성 코드가 반복되어 한곳에 둔다.</summary>
        private static GameObject CreateCylinder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.localPosition = localPosition;
            cylinder.transform.localScale = localScale;
            cylinder.GetComponent<Renderer>().sharedMaterial = material;
            return cylinder;
        }

        /// <summary>이 파일 안에서 짧게 부를 수 있도록 공용 칠하기 함수를 그대로 넘긴다.</summary>
        private static void Paint(GameObject target, Material material) => MapLayoutTools.Paint(target, material);

        #endregion

        #region 조명·지점·분위기

        /// <summary>비슷한 방이 많아 길을 잃지 않도록 구역별 형광등 색을 다르게 한다.</summary>
        private static void BuildLights(Context context)
        {
            Material on = context.materials.lampOn;
            Material off = context.materials.lampOff;
            var cool = new Color(0.82f, 0.92f, 1f);
            var green = new Color(0.75f, 1f, 0.82f);
            var warm = new Color(1f, 0.88f, 0.72f);
            var orange = new Color(1f, 0.72f, 0.45f);
            var white = Color.white;
            var cold = new Color(0.65f, 0.8f, 1f);

            // 지정 위치·높이에 패널 조명 하나를 놓는다.
            void Panel(float x, float z, Color color, float intensity = 2.2f, float range = 8f, float yaw = 0f, float height = RoomHeight)
                => MapPiecePlacer.CreatePanelLamp(context.lights, new Vector3(x, height, z), color, intensity, range, on, off, yaw);

            // 구역
            Panel(-21f, 16f, green); Panel(-15f, 16f, green);
            Panel(-4f, 16f, orange); Panel(4f, 16f, orange);
            Panel(19.5f, 16.5f, white, 3f); Panel(14f, 15f, white, 1.8f);
            foreach (float cz in new[] { -6f, -2f, 2f, 6f })
                Panel(-22f, cz, cold, 1.2f, 5f, 90f);
            Panel(-16f, 4f, cool); Panel(-16f, -4f, cool);
            Panel(16f, 4f, cool); Panel(16f, -4f, cool); Panel(22f, 0f, cold, 1.8f, 7f, 90f);
            Panel(-21f, -16f, warm, 1.8f); Panel(-15f, -16f, warm, 1.8f);
            Panel(-3f, -16f, warm); Panel(3f, -16f, warm);
            Panel(15f, -16f, cold, 1.6f); Panel(21f, -16f, cold, 1.6f);

            // 중앙 실험실: 매달린 조명 4개 + 발코니 위 형광등
            foreach (Vector2 spot in new[] { new Vector2(-4f, -4f), new Vector2(4f, -4f), new Vector2(-4f, 1.5f), new Vector2(4f, 1.5f) })
            {
                GameObject pendant = context.placer.Place(Kit.PendantLamp, context.lights, new Vector3(spot.x, AtriumHeight - 0.8f, spot.y), 0f, name: "PendantLamp");
                if (pendant != null)
                {
                    // 공중의 조명에 부딪히거나 총알이 막히지 않도록 충돌체를 끈다.
                    foreach (Collider part in pendant.GetComponentsInChildren<Collider>())
                        part.enabled = false;
                    MapPiecePlacer.CreateBareLight(pendant.transform, "Glow", new Vector3(spot.x, AtriumHeight - 1f, spot.y), cool, 3f, 10f);
                }
            }
            Panel(-4f, 6f, cool, 2f, 7f, 0f, AtriumHeight);
            Panel(4f, 6f, cool, 2f, 7f, 0f, AtriumHeight);

            // 복도
            foreach (float x in new[] { -20f, -10f, 0f, 10f, 20f })
            {
                Panel(x, 10f, cool, 1.8f, 7f);
                Panel(x, -10f, cool, 1.8f, 7f);
            }
            foreach (float z in new[] { -16f, 0f })
            {
                Panel(-10f, z, cool, 1.8f, 7f, 90f);
                Panel(10f, z, cool, 1.8f, 7f, 90f);
            }
            Panel(-10f, 16f, cool, 1.8f, 7f, 90f);
            MapPiecePlacer.CreatePanelLamp(context.lights, new Vector3(10f, RoomHeight, 16f), new Color(1f, 0.22f, 0.15f), 2.5f, 7f,
                context.materials.emergency, off, 90f).name = "EmergencyLamp";
        }

        /// <summary>시작하자마자 마주치지 않도록 마네킹은 서쪽, 가드는 동쪽에서 시작하게 한다.</summary>
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

        /// <summary>지하라 하늘을 끄고 어두운 푸른 환경광을 쓴다.</summary>
        private static void ApplyUndergroundAtmosphere(Scene scene)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.2f);
            RenderSettings.fog = false;
            SceneLightingSetup.Apply(scene);
        }

        #endregion

        #region 확인용 결과

        /// <summary>
        /// Unity를 열지 않고도 결과를 확인할 수 있도록 배치도와 구역별 눈높이 장면을 찍어 Temp/UndergroundLab에 저장한다.
        /// </summary>
        private static void CapturePreviews(Context context, Bounds playArea)
        {
            const float eye = 1.7f;
            var shots = new List<MapPreviewCapture.Shot>
            {
                new MapPreviewCapture.Shot("01_Atrium_FromSouth", new Vector3(2f, eye, -10.5f), new Vector3(0f, 1.6f, 4f)),
                new MapPreviewCapture.Shot("02_Atrium_FromBalcony", new Vector3(-6f, 3.8f, 7f), new Vector3(3f, 0.5f, -5f)),
                new MapPreviewCapture.Shot("03_SpecimenLab", new Vector3(-13f, eye, 13f), new Vector3(-22f, 0.8f, 19f)),
                new MapPreviewCapture.Shot("04_MachineRoom", new Vector3(6.5f, eye, 13f), new Vector3(-5f, 1f, 19f)),
                new MapPreviewCapture.Shot("05_Surgery", new Vector3(13f, eye, 13f), new Vector3(22f, 0.8f, 19f)),
                new MapPreviewCapture.Shot("06_Containment", new Vector3(-13f, eye, 0f), new Vector3(-23f, 0.8f, -2f)),
                new MapPreviewCapture.Shot("07_Office", new Vector3(13f, eye, 7f), new Vector3(23f, 0.8f, -5f)),
                new MapPreviewCapture.Shot("08_Storage", new Vector3(-13f, eye, -13f), new Vector3(-23f, 0.8f, -19f)),
                new MapPreviewCapture.Shot("09_BreakRoom", new Vector3(7f, eye, -13f), new Vector3(-7f, 0.8f, -19f)),
                new MapPreviewCapture.Shot("10_Morgue", new Vector3(13f, eye, -13f), new Vector3(23f, 0.8f, -19f)),
                new MapPreviewCapture.Shot("11_NorthCorridor", new Vector3(-23f, eye, 10f), new Vector3(24f, 1.5f, 10f)),
                new MapPreviewCapture.Shot("12_Elevator", new Vector3(10f, eye, 9f), new Vector3(10f, 1.5f, 20f)),
            };
            var area = new Bounds(playArea.center, new Vector3(playArea.size.x, AtriumHeight, playArea.size.z));
            MapPreviewCapture.Capture(PreviewFolder, area, context.ceilings.Where(r => r != null).ToList(), shots);
        }

        /// <summary>
        /// 배치 좌표를 다듬을 때 에셋 실제 크기와 크기 보정 내역을 참고하도록 Temp/UndergroundLab/sizes.txt에 남긴다.
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

        /// <summary>지하 연구소 머티리얼을 없으면 URP Lit으로 만든다.</summary>
        private sealed class LabMaterials
        {
            private const string Folder ="Assets/_Project/Art/MapKit/Materials";

            public Material floor, ceiling, wall, trim, concrete, metal, glass, liquid, lampOn, lampOff, emergency;

            /// <summary>MapKit 머티리얼 이름 → 연구소 머티리얼.</summary>
            public Dictionary<string, Material> KitRemap { get; private set; }

            /// <summary>
            /// 빌더를 다시 실행해도 GUID가 유지되어 씬 참조가 끊기지 않도록 있으면 불러오고, 없을 때만 URP Lit으로 만든다.
            /// </summary>
            public static LabMaterials LoadOrCreate()
            {
                var factory = new MapMaterialFactory(Folder);
                var result = new LabMaterials
                {
                    floor = factory.Opaque("M_Lab_Floor", new Color(0.3f, 0.33f, 0.33f), 0f, 0.55f),
                    ceiling = factory.Opaque("M_Lab_Ceiling", new Color(0.36f, 0.38f, 0.39f), 0f, 0.2f),
                    wall = factory.Opaque("M_Lab_Wall", new Color(0.72f, 0.75f, 0.75f), 0f, 0.25f),
                    trim = factory.Opaque("M_Lab_Trim", new Color(0.15f, 0.19f, 0.21f), 0.4f, 0.45f),
                    concrete = factory.Opaque("M_Lab_Concrete", new Color(0.42f, 0.43f, 0.43f), 0f, 0.2f),
                    metal = factory.Opaque("M_Lab_Metal", new Color(0.45f, 0.5f, 0.55f), 0.6f, 0.5f),
                    glass = factory.Transparent("M_Lab_Glass", new Color(0.6f, 0.85f, 0.9f, 0.22f)),
                    liquid = factory.Emissive("M_Lab_Liquid", new Color(0.12f, 0.6f, 0.3f), new Color(0.2f, 1f, 0.45f) * 1.4f),
                    emergency = factory.Emissive("M_Lab_Emergency", new Color(0.6f, 0.05f, 0.03f), new Color(1f, 0.15f, 0.1f) * 2f),
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
                AssetDatabase.SaveAssets();
                return result;
            }
        }
    }
}
