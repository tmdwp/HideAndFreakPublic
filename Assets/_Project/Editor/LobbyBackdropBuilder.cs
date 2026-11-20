using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuardVsMannequin.Core;
using GuardVsMannequin.Map;
using GuardVsMannequin.Player;
using GuardVsMannequin.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 로비 배경을 밤의 편의점 안으로 만든다. 형광등이 깜빡이다 정전될 때마다 구울이 자동문 → 진열대 뒤 → 카메라 코앞 순서로 다가온다.
    /// 카메라는 가게 안쪽에서 앞쪽 통유리와 자동문을 바라본다.
    /// </summary>
    public static class LobbyBackdropBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/Lobby/LobbyStore.prefab";
        private const string MaterialFolder = "Assets/_Project/Art/Materials/Lobby";
        private const string RootName = "LobbyStore";
        // 예전 추격 배경이 남아 두 벌이 되지 않게 함께 지운다.
        private const string LegacyRootName = "LobbyChaseRoom";
        private const string CameraPointName = "CameraPoint";
        private const string PeekClipName = "Ghoul_Peek";
        private const string StareClipName = "Ghoul_Stare";

        // 가게 안쪽 크기(m). 앞 유리면은 +Z 끝.
        private const float StoreHalfWidth = 5f;
        private const float StoreHalfDepth = 4f;
        private const float StoreHeight = 3f;
        private const float FrontZ = StoreHalfDepth;

        // 앞 유리면 높이(m): 아래 걸레받이 위부터 간판 띠 아래까지.
        private const float GlassBottom = 0.3f;
        private const float GlassTop = 2.5f;

        // 자동문 가운데와 폭(m).
        private const float DoorCenterX = 1.1f;
        private const float DoorWidth = 1.8f;

        // 구울 손톱과 유리 사이 틈(m).
        private const float GhoulGlassGap = 0.03f;

        // 진열대 등장: 왼쪽 진열대 뒤(냉장고 통로) 자리(X, Z)와, 진열대 위로 드러낼 머리 높이(m).
        // Z는 진열대 위 붉은 간판(진열대 가운데마다 있음)에 머리가 가리지 않도록 두 진열대 사이 틈에 맞춘다.
        private static readonly Vector2 ShelfPeekPosition = new Vector2(-2.9f, -1.45f);
        private const float ShelfTop = 1.5f;
        private const float ShelfHeadShow = 0.48f;

        // 등장 장면 얼굴 조명 (밝기, 범위 m). 진열대는 멀어서 더 밝게 비춘다.
        private static readonly Vector2 ShelfFaceLight = new Vector2(1.4f, 2.6f);
        private static readonly Vector2 CloseFaceLight = new Vector2(0.5f, 2.2f);

        // 코앞 등장: 카메라 기준 (오른쪽, 위, 앞) 거리(m). 화면 오른쪽 끝에 무릎 위가 걸리는 거리.
        private static readonly Vector3 CloseOffset = new Vector3(1.0f, 0f, 1.7f);

        private static readonly Vector3 CameraPosition = new Vector3(0.6f, 1.65f, -3.5f);
        private static readonly Vector3 CameraLookAt = new Vector3(0.3f, 1.3f, FrontZ);
        private const float CameraFieldOfView = 62f;

        // 정전되면 바깥 가로등만 남도록 아주 어두운 밤 환경.
        private static readonly Color AmbientColor = new Color(0.02f, 0.025f, 0.04f);
        private static readonly Color FogColor = new Color(0.01f, 0.014f, 0.025f);
        private const float FogDensity = 0.03f;

        /// <summary>로비 씬을 다시 만들지 않고 배경만 바꿀 수 있도록 열린 씬에 넣는다.</summary>
        [MenuItem(EditorMenus.Root + "로비/편의점 배경 넣기 (열린 씬)")]
        public static void AddToOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            AddToScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[LobbyBackdropBuilder] '{scene.name}' 씬에 편의점 배경을 넣었습니다. 씬을 저장하세요.");
        }

        /// <summary>직접 고친 배치가 망가졌을 때 처음 모양으로 되돌릴 수 있게 둔다.</summary>
        [MenuItem(EditorMenus.Root + "로비/편의점 배경 프리팹 다시 만들기")]
        public static void RebuildPrefab()
        {
            if (!EditorUtility.DisplayDialog("편의점 배경 프리팹 다시 만들기",
                    "LobbyStore 프리팹을 처음 모양으로 덮어씁니다. 직접 고친 배치는 사라집니다.", "다시 만들기", "취소"))
                return;
            BuildPrefab();
            Debug.Log($"[LobbyBackdropBuilder] 편의점 배경 프리팹을 다시 만들었습니다: {PrefabPath}");
        }

        /// <summary>직접 고친 배치를 지키려고 프리팹이 있으면 그대로 쓰고, 두 벌이 되지 않게 기존 인스턴스는 지운다.</summary>
        internal static void AddToScene(Scene scene)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                prefab = BuildPrefab();
            if (prefab == null)
                return;

            foreach (GameObject existingInstance in scene.GetRootGameObjects().Where(rootObject => rootObject.name == RootName || rootObject.name == LegacyRootName))
                Object.DestroyImmediate(existingInstance);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetAsFirstSibling();
            PlaceCamera(scene, instance.transform.Find(CameraPointName));
            ApplyNightEnvironment();
        }

        #region 씬 설정

        /// <summary>카메라 구도는 연출의 일부라 프리팹 안의 CameraPoint로 메뉴 카메라를 옮긴다.</summary>
        private static void PlaceCamera(Scene scene, Transform cameraPoint)
        {
            Camera menuCamera = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(candidate => candidate.CompareTag("MainCamera"));
            if (menuCamera == null || cameraPoint == null)
            {
                Debug.LogWarning("[LobbyBackdropBuilder] MainCamera 또는 CameraPoint를 찾지 못해 카메라를 옮기지 않았습니다.");
                return;
            }

            menuCamera.transform.SetPositionAndRotation(cameraPoint.position, cameraPoint.rotation);
            menuCamera.fieldOfView = CameraFieldOfView;
            menuCamera.clearFlags = CameraClearFlags.SolidColor;
            menuCamera.backgroundColor = Color.black;
            EditorUtility.SetDirty(menuCamera.transform);
            EditorUtility.SetDirty(menuCamera);
        }

        /// <summary>정전 때 가게 안이 정말 깜깜해야 유리 밖 구울이 드러나므로 하늘을 끄고 환경광을 거의 없앤다.</summary>
        private static void ApplyNightEnvironment()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = AmbientColor;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = FogDensity;
        }

        #endregion

        #region 프리팹

        /// <summary>다시 만들어도 씬 인스턴스가 끊기지 않도록 같은 경로에 저장해 GUID를 유지한다.</summary>
        private static GameObject BuildPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            AssetDatabase.Refresh();
            LobbyMaterials materials = LobbyMaterials.LoadOrCreate();

            var root = new GameObject(RootName);
            var powered = new List<Renderer>();

            BuildShell(root.transform, materials);
            BuildStorefront(root.transform, materials);
            BuildOutside(root.transform, materials);
            powered.AddRange(BuildProps(root.transform, materials));
            List<FlickeringLight> storeLights = BuildCeilingLights(root.transform, materials, powered);

            StorePower power = root.AddComponent<StorePower>();
            TestEnvironmentSetupTool.SetArray(power, "lights", storeLights.Cast<Object>().ToArray());
            TestEnvironmentSetupTool.SetArray(power, "poweredRenderers", powered.Cast<Object>().ToArray());

            GameObject[] appearances = BuildAppearances(root.transform);
            LobbyBlackoutCycle cycle = root.AddComponent<LobbyBlackoutCycle>();
            TestEnvironmentSetupTool.SetField(cycle, "power", power);
            TestEnvironmentSetupTool.SetArray(cycle, "appearances", appearances.Cast<Object>().ToArray());

            var cameraPoint = new GameObject(CameraPointName).transform;
            cameraPoint.SetParent(root.transform, false);
            cameraPoint.localPosition = CameraPosition;
            cameraPoint.localRotation = Quaternion.LookRotation(CameraLookAt - CameraPosition, Vector3.up);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>바닥 타일·천장·옆벽·뒷벽. 바닥은 두 색 타일을 번갈아 깔아 원근감을 준다.</summary>
        private static void BuildShell(Transform root, LobbyMaterials materials)
        {
            Transform shell = MapLayoutTools.CreateChild(root, "Shell");
            const float tileSize = 1f;
            for (float x = -StoreHalfWidth; x < StoreHalfWidth - 0.01f; x += tileSize)
            {
                for (float z = -StoreHalfDepth; z < StoreHalfDepth - 0.01f; z += tileSize)
                {
                    bool isDark = (Mathf.RoundToInt(x) + Mathf.RoundToInt(z)) % 2 == 0;
                    MapLayoutTools.Box(shell, "FloorTile", new Vector3(x + tileSize / 2f, -0.05f, z + tileSize / 2f),
                        new Vector3(tileSize, 0.1f, tileSize), isDark ? materials.floorDark : materials.floorLight, collider: false);
                }
            }

            float width = StoreHalfWidth * 2f;
            float depth = StoreHalfDepth * 2f;
            MapLayoutTools.Box(shell, "Ceiling", new Vector3(0f, StoreHeight + 0.05f, 0f), new Vector3(width, 0.1f, depth), materials.ceiling, collider: false);
            MapLayoutTools.Box(shell, "Wall_Back", new Vector3(0f, StoreHeight / 2f, -StoreHalfDepth - 0.1f), new Vector3(width, StoreHeight, 0.2f), materials.wall, collider: false);
            MapLayoutTools.Box(shell, "Wall_Left", new Vector3(-StoreHalfWidth - 0.1f, StoreHeight / 2f, 0f), new Vector3(0.2f, StoreHeight, depth), materials.wall, collider: false);
            MapLayoutTools.Box(shell, "Wall_Right", new Vector3(StoreHalfWidth + 0.1f, StoreHeight / 2f, 0f), new Vector3(0.2f, StoreHeight, depth), materials.wall, collider: false);
        }

        /// <summary>
        /// 앞쪽 통유리면: 아래 걸레받이, 위 간판 띠, 세로 창틀 사이 유리, 가운데 오른쪽 자동문 두 짝.
        /// 유리는 투명해서 정전 때 바깥 가로등 빛 속의 구울이 비친다.
        /// </summary>
        private static void BuildStorefront(Transform root, LobbyMaterials materials)
        {
            Transform front = MapLayoutTools.CreateChild(root, "Storefront");
            float width = StoreHalfWidth * 2f;
            MapLayoutTools.Box(front, "Kickplate", new Vector3(0f, GlassBottom / 2f, FrontZ), new Vector3(width, GlassBottom, 0.12f), materials.trim, collider: false);
            MapLayoutTools.Box(front, "SignBand", new Vector3(0f, (GlassTop + StoreHeight) / 2f, FrontZ), new Vector3(width, StoreHeight - GlassTop, 0.15f), materials.signBand, collider: false);

            float doorLeft = DoorCenterX - DoorWidth / 2f;
            float doorRight = DoorCenterX + DoorWidth / 2f;
            float[] mullions = { -StoreHalfWidth, -3.3f, -1.6f, doorLeft, doorRight, 3.5f, StoreHalfWidth };
            foreach (float x in mullions)
                MapLayoutTools.Box(front, "Mullion", new Vector3(x, (GlassBottom + GlassTop) / 2f, FrontZ), new Vector3(0.08f, GlassTop - GlassBottom, 0.1f), materials.trim, collider: false);

            for (int i = 0; i < mullions.Length - 1; i++)
            {
                if (Mathf.Approximately(mullions[i], doorLeft))
                    continue;
                float paneCenter = (mullions[i] + mullions[i + 1]) / 2f;
                float paneWidth = mullions[i + 1] - mullions[i];
                Glass(front, new Vector3(paneCenter, (GlassBottom + GlassTop) / 2f, FrontZ), new Vector3(paneWidth, GlassTop - GlassBottom, 0.02f), materials.glass);
            }

            BuildSlidingDoor(front, materials, doorLeft);
        }

        /// <summary>문은 바닥까지 내려오는 유리 두 짝과 굵은 문틀, 세로 손잡이, 초록 "자동문" 표시로 창과 구분한다.</summary>
        private static void BuildSlidingDoor(Transform front, LobbyMaterials materials, float doorLeft)
        {
            Transform door = MapLayoutTools.CreateChild(front, "SlidingDoor");
            MapLayoutTools.Box(door, "DoorKick", new Vector3(DoorCenterX, 0.04f, FrontZ), new Vector3(DoorWidth, 0.08f, 0.14f), materials.trim, collider: false);
            float leafWidth = DoorWidth / 2f;
            for (int leaf = 0; leaf < 2; leaf++)
            {
                float leafCenter = doorLeft + leafWidth * (leaf + 0.5f);
                MapLayoutTools.Box(door, "LeafFrameTop", new Vector3(leafCenter, GlassTop - 0.05f, FrontZ - 0.02f), new Vector3(leafWidth, 0.1f, 0.06f), materials.trim, collider: false);
                MapLayoutTools.Box(door, "LeafFrameBottom", new Vector3(leafCenter, 0.14f, FrontZ - 0.02f), new Vector3(leafWidth, 0.12f, 0.06f), materials.trim, collider: false);
                float innerEdge = leaf == 0 ? doorLeft + leafWidth - 0.03f : doorLeft + leafWidth + 0.03f;
                MapLayoutTools.Box(door, "LeafFrameEdge", new Vector3(innerEdge, (0.08f + GlassTop) / 2f, FrontZ - 0.02f), new Vector3(0.06f, GlassTop - 0.08f, 0.06f), materials.trim, collider: false);
                MapLayoutTools.Box(door, "Handle", new Vector3(innerEdge + (leaf == 0 ? -0.1f : 0.1f), 1.1f, FrontZ - 0.07f), new Vector3(0.03f, 0.6f, 0.03f), materials.steel, collider: false);
                Glass(door, new Vector3(leafCenter, (0.2f + GlassTop - 0.1f) / 2f, FrontZ - 0.02f), new Vector3(leafWidth - 0.06f, GlassTop - 0.3f, 0.02f), materials.glass);
            }
            MapLayoutTools.Box(door, "AutoDoorSign", new Vector3(DoorCenterX, 1.45f, FrontZ - 0.035f), new Vector3(0.4f, 0.1f, 0.005f), materials.signGreen, collider: false);
            MapLayoutTools.Box(door, "DoorSensor", new Vector3(DoorCenterX, GlassTop + 0.08f, FrontZ - 0.1f), new Vector3(0.35f, 0.08f, 0.08f), materials.trim, collider: false);
        }

        /// <summary>유리는 그림자를 막으면 가게 안이 어두워지고, 충돌할 일도 없어 그림자와 충돌체를 끈다.</summary>
        private static void Glass(Transform parent, Vector3 center, Vector3 size, Material glass)
        {
            GameObject pane = MapLayoutTools.Box(parent, "Glass", center, size, glass, collider: false);
            pane.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>
        /// 유리 너머: 인도·차도·건너편 건물, 그리고 정전 때 구울을 뒤에서 비추는 가로등.
        /// 바깥 조명은 가게 전기와 따로라 정전 때도 켜져 있다.
        /// </summary>
        private static void BuildOutside(Transform root, LobbyMaterials materials)
        {
            Transform outside = MapLayoutTools.CreateChild(root, "Outside");
            MapLayoutTools.Box(outside, "Sidewalk", new Vector3(0f, -0.05f, FrontZ + 2.6f), new Vector3(24f, 0.1f, 5f), materials.sidewalk, collider: false);
            MapLayoutTools.Box(outside, "Curb", new Vector3(0f, -0.02f, FrontZ + 5.15f), new Vector3(24f, 0.16f, 0.3f), materials.curb, collider: false);
            MapLayoutTools.Box(outside, "Road", new Vector3(0f, -0.12f, FrontZ + 9.3f), new Vector3(24f, 0.1f, 8f), materials.asphalt, collider: false);
            MapLayoutTools.Box(outside, "Building_Across", new Vector3(0f, 5f, FrontZ + 14f), new Vector3(24f, 10f, 1f), materials.building, collider: false);
            foreach (Vector2 window in new[] { new Vector2(-6.5f, 2.2f), new Vector2(4.5f, 5.4f), new Vector2(-1.5f, 6.6f) })
                MapLayoutTools.Box(outside, "LitWindow", new Vector3(window.x, window.y, FrontZ + 13.45f), new Vector3(1.4f, 1f, 0.05f), materials.windowLit, collider: false);

            GameObject streetLamp = PlaceProp(outside, MapAssets.City.StreetLamp, new Vector3(3.4f, 0f, FrontZ + 4.6f), 180f, materials);
            if (streetLamp != null)
            {
                Light lampLight = CreateLight(outside, "StreetLampLight", LightType.Spot, new Vector3(3.4f, 4.9f, FrontZ + 3.45f),
                    new Color(0.62f, 0.74f, 1f), 6f, 12f);
                lampLight.transform.localRotation = Quaternion.Euler(78f, 180f, 0f);
                lampLight.spotAngle = 95f;
                lampLight.innerSpotAngle = 50f;
            }

            // 정전 때 구울 앞면이 완전히 검게 묻히지 않도록, 가로등 빛이 유리로 스며든 것처럼 유리 안쪽에 아주 약한 푸른 빛을 둔다.
            CreateLight(outside, "NightFill", LightType.Point, new Vector3(0.3f, 2.2f, FrontZ - 0.8f), new Color(0.45f, 0.55f, 0.85f), 0.5f, 3.5f);
        }

        /// <summary>
        /// 왼쪽 벽 냉장고, 가운데 왼쪽 진열대 두 줄, 오른쪽 계산대와 뒤 담배 진열대, 창가 소품.
        /// 정전 때 같이 꺼지도록 발광면이 있는 냉장고·계산대 렌더러를 돌려준다.
        /// </summary>
        private static List<Renderer> BuildProps(Transform root, LobbyMaterials materials)
        {
            Transform props = MapLayoutTools.CreateChild(root, "Props");
            var powered = new List<Renderer>();

            foreach (float z in new[] { -2.8f, -1.6f, -0.4f, 0.8f })
                AddPowered(powered, PlaceProp(props, MapAssets.City.FridgeCase, new Vector3(-StoreHalfWidth + 0.4f, 0f, z), 90f, materials));

            foreach (float z in new[] { -2.2f, -0.7f, 0.8f })
            {
                PlaceProp(props, MapAssets.City.ShopShelf, new Vector3(-2.1f, 0f, z), -90f, materials);
                PlaceProp(props, MapAssets.City.ShopShelf, new Vector3(-1.5f, 0f, z), 90f, materials);
            }
            PlaceProp(props, MapAssets.City.ShopShelf, new Vector3(-3.4f, 0f, FrontZ - 0.45f), 180f, materials);

            AddPowered(powered, PlaceProp(props, MapAssets.City.Counter, new Vector3(3.0f, 0f, 0.8f), -90f, materials));
            PlaceProp(props, MapAssets.City.ShopShelf, new Vector3(StoreHalfWidth - 0.35f, 0f, 0.8f), -90f, materials);
            PlaceProp(props, MapAssets.City.CrateStack, new Vector3(4.4f, 0f, FrontZ - 0.6f), 20f, materials);
            PlaceProp(props, MapAssets.Kit.BoxStack, new Vector3(-4.4f, 0f, -StoreHalfDepth + 0.5f), 10f, materials);
            PlaceProp(props, MapAssets.Kit.TrashBin, new Vector3(2.4f, 0f, FrontZ + 0.6f), 0f, materials);
            return powered;
        }

        /// <summary>
        /// 천장 형광등 2줄 × 3개. 등마다 깜빡이는 점광원과 발광 관을 둬, 평소에도 제각각 깜빡이고 정전 때는 함께 꺼진다.
        /// </summary>
        private static List<FlickeringLight> BuildCeilingLights(Transform root, LobbyMaterials materials, List<Renderer> powered)
        {
            Transform lights = MapLayoutTools.CreateChild(root, "CeilingLights");
            var storeLights = new List<FlickeringLight>();
            foreach (float x in new[] { -2.2f, 1.8f })
            {
                foreach (float z in new[] { -2.4f, 0f, 2.4f })
                {
                    Transform fixture = MapLayoutTools.CreateChild(lights, "Fluorescent");
                    fixture.localPosition = new Vector3(x, StoreHeight, z);
                    MapLayoutTools.Box(fixture, "Housing", new Vector3(0f, -0.04f, 0f), new Vector3(0.3f, 0.06f, 1.3f), materials.trim, collider: false);
                    GameObject tube = MapLayoutTools.Box(fixture, "Tube", new Vector3(0f, -0.085f, 0f), new Vector3(0.2f, 0.03f, 1.2f), materials.tube, collider: false);
                    tube.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    powered.Add(tube.GetComponent<Renderer>());

                    Light tubeLight = CreateLight(fixture, "Light", LightType.Point, new Vector3(0f, -0.3f, 0f), new Color(0.9f, 0.95f, 1f), 1.5f, 6.5f);
                    FlickeringLight flicker = tubeLight.gameObject.AddComponent<FlickeringLight>();
                    SetFloat(flicker, "noiseAmount", 0.12f);
                    SetFloat(flicker, "blinkChancePerSecond", 0.25f);
                    storeLights.Add(flicker);
                }
            }
            return storeLights;
        }

        /// <summary>
        /// 정전마다 순서대로 켤 구울 등장 장면: 1 자동문 유리에 손을 대고 들여다봄 → 2 왼쪽 진열대 뒤로 머리만 내밂 → 3 카메라 바로 앞 오른쪽.
        /// 정전이 거듭될수록 점점 가까워지도록 이 순서로 둔다.
        /// </summary>
        private static GameObject[] BuildAppearances(Transform root)
        {
            Transform appearanceRoot = MapLayoutTools.CreateChild(root, "GhoulAppearances");
            if (!GhoulSkinBuilder.PrepareImport())
                return System.Array.Empty<GameObject>();
            return new[]
            {
                BuildDoorAppearance(appearanceRoot),
                BuildShelfAppearance(appearanceRoot),
                BuildCloseAppearance(appearanceRoot)
            };
        }

        /// <summary>가게 안을 보도록 돌리고, 손톱이 유리 바로 앞에 오게 손이 나간 거리만큼 뒤로 뺀다.</summary>
        private static GameObject BuildDoorAppearance(Transform parent)
        {
            GameObject ghoul = CreatePosedGhoul(parent, "1_Door", PeekClipName, out Bounds bounds);
            ghoul.transform.localPosition = new Vector3(DoorCenterX, 0f, FrontZ + 0.01f + GhoulGlassGap + bounds.max.z);
            ghoul.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            return ghoul;
        }

        /// <summary>
        /// 진열대 뒤 냉장고 통로에 세우고, 머리 꼭대기가 진열대 위로 정해진 만큼만 나오게 높이를 내린다.
        /// 몸과 발은 진열대와 바닥 뒤에 가려 카메라에 보이지 않는다.
        /// </summary>
        private static GameObject BuildShelfAppearance(Transform parent)
        {
            GameObject ghoul = CreatePosedGhoul(parent, "2_Shelf", StareClipName, out Bounds bounds);
            var position = new Vector3(ShelfPeekPosition.x, ShelfTop + ShelfHeadShow - bounds.max.y, ShelfPeekPosition.y);
            ghoul.transform.localPosition = position;
            ghoul.transform.localRotation = FaceCamera(position);
            AddFaceLight(ghoul.transform, bounds, ShelfFaceLight.x, ShelfFaceLight.y);
            return ghoul;
        }

        /// <summary>카메라가 보는 방향 기준으로 오른쪽 앞에 세워 화면 오른쪽 끝에 크게 걸리게 하고 카메라를 노려보게 한다.</summary>
        private static GameObject BuildCloseAppearance(Transform parent)
        {
            GameObject ghoul = CreatePosedGhoul(parent, "3_Close", StareClipName, out Bounds bounds);
            Quaternion cameraYaw = Quaternion.Euler(0f, Quaternion.LookRotation(CameraLookAt - CameraPosition).eulerAngles.y, 0f);
            Vector3 position = CameraPosition + cameraYaw * CloseOffset;
            position.y = 0f;
            ghoul.transform.localPosition = position;
            ghoul.transform.localRotation = FaceCamera(position);
            AddFaceLight(ghoul.transform, bounds, CloseFaceLight.x, CloseFaceLight.y);
            return ghoul;
        }

        /// <summary>구울이 카메라를 정면으로 노려보도록 수평 방향만 카메라 쪽으로 돌린다.</summary>
        private static Quaternion FaceCamera(Vector3 position)
        {
            Vector3 toCamera = CameraPosition - position;
            toCamera.y = 0f;
            return Quaternion.LookRotation(toCamera, Vector3.up);
        }

        /// <summary>
        /// 정전 때 가게 안은 깜깜해 눈만 보이므로, 얼굴 앞에 아주 약한 푸른 빛을 둬 윤곽이 드러나게 한다.
        /// 등장 장면의 자식이라 장면과 함께 켜지고 꺼진다.
        /// </summary>
        private static void AddFaceLight(Transform ghoul, Bounds bounds, float intensity, float range)
        {
            var facePoint = new Vector3(0f, bounds.max.y - 0.15f, bounds.max.z + 0.45f);
            CreateLight(ghoul, "FaceLight", LightType.Point, facePoint, new Color(0.55f, 0.65f, 0.95f), intensity, range);
        }

        /// <summary>
        /// 구울 모델에 클립 첫 프레임을 입혀 뼈 자세째로 굳힌다. 로비에서는 움직이지 않으므로 애니메이터를 없애 매 프레임 계산을 아낀다.
        /// </summary>
        /// <param name="bounds">굳힌 자세의 구울 기준 경계. 유리·진열대에 맞춰 세우는 데 쓴다.</param>
        private static GameObject CreatePosedGhoul(Transform parent, string appearanceName, string clipName, out Bounds bounds)
        {
            bounds = new Bounds(new Vector3(0f, 1.2f, 0f), new Vector3(1f, 2.4f, 1f));
            var ghoul = new GameObject(appearanceName);
            ghoul.transform.SetParent(parent, false);

            CharacterSkin skin = AssetDatabase.LoadAssetAtPath<CharacterSkin>(CharacterAssetBuilder.GhoulSkinPath);
            if (skin == null || skin.modelPrefab == null)
            {
                Debug.LogWarning($"[LobbyBackdropBuilder] 구울 외형이 없습니다: {CharacterAssetBuilder.GhoulSkinPath} — 메뉴 \"캐릭터·무기 에셋 생성\"을 먼저 실행하세요.");
                ghoul.SetActive(false);
                return ghoul;
            }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(skin.modelPrefab, ghoul.transform);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.transform.localRotation = Quaternion.Euler(0f, skin.yawOffset, 0f);

            AnimationClip clip = LoadClip(GhoulSkinBuilder.SourcePath, clipName);
            if (clip != null)
                clip.SampleAnimation(model, 0f);
            else
                Debug.LogWarning($"[LobbyBackdropBuilder] {clipName} 클립이 없습니다. generate_ghoul.py로 SK_Ghoul.fbx를 다시 만들었는지 확인하세요.");

            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
                Object.DestroyImmediate(animator);

            bounds = PosedBounds(ghoul.transform);
            ghoul.SetActive(false);
            return ghoul;
        }

        /// <summary>FBX 안의 클립은 이름으로만 구분되므로 미리보기용 복사본을 빼고 찾는다.</summary>
        private static AnimationClip LoadClip(string modelPath, string clipName)
        {
            return AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                .FirstOrDefault(clip => clip.name == clipName && !clip.name.StartsWith("__preview__"));
        }

        /// <summary>
        /// 자세를 바꾸면 손·머리 위치가 달라지므로, 스킨 메시를 지금 자세로 구워 구울 기준 경계를 잰다.
        /// </summary>
        private static Bounds PosedBounds(Transform ghoul)
        {
            bool hasPoint = false;
            var bounds = new Bounds();
            var bakedMesh = new Mesh();
            foreach (SkinnedMeshRenderer skinned in ghoul.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skinned.BakeMesh(bakedMesh, true);
                foreach (Vector3 vertex in bakedMesh.vertices)
                {
                    Vector3 local = ghoul.InverseTransformPoint(skinned.transform.TransformPoint(vertex));
                    if (hasPoint)
                        bounds.Encapsulate(local);
                    else
                        bounds = new Bounds(local, Vector3.zero);
                    hasPoint = true;
                }
            }
            Object.DestroyImmediate(bakedMesh);
            return bounds;
        }

        /// <summary>
        /// 맵과 같은 소품 프리팹을 중첩 프리팹으로 놓아 소품을 고치면 로비에도 반영되게 하고, 발광 머티리얼은 로비용으로 바꿔 끼운다.
        /// </summary>
        private static GameObject PlaceProp(Transform parent, string prefabPath, Vector3 position, float yaw, LobbyMaterials materials)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[LobbyBackdropBuilder] 소품 프리팹이 없습니다: {prefabPath} — 메뉴 \"맵 물체/물체 프리팹 만들기·갱신\"을 먼저 실행하세요.");
                return null;
            }

            var prop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            prop.transform.localPosition = position;
            prop.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            MapLayoutTools.ReplaceMaterials(prop.transform, materials.PropRemap);
            return prop;
        }

        /// <summary>정전 때 함께 꺼지도록 소품의 렌더러를 전기 목록에 넣는다. 발광 슬롯만 실제로 줄어든다.</summary>
        private static void AddPowered(List<Renderer> powered, GameObject prop)
        {
            if (prop != null)
                powered.AddRange(prop.GetComponentsInChildren<Renderer>(true));
        }

        /// <summary>로비는 가볍게 돌아가야 하므로 기본은 그림자를 끈다.</summary>
        private static Light CreateLight(Transform parent, string lightName, LightType type, Vector3 position, Color color, float intensity, float range)
        {
            Light createdLight = new GameObject(lightName).AddComponent<Light>();
            createdLight.transform.SetParent(parent, false);
            createdLight.transform.localPosition = position;
            createdLight.type = type;
            createdLight.color = color;
            createdLight.intensity = intensity;
            createdLight.range = range;
            createdLight.shadows = LightShadows.None;
            return createdLight;
        }

        /// <summary>조정값은 비공개 직렬화 필드라 SerializedObject로 넣는다.</summary>
        private static void SetFloat(Object target, string fieldName, float value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"[LobbyBackdropBuilder] {target.GetType().Name}.{fieldName} 필드를 찾을 수 없습니다.");
                return;
            }
            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        #endregion

        /// <summary>편의점 배경 머티리얼 묶음. 다시 만들어도 GUID가 유지되도록 공용 팩토리로 만든다.</summary>
        private sealed class LobbyMaterials
        {
            public Material floorLight, floorDark, ceiling, wall, trim, steel, signBand, signGreen, glass;
            public Material tube, glowWhite, lampHead, sidewalk, curb, asphalt, building, windowLit;

            /// <summary>FBX 가져오기는 발광을 옮기지 않으므로 소품 발광 머티리얼을 로비 발광 머티리얼로 바꿔 끼운다.</summary>
            public Dictionary<string, Material> PropRemap { get; private set; }

            /// <summary>빌더를 다시 실행하면 색 조정이 반영되도록 매번 값을 새로 쓴다.</summary>
            public static LobbyMaterials LoadOrCreate()
            {
                var factory = new MapMaterialFactory(MaterialFolder);
                var result = new LobbyMaterials
                {
                    floorLight = factory.Opaque("M_Lobby_FloorLight", new Color(0.78f, 0.78f, 0.75f), 0f, 0.55f),
                    floorDark = factory.Opaque("M_Lobby_FloorDark", new Color(0.66f, 0.67f, 0.65f), 0f, 0.55f),
                    ceiling = factory.Opaque("M_Lobby_Ceiling", new Color(0.8f, 0.8f, 0.78f), 0f, 0.15f),
                    wall = factory.Opaque("M_Lobby_Wall", new Color(0.86f, 0.86f, 0.83f), 0f, 0.3f),
                    trim = factory.Opaque("M_Lobby_Trim", new Color(0.32f, 0.34f, 0.36f), 0.7f, 0.45f),
                    steel = factory.Opaque("M_Lobby_Steel", new Color(0.7f, 0.72f, 0.74f), 0.85f, 0.6f),
                    signBand = factory.Opaque("M_Lobby_SignBand", new Color(0.12f, 0.42f, 0.25f), 0f, 0.4f),
                    signGreen = factory.Emissive("M_Lobby_SignGreen", new Color(0.15f, 0.6f, 0.3f), new Color(0.15f, 0.7f, 0.35f) * 0.8f),
                    glass = factory.Transparent("M_Lobby_Glass", new Color(0.55f, 0.68f, 0.75f, 0.12f)),
                    tube = factory.Emissive("M_Lobby_Tube", new Color(0.92f, 0.95f, 1f), new Color(0.9f, 0.95f, 1f) * 2f),
                    glowWhite = factory.Emissive("M_Lobby_GlowWhite", new Color(0.92f, 0.95f, 1f), new Color(0.9f, 0.95f, 1f) * 1.4f),
                    lampHead = factory.Emissive("M_Lobby_LampHead", new Color(0.8f, 0.88f, 1f), new Color(0.7f, 0.82f, 1f) * 2f),
                    sidewalk = factory.Opaque("M_Lobby_Sidewalk", new Color(0.4f, 0.39f, 0.37f), 0f, 0.2f),
                    curb = factory.Opaque("M_Lobby_Curb", new Color(0.55f, 0.54f, 0.52f), 0f, 0.2f),
                    asphalt = factory.Opaque("M_Lobby_Asphalt", new Color(0.12f, 0.12f, 0.13f), 0f, 0.3f),
                    building = factory.Opaque("M_Lobby_Building", new Color(0.1f, 0.1f, 0.12f), 0f, 0.1f),
                    windowLit = factory.Emissive("M_Lobby_WindowLit", new Color(0.95f, 0.8f, 0.55f), new Color(1f, 0.75f, 0.45f) * 0.9f),
                };
                result.PropRemap = new Dictionary<string, Material>
                {
                    { "M_City_GlowWhite", result.glowWhite },
                    // 가로등 발광판은 바깥 가로등 빛과 같은 푸른 흰색으로 맞춘다.
                    { "M_City_GlowWarm", result.lampHead },
                };
                return result;
            }
        }
    }
}
