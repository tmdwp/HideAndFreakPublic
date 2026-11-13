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
    /// <summary>로비 배경에 가드가 구울에게 쫓기는 장면을 프리팹으로 만들어 넣는다.</summary>
    public static class LobbyBackdropBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/Lobby/LobbyChaseRoom.prefab";
        private const string MaterialFolder = "Assets/_Project/Art/Materials";
        private const string RootName = "LobbyChaseRoom";
        private const string CameraPointName = "CameraPoint";

        // 방 크기(m)
        private const float RoomWidth = 24f;
        private const float RoomDepth = 16f;
        private const float RoomHeight = 4f;
        private const float WallThickness = 0.3f;

        // 경로 중심(방 기준)과 카메라
        private static readonly Vector3 PathCenter = new Vector3(0f, 0f, 1f);
        private static readonly Vector3 CameraPosition = new Vector3(0f, 2.4f, -7f);
        private static readonly Vector3 CameraLookAt = new Vector3(0f, 0.7f, 1f);
        private const float CameraFieldOfView = 55f;

        // 어두운 환경
        private static readonly Color AmbientColor = new Color(0.035f, 0.035f, 0.045f);
        private static readonly Color FogColor = new Color(0.01f, 0.01f, 0.015f);
        private const float FogDensity = 0.05f;

        /// <summary>로비 씬을 다시 만들지 않고 배경만 바꿀 수 있도록 열린 씬에 넣는다.</summary>
        [MenuItem(EditorMenus.Root + "로비/쫓기는 배경 연출 넣기 (열린 씬)")]
        public static void AddToOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            AddToScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[LobbyBackdropBuilder] '{scene.name}' 씬에 배경 연출을 넣었습니다. 씬을 저장하세요.");
        }

        /// <summary>직접 고친 배치가 망가졌을 때 처음 모양으로 되돌릴 수 있게 둔다.</summary>
        [MenuItem(EditorMenus.Root + "로비/쫓기는 배경 프리팹 다시 만들기")]
        public static void RebuildPrefab()
        {
            if (!EditorUtility.DisplayDialog("배경 프리팹 다시 만들기",
                    "LobbyChaseRoom 프리팹을 처음 모양으로 덮어씁니다. 직접 고친 배치는 사라집니다.", "다시 만들기", "취소"))
                return;
            BuildPrefab();
            Debug.Log($"[LobbyBackdropBuilder] 배경 프리팹을 다시 만들었습니다: {PrefabPath}");
        }

        /// <summary>직접 고친 배치를 지키려고 프리팹이 있으면 그대로 쓰고, 두 벌이 되지 않게 기존 인스턴스는 지운다.</summary>
        internal static void AddToScene(Scene scene)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                prefab = BuildPrefab();

            foreach (GameObject existingInstance in scene.GetRootGameObjects().Where(rootObject => rootObject.name == RootName))
                Object.DestroyImmediate(existingInstance);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetAsFirstSibling();
            PlaceCamera(scene, instance.transform.Find(CameraPointName));
            ApplyDarkEnvironment();
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

        /// <summary>
        /// 손전등과 전구만 빛나 보여야 쫓기는 긴장감이 살아서 하늘을 끄고 환경광을 아주 어둡게, 안개를 짙게 한다.
        /// </summary>
        private static void ApplyDarkEnvironment()
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
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();

            var root = new GameObject(RootName);
            BuildRoom(root.transform);
            BuildLights(root.transform);

            LobbyChasePath chasePath = new GameObject("ChasePath").AddComponent<LobbyChasePath>();
            chasePath.transform.SetParent(root.transform, false);
            chasePath.transform.localPosition = PathCenter;

            Transform guard = CreateCharacter(root.transform, "Guard", PlayerRole.Guard, CharacterAssetBuilder.GuardSkinPath, out _);
            AddFlashlight(guard);
            Transform ghoul = CreateCharacter(root.transform, "Ghoul", PlayerRole.Mannequin, CharacterAssetBuilder.GhoulSkinPath, out CharacterSkinView ghoulSkin);

            LobbyChaseLoop chaseLoop = root.AddComponent<LobbyChaseLoop>();
            TestEnvironmentSetupTool.SetField(chaseLoop, "path", chasePath);
            TestEnvironmentSetupTool.SetField(chaseLoop, "runner", guard);
            TestEnvironmentSetupTool.SetField(chaseLoop, "chaser", ghoul);
            TestEnvironmentSetupTool.SetField(chaseLoop, "chaserSkin", ghoulSkin);

            // 씬 뷰에서도 시작 위치가 보이도록 미리 놓는다.
            SetPose(guard, chasePath.Evaluate(0f));
            SetPose(ghoul, chasePath.Evaluate(-new SerializedObject(chaseLoop).FindProperty("baseGap").floatValue));

            var cameraPoint = new GameObject(CameraPointName).transform;
            cameraPoint.SetParent(root.transform, false);
            cameraPoint.localPosition = CameraPosition;
            cameraPoint.localRotation = Quaternion.LookRotation(CameraLookAt - CameraPosition, Vector3.up);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>텅 빈 방은 깊이감이 없어 기둥과 상자를 둔다.</summary>
        private static void BuildRoom(Transform root)
        {
            Material floorMaterial = LoadOrCreateMaterial("M_LobbyFloor", new Color(0.09f, 0.09f, 0.09f), 0.35f);
            Material wallMaterial = LoadOrCreateMaterial("M_LobbyWall", new Color(0.16f, 0.16f, 0.15f), 0.1f);
            Material propMaterial = LoadOrCreateMaterial("M_LobbyProp", new Color(0.18f, 0.13f, 0.09f), 0.15f);

            Transform room = new GameObject("Room").transform;
            room.SetParent(root, false);

            float halfWidth = RoomWidth * 0.5f;
            float halfDepth = RoomDepth * 0.5f;
            CreateBlock(room, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(RoomWidth, 0.2f, RoomDepth), floorMaterial);
            CreateBlock(room, "Ceiling", new Vector3(0f, RoomHeight + 0.1f, 0f), new Vector3(RoomWidth, 0.2f, RoomDepth), wallMaterial);
            CreateBlock(room, "Wall_Back", new Vector3(0f, RoomHeight * 0.5f, halfDepth), new Vector3(RoomWidth, RoomHeight, WallThickness), wallMaterial);
            CreateBlock(room, "Wall_Front", new Vector3(0f, RoomHeight * 0.5f, -halfDepth), new Vector3(RoomWidth, RoomHeight, WallThickness), wallMaterial);
            CreateBlock(room, "Wall_Left", new Vector3(-halfWidth, RoomHeight * 0.5f, 0f), new Vector3(WallThickness, RoomHeight, RoomDepth), wallMaterial);
            CreateBlock(room, "Wall_Right", new Vector3(halfWidth, RoomHeight * 0.5f, 0f), new Vector3(WallThickness, RoomHeight, RoomDepth), wallMaterial);

            // 경로 안쪽 기둥: 뒤쪽을 지나는 캐릭터가 잠깐 가려져 깊이감이 생긴다.
            CreateBlock(room, "Pillar_L", PathCenter + new Vector3(-3.2f, RoomHeight * 0.5f, 0f), new Vector3(0.6f, RoomHeight, 0.6f), wallMaterial);
            CreateBlock(room, "Pillar_R", PathCenter + new Vector3(3.2f, RoomHeight * 0.5f, 0f), new Vector3(0.6f, RoomHeight, 0.6f), wallMaterial);

            CreateBlock(room, "Crate_1", new Vector3(-9.5f, 0.5f, 6.5f), Vector3.one, propMaterial);
            CreateBlock(room, "Crate_2", new Vector3(-8.4f, 0.4f, 6.8f), Vector3.one * 0.8f, propMaterial);
            CreateBlock(room, "Crate_3", new Vector3(-9.3f, 1.25f, 6.6f), Vector3.one * 0.5f, propMaterial);
            CreateBlock(room, "Crate_4", new Vector3(9.8f, 0.6f, 5.8f), new Vector3(1.4f, 1.2f, 1.2f), propMaterial);
        }

        /// <summary>
        /// 불안한 분위기를 내려고 경로 위 전구는 깜빡이게 하고, 구석에 붉은 비상등을 둬 완전히 깜깜하지 않게 한다.
        /// </summary>
        private static void BuildLights(Transform root)
        {
            Light ceilingLamp = CreateLight(root, "CeilingLamp", LightType.Point, PathCenter + new Vector3(0f, RoomHeight - 0.3f, 0f),
                new Color(1f, 0.82f, 0.6f), 2.2f, 9f);
            ceilingLamp.gameObject.AddComponent<FlickeringLight>();

            CreateLight(root, "EmergencyLight", LightType.Point, new Vector3(RoomWidth * 0.5f - 1.5f, 2.6f, RoomDepth * 0.5f - 0.6f),
                new Color(1f, 0.12f, 0.08f), 0.9f, 6f);
        }

        /// <summary>
        /// 쫓기는 가드가 앞을 비추며 달려야 장면이 읽히므로 가슴 높이에 그림자 있는 스포트라이트를 붙인다.
        /// </summary>
        private static void AddFlashlight(Transform guard)
        {
            Light flashlight = CreateLight(guard, "Flashlight", LightType.Spot, new Vector3(0.2f, 1.35f, 0.35f),
                new Color(1f, 0.96f, 0.88f), 4f, 14f);
            flashlight.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            flashlight.spotAngle = 50f;
            flashlight.innerSpotAngle = 25f;
            flashlight.shadows = LightShadows.Soft;
        }

        /// <summary>외형을 바꾸면 로비에도 반영되도록 게임과 같은 외형 컴포넌트를 쓴다.</summary>
        private static Transform CreateCharacter(Transform root, string characterName, PlayerRole role, string skinPath, out CharacterSkinView skinView)
        {
            var character = new GameObject(characterName).transform;
            character.SetParent(root, false);
            var visualRoot = new GameObject("Visual").transform;
            visualRoot.SetParent(character, false);

            CharacterSkin skin = AssetDatabase.LoadAssetAtPath<CharacterSkin>(skinPath);
            if (skin == null)
                Debug.LogWarning($"[LobbyBackdropBuilder] 외형이 없습니다: {skinPath} — 메뉴 \"캐릭터·무기 에셋 생성\"을 먼저 실행하세요.");

            skinView = character.gameObject.AddComponent<CharacterSkinView>();
            var serializedSkinView = new SerializedObject(skinView);
            serializedSkinView.FindProperty("role").intValue = (int)role;
            serializedSkinView.ApplyModifiedPropertiesWithoutUndo();
            TestEnvironmentSetupTool.SetField(skinView, "defaultSkin", skin);
            TestEnvironmentSetupTool.SetField(skinView, "visualRoot", visualRoot);
            return character;
        }

        /// <summary>
        /// 배경 장식이라 충돌할 일이 없으므로 기본 상자의 충돌체는 지운다.
        /// </summary>
        private static void CreateBlock(Transform parent, string blockName, Vector3 position, Vector3 size, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = blockName;
            Object.DestroyImmediate(block.GetComponent<Collider>());
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = size;
            block.GetComponent<Renderer>().sharedMaterial = material;
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

        /// <summary>사용자가 색을 다듬었을 수 있어 이미 있으면 덮어쓰지 않는다.</summary>
        private static Material LoadOrCreateMaterial(string materialName, Color color, float smoothness)
        {
            string materialPath = $"{MaterialFolder}/{materialName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, materialPath);
            return material;
        }

        /// <summary>
        /// 경로 계산 결과(Pose)를 그대로 넘길 수 있게 둔 도우미.
        /// </summary>
        private static void SetPose(Transform target, Pose pose)
        {
            target.SetPositionAndRotation(pose.position, pose.rotation);
        }

        #endregion
    }
}
