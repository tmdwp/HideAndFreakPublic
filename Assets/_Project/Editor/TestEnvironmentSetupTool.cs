using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
using GuardVsMannequin.Audio;
using GuardVsMannequin.Cameras;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Map;
using GuardVsMannequin.Network;
using GuardVsMannequin.Objectives;
using GuardVsMannequin.Player;
using GuardVsMannequin.Player.Guard;
using GuardVsMannequin.Player.Emotes;
using GuardVsMannequin.Player.Mannequin;
using GuardVsMannequin.PropPhysics;
using GuardVsMannequin.Props;
using GuardVsMannequin.Room;
using GuardVsMannequin.Rules;
using GuardVsMannequin.UI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>다시 실행해도 항상 같은 결과가 나오도록 테스트 환경을 메뉴 한 번으로 덮어써 만든다.</summary>
    public static class TestEnvironmentSetupTool
    {
        private const string RootFolder = "Assets/_Project";
        internal const string MaterialFolder = RootFolder + "/Art/Materials";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        internal const string SoFolder = RootFolder + "/ScriptableObjects";
        internal const string SceneFolder = RootFolder + "/Scenes";

        internal const string InputActionsPath = RootFolder + "/Input/GameInput.inputactions";
        private const string GuardPrefabPath = PrefabFolder + "/Guard.prefab";
        private const string MannequinPrefabPath = PrefabFolder + "/Mannequin.prefab";
        private const string GeneratorPrefabPath = PrefabFolder + "/Generator.prefab";
        internal const string NoiseLibraryPath = SoFolder + "/NoiseLibrary.asset";
        internal const string ScenePath = SceneFolder + "/Game_Test.unity";
        internal const string LobbyScenePath = SceneFolder + "/Lobby.unity";
        internal const string RoomScenePath = SceneFolder + "/Room.unity";
        internal const string RoomStatePrefabPath = PrefabFolder + "/RoomState.prefab";
        private const string DefaultPrefabObjectsPath = "Assets/DefaultPrefabObjects.asset";

        /// <summary>창 제목·빌드 정보가 같은 이름을 쓰도록 한곳에 둔다.</summary>
        internal const string ProductName = "Hide&Freak";

        /// <summary>
        /// 새로 받은 프로젝트도 메뉴 한 번으로 플레이할 수 있게 머티리얼·프리팹·씬·빌드 설정을 의존 순서대로 모두 만든다.
        /// </summary>
        [MenuItem(EditorMenus.Root + "테스트 환경 생성")]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolders();

            // 텍스트 데이터 파일(문장, 설정 항목)을 수정한 뒤 바로 실행해도 최신 내용으로 UI를 만든다.
            Loc.Reload();
            GameSettingDefinitions.ClearCache();

            // 프리팹이 참조하므로 캐릭터 외형과 총 모델을 먼저 만든다.
            CharacterAssetBuilder.BuildAll();

            Dictionary<string, Material> materials = CreateMaterials();
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (inputActions == null)
            {
                Debug.LogError($"[TestSetup] 입력 에셋을 찾을 수 없습니다: {InputActionsPath}");
                return;
            }

            NoiseLibrary noiseLibrary = CreateNoiseLibrary();

            // 이동 구현이 등록되지 않으면 아바타 프리팹을 만들 수 없다.
            if (AvatarMovementRigs.Current == null)
            {
                Debug.LogError("[TestSetup] 아바타 이동 구현이 없어 프리팹을 만들 수 없습니다. ECM2를 설치하면 GVM_ECM2 기호가 켜지고 이동 어셈블리가 컴파일됩니다.");
                return;
            }

            var prefabs = new ScenePrefabs
            {
                guard = CreateGuardPrefab(inputActions, materials, noiseLibrary),
                mannequin = CreateMannequinPrefab(inputActions, materials, noiseLibrary),
                generator = CreateGeneratorPrefab(materials, noiseLibrary)
            };
            NetworkObject roomStatePrefab = CreateRoomStatePrefab();
            RegisterSpawnablePrefabs(prefabs.guard, prefabs.mannequin, prefabs.generator, roomStatePrefab);

            // 마지막에 시작 씬인 로비 씬이 열리도록 이 순서로 만든다.
            CreateTestScene();
            GameSystemsSceneBuilder.Build();
            MenuSceneBuilder.BuildRoomScene(RoomScenePath, AssetDatabase.LoadAssetAtPath<NetworkObject>(RoomStatePrefabPath));
            MenuSceneBuilder.BuildLobbyScene(LobbyScenePath, RoomScenePath,
                AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(DefaultPrefabObjectsPath));

            RegisterBuildScenes();
            // 저장 위치(PlayerPrefs, 로그)도 따르는 게임 이름.
            PlayerSettings.productName = ProductName;
            KeepLinearFogShaderVariants();
            SteamBuildProcessor.EnsureRunInBackground();

            AssetDatabase.SaveAssets();
            Debug.Log("[TestSetup] 테스트 환경 생성 완료. 로비 씬에서 Play 하세요: " + LobbyScenePath);
        }

        /// <summary>씬이 바뀌어도 참가자·준비 상태가 유지되도록 룸 상태를 전역 네트워크 오브젝트로 만든다.</summary>
        private static NetworkObject CreateRoomStatePrefab()
        {
            var root = new GameObject("RoomState");
            NetworkObject networkObject = root.AddComponent<NetworkObject>();
            var serialized = new SerializedObject(networkObject);
            serialized.FindProperty("_isGlobal").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // 맵 목록은 실행 중에 등록소에서 읽는다.
            root.AddComponent<RoomState>();
            return SavePrefab(root, RoomStatePrefabPath);
        }

        /// <summary>프리팹을 다시 만들면 FishNet 해시가 바뀌어 씬 참조가 끊기므로 이미 만든 프리팹을 불러와 재사용한다.</summary>
        internal static bool TryLoadScenePrefabs(out ScenePrefabs prefabs)
        {
            prefabs = new ScenePrefabs
            {
                guard = AssetDatabase.LoadAssetAtPath<NetworkObject>(GuardPrefabPath),
                mannequin = AssetDatabase.LoadAssetAtPath<NetworkObject>(MannequinPrefabPath),
                generator = AssetDatabase.LoadAssetAtPath<NetworkObject>(GeneratorPrefabPath)
            };
            return prefabs.guard != null && prefabs.mannequin != null && prefabs.generator != null;
        }

        /// <summary>시작 씬과 맵 번호가 PC끼리 같도록 정해진 순서로 등록하고, 없는 씬은 빌드 실패를 막으려고 건너뛴다.</summary>
        internal static void RegisterBuildScenes()
        {
            var paths = new List<string> { LobbyScenePath, RoomScenePath };
            if (File.Exists(GameSystemsSceneBuilder.ScenePath))
                paths.Add(GameSystemsSceneBuilder.ScenePath);
            foreach (MapCatalog.Entry map in MapCatalog.AllMaps)
            {
                if (File.Exists(map.ScenePath))
                    paths.Add(map.ScenePath);
            }
            SetBuildScenes(paths.ToArray());
        }

        #region 에셋

        /// <summary>
        /// 에셋 저장은 폴더가 없으면 실패하므로 새 프로젝트에서도 동작하도록 먼저 만든다.
        /// </summary>
        private static void EnsureFolders()
        {
            foreach (string folder in new[] { MaterialFolder, PrefabFolder, SoFolder, SceneFolder })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 에셋 팩이 없는 환경에서도 역할·사물을 구별할 수 있도록 단색 URP 머티리얼을 만든다.
        /// </summary>
        private static Dictionary<string, Material> CreateMaterials()
        {
            var litColors = new Dictionary<string, Color>
            {
                { "Guard", new Color(0.2f, 0.35f, 0.8f) },
                { "Mannequin", new Color(0.9f, 0.8f, 0.65f) },
                { "Visor", new Color(0.1f, 0.1f, 0.1f) },
                { "Floor", new Color(0.45f, 0.45f, 0.45f) },
                { "Wall", new Color(0.75f, 0.75f, 0.72f) },
                { "Prop", new Color(0.55f, 0.4f, 0.3f) },
                { "Generator", new Color(0.2f, 0.6f, 0.3f) },
                { "GeneratorDestroyed", new Color(0.15f, 0.15f, 0.15f) }
            };
            var unlitColors = new Dictionary<string, Color>
            {
                { "Tracer", new Color(1f, 0.9f, 0.3f) },
                { "ProgressBackground", new Color(0.1f, 0.1f, 0.1f) },
                { "ProgressFill", new Color(1f, 0.3f, 0.2f) }
            };

            var result = new Dictionary<string, Material>();
            CreateMaterialSet(litColors, Shader.Find("Universal Render Pipeline/Lit"), result);
            CreateMaterialSet(unlitColors, Shader.Find("Universal Render Pipeline/Unlit"), result);

            // 조명 기구 전구: 켜짐은 발광, 꺼짐은 어두운 유리
            CreateMaterialSet(new Dictionary<string, Color> { { "LampOff", new Color(0.2f, 0.2f, 0.22f) } }, Shader.Find("Universal Render Pipeline/Lit"), result);
            CreateMaterialSet(new Dictionary<string, Color> { { "LampOn", new Color(1f, 0.97f, 0.9f) } }, Shader.Find("Universal Render Pipeline/Lit"), result);
            Material lampOn = result["LampOn"];
            lampOn.EnableKeyword("_EMISSION");
            lampOn.SetColor("_EmissionColor", new Color(1f, 0.95f, 0.85f) * 3f);
            lampOn.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(lampOn);

            // 외곽선은 머티리얼 대신 OutlineRendererFeature가 그린다.
            return result;
        }

        /// <summary>
        /// GUID가 바뀌면 프리팹·씬의 머티리얼 참조가 끊기므로 이미 있으면 다시 만들지 않고 색만 갱신한다.
        /// </summary>
        private static void CreateMaterialSet(Dictionary<string, Color> colors, Shader shader, Dictionary<string, Material> result)
        {
            foreach (KeyValuePair<string, Color> pair in colors)
            {
                string path = $"{MaterialFolder}/M_{pair.Key}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, path);
                }
                material.shader = shader;
                material.SetColor("_BaseColor", pair.Value);
                EditorUtility.SetDirty(material);
                result[pair.Key] = material;
            }
        }

        /// <summary>
        /// 씬에서 스폰하는 네트워크 프리팹 묶음.
        /// </summary>
        internal struct ScenePrefabs
        {
            public NetworkObject guard;
            public NetworkObject mannequin;
            public NetworkObject generator;
        }

        /// <summary>
        /// 사용자가 넣은 소리 클립을 잃지 않도록 이미 있으면 기존 클립과 값은 유지하고 새 소음 종류만 추가한다.
        /// </summary>
        private static NoiseLibrary CreateNoiseLibrary()
        {
            NoiseLibrary library = AssetDatabase.LoadAssetAtPath<NoiseLibrary>(NoiseLibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<NoiseLibrary>();
                AssetDatabase.CreateAsset(library, NoiseLibraryPath);
            }

            if (library.AddMissingDefaults())
                EditorUtility.SetDirty(library);
            return library;
        }

        /// <summary>
        /// FishNet은 등록된 프리팹만 네트워크로 스폰할 수 있어 DefaultPrefabObjects에 등록한다.
        /// </summary>
        private static void RegisterSpawnablePrefabs(params NetworkObject[] prefabs)
        {
            var collection = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(DefaultPrefabObjectsPath);
            if (collection == null)
            {
                Debug.LogWarning("[TestSetup] DefaultPrefabObjects를 찾지 못했습니다. FishNet이 자동으로 생성/등록하는지 확인하세요.");
                return;
            }

            foreach (NetworkObject prefab in prefabs)
                collection.AddObject(prefab, checkForDuplicates: true, initializeAdded: false);

            // 같은 경로에 프리팹을 덮어쓰면 NetworkObject가 새로 만들어져 AssetPathHash가 0이 된다.
            // 이미 등록된 프리팹이라 FishNet 자동 생성기가 해시를 다시 계산하지 않으므로 직접 갱신한다.
            RefreshAssetPathHashes(collection);
            EditorUtility.SetDirty(collection);
        }

        /// <summary>공개 API가 없어 리플렉션으로 FishNet 프리팹 해시를 다시 계산한다.</summary>
        private static void RefreshAssetPathHashes(DefaultPrefabObjects collection)
        {
            MethodInfo method = typeof(DefaultPrefabObjects).GetMethod("SetAssetPathHashes", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(int) }, null);

            if (method == null)
            {
                Debug.LogError("[TestSetup] FishNet 프리팹 해시 갱신 메서드를 찾지 못했습니다. " +
                               "Tools > Fish-Networking > Utility > Reserialize NetworkObjects에서 프리팹을 재직렬화하세요.");
                return;
            }

            method.Invoke(collection, new object[] { 0 });
            AssetDatabase.SaveAssets();
        }

        #endregion

        #region 프리팹

        /// <summary>가드는 조준이 중요해 1인칭으로 만들고, 코드와 어긋나지 않게 컴포넌트 구성을 코드로 고정한다.</summary>
        private static NetworkObject CreateGuardPrefab(InputActionAsset inputActions, Dictionary<string, Material> materials, NoiseLibrary noiseLibrary)
        {
            GameObject root = CreateAvatarRoot("Guard", AvatarKind.Guard, inputActions, out Component character, out Behaviour[] ownerOnly);

            Renderer body = CreateVisual(root.transform, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(0.8f, 0.9f, 0.8f), materials["Guard"]);
            Renderer visor = CreateVisual(root.transform, PrimitiveType.Cube, "Visor", new Vector3(0f, 1.55f, 0.3f), new Vector3(0.5f, 0.15f, 0.2f), materials["Visor"]);

            Transform cameraPivot = new GameObject("CameraPivot").transform;
            cameraPivot.SetParent(root.transform, false);
            cameraPivot.localPosition = new Vector3(0f, 1.65f, 0f);
            Camera playerCamera = CreatePlayerCamera(cameraPivot, Vector3.zero);

            SetField(character, "cameraPivot", cameraPivot);
            ConfigureOwnership(root, playerCamera, ownerOnly, new[] { body, visor }, hideSkinForOwner: true);
            AddMoveSpeedController(root, PlayerRole.Guard);

            // 외형이 없으면 기본 도형이 보인다.
            AddSkinView(root, PlayerRole.Guard, CharacterAssetBuilder.GuardSkinPath, root.transform, new[] { body, visor });

            // 총: 소유자에게는 1인칭 리볼버, 다른 플레이어에게는 손에 든 리볼버
            GuardWeaponView weaponView = AddWeaponView(root, cameraPivot);

            // 사격: 조준 → 소음 → 총 → 총알 연출
            AvatarMovementRigs.Current.AddAimProvider(root);
            AddNoiseEmitter(root, noiseLibrary);
            GunTool gun = root.AddComponent<GunTool>();
            SetField(gun, "tracerView", CreateTracer(root.transform, cameraPivot, materials["Tracer"], weaponView));

            // 수류탄: 서버 궤적 판정 + 모든 클라이언트 연출
            AddGrenade(root);

            // Phase 3: 상하 시선 동기화, 사냥 시간 손전등
            AddViewSync(root, cameraPivot, cameraPivot, isThirdPerson: false);
            FlashlightTool flashlight = root.AddComponent<FlashlightTool>();
            SetField(flashlight, "spotLight", CreateFlashlight(cameraPivot));

            return SavePrefab(root, GuardPrefabPath);
        }

        /// <summary>손전등이 시선 방향을 비추도록 카메라 피벗 아래에 꺼진 스포트라이트를 둔다.</summary>
        private static Light CreateFlashlight(Transform cameraPivot)
        {
            var flashlightObject = new GameObject("Flashlight");
            flashlightObject.transform.SetParent(cameraPivot, false);
            flashlightObject.transform.localPosition = new Vector3(0.2f, -0.15f, 0.2f);

            Light spot = flashlightObject.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 25f;
            spot.spotAngle = 55f;
            spot.innerSpotAngle = 30f;
            spot.intensity = 8f;
            spot.color = new Color(1f, 0.95f, 0.85f);
            spot.shadows = LightShadows.Soft;
            spot.enabled = false;
            return spot;
        }

        /// <summary>마네킹은 변신한 자기 모습을 봐야 숨을 자리를 고를 수 있어 3인칭으로 만든다.</summary>
        private static NetworkObject CreateMannequinPrefab(InputActionAsset inputActions, Dictionary<string, Material> materials,
            NoiseLibrary noiseLibrary)
        {
            GameObject root = CreateAvatarRoot("Mannequin", AvatarKind.Mannequin, inputActions, out Component character, out Behaviour[] ownerOnly);

            // 외형은 맵이 바꿀 수 있다.
            Transform visual = CreateMannequinVisual(root.transform, materials, out Renderer body, out Renderer face);

            Transform followTarget = new GameObject("FollowTarget").transform;
            followTarget.SetParent(root.transform, false);
            followTarget.localPosition = new Vector3(0f, 1.5f, 0f);
            Camera playerCamera = CreatePlayerCamera(root.transform, new Vector3(0f, 2.5f, -3.5f));

            SetField(character, "followTarget", followTarget);
            ConfigureOwnership(root, playerCamera, ownerOnly, new Renderer[0]);
            AddMoveSpeedController(root, PlayerRole.Mannequin);

            // 변신과 상호작용.
            AvatarMovementRigs.Current.AddAimProvider(root);
            root.AddComponent<MannequinTransformer>();

            PropDisguiseView disguiseView = root.AddComponent<PropDisguiseView>();
            SetArray(disguiseView, "bodyRenderers", new Object[] { body, face });

            // 변신 중 총에 맞으면 진짜 사물처럼 날아가게 한다.
            root.AddComponent<DisguiseKnockback>();

            root.AddComponent<MannequinInteractor>();
            root.AddComponent<InteractAnimationView>();

            // 조준한 변신 가능 사물을 본인 화면에만 외곽선으로 표시한다.
            root.AddComponent<PropHighlighter>();

            // 수류탄 효과: 외곽선 표시·감속 동기화, 가드 화면의 벽 너머 외곽선
            AddGrenadeTargetComponents(root);

            // 소음, 벽타기, 감정 표현.
            AddNoiseEmitter(root, noiseLibrary);
            AvatarMovementRigs.Current.AddMannequinAbilities(root);
            AddEmotes(root);

            // 사냥 시간 모드와 근접 공격.
            MannequinHuntingMode huntingMode = root.AddComponent<MannequinHuntingMode>();
            SetArray(huntingMode, "bodyRenderers", new Object[] { body, face });

            // 사냥 시간 시야: 눈 높이의 개인 조명(본인 화면에만 켜짐) + 로컬 안개
            HuntVisionEffect huntVision = root.AddComponent<HuntVisionEffect>();
            SetField(huntVision, "visionLight", CreateHuntVisionLight(followTarget));
            root.AddComponent<MannequinAttackTool>();

            // 시선 동기화와 관전 시점.
            AddViewSync(root, null, followTarget, isThirdPerson: true);

            return SavePrefab(root, MannequinPrefabPath);
        }

        /// <summary>
        /// 역할마다 이동 속도 설정 항목이 달라서 어느 항목을 읽을지 알 수 있게 역할을 함께 지정한다.
        /// </summary>
        private static void AddMoveSpeedController(GameObject root, PlayerRole role)
        {
            MoveSpeedController speedController = root.AddComponent<MoveSpeedController>();
            var serialized = new SerializedObject(speedController);
            serialized.FindProperty("role").enumValueIndex = (int)role;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // 스피드핵·순간이동을 막도록 소유자 권한 이동을 서버에서 검증한다.
            root.AddComponent<MovementValidator>();
        }

        /// <summary>
        /// 다른 플레이어가 어디를 보는지 고개 방향으로 보여 주고, 죽은 사람이 이 아바타의 시야로 관전할 수 있도록 시선 동기화와 관전 시점을 붙인다.
        /// </summary>
        /// <param name="root">아바타 루트.</param>
        /// <param name="pitchPivot">원격 피어에서 상하 시선을 적용할 피벗(없으면 null).</param>
        /// <param name="eyePoint">관전 카메라의 눈 위치.</param>
        /// <param name="isThirdPerson">평소 3인칭으로 보는 역할인지 여부.</param>
        private static void AddViewSync(GameObject root, Transform pitchPivot, Transform eyePoint, bool isThirdPerson)
        {
            PlayerViewSync viewSync = root.AddComponent<PlayerViewSync>();
            SetField(viewSync, "pitchPivot", pitchPivot);

            SpectatorViewPoint viewPoint = root.AddComponent<SpectatorViewPoint>();
            SetField(viewPoint, "eyePoint", eyePoint);
            var serialized = new SerializedObject(viewPoint);
            serialized.FindProperty("isThirdPerson").boolValue = isThirdPerson;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>사냥 시간에 구울이 길을 찾도록 가드에게 위치를 알리지 않는 본인 전용 조명을 둔다.</summary>
        private static Light CreateHuntVisionLight(Transform followTarget)
        {
            var lightObject = new GameObject("HuntVisionLight");
            lightObject.transform.SetParent(followTarget, false);

            Light point = lightObject.AddComponent<Light>();
            point.type = LightType.Point;
            point.range = 12f;
            point.intensity = 1.5f;
            point.color = new Color(0.65f, 0.75f, 0.95f);
            point.shadows = LightShadows.None;
            point.enabled = false;
            return point;
        }

        /// <summary>외형 에셋이 없어도 앞뒤를 알 수 있게 얼굴 표시가 있는 기본 도형 외형을 만든다.</summary>
        private static Transform CreateMannequinVisual(Transform parent, Dictionary<string, Material> materials, out Renderer body, out Renderer face)
        {
            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(parent, false);

            body = CreateVisual(visual, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(0.8f, 0.9f, 0.8f), materials["Mannequin"]);
            face = CreateVisual(visual, PrimitiveType.Cube, "FaceMarker", new Vector3(0f, 1.5f, 0.3f), new Vector3(0.3f, 0.1f, 0.2f), materials["Visor"]);

            // 맵이 마네킹 외형을 지정하면 실행 시 그것으로 바뀐다.
            AddSkinView(parent.gameObject, PlayerRole.Mannequin, CharacterAssetBuilder.DummySkinPath, visual, new[] { body, face });
            return visual;
        }

        /// <summary>맵마다 외형을 바꿀 수 있도록 외형 컴포넌트를 붙이고 기본 도형을 대체 외형으로 연결한다.</summary>
        private static void AddSkinView(GameObject root, PlayerRole role, string defaultSkinPath, Transform visualRoot, Renderer[] fallback)
        {
            CharacterSkinView skinView = root.AddComponent<CharacterSkinView>();
            var serialized = new SerializedObject(skinView);
            serialized.FindProperty("role").enumValueIndex = (int)role;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SetField(skinView, "defaultSkin", AssetDatabase.LoadAssetAtPath<CharacterSkin>(defaultSkinPath));

            // 맵이 사냥 외형을 지정하지 않으면 구울로 바뀐다.
            if (role == PlayerRole.Mannequin)
                SetField(skinView, "huntSkin", AssetDatabase.LoadAssetAtPath<CharacterSkin>(CharacterAssetBuilder.GhoulSkinPath));
            SetField(skinView, "visualRoot", visualRoot);
            SetArray(skinView, "fallbackRenderers", fallback);

            // 체력이 있는 아바타는 피격 시 Hit 트리거를 발동한다.
            if (root.GetComponent<PlayerHealth>() != null)
                root.AddComponent<HitReactionAnimator>();
        }

        /// <summary>1인칭 총이 시야를 가리지 않도록 카메라 피벗 오른쪽에 두고, 다른 사람에게는 3인칭 총이 보이게 한다.</summary>
        private static GuardWeaponView AddWeaponView(GameObject root, Transform cameraPivot)
        {
            GameObject revolver = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetBuilder.WeaponViewPath);
            GuardWeaponView weaponView = root.AddComponent<GuardWeaponView>();
            SetField(weaponView, "aimPivot", cameraPivot);
            SetField(weaponView, "thirdPersonGunPrefab", revolver);

            if (revolver != null)
            {
                var firstPerson = (GameObject)PrefabUtility.InstantiatePrefab(revolver);
                firstPerson.name = "FirstPersonGun";
                firstPerson.transform.SetParent(cameraPivot, false);
                firstPerson.transform.localPosition = new Vector3(0.18f, -0.2f, 0.3f);
                SetField(weaponView, "firstPersonGun", firstPerson.transform);
            }
            else
            {
                Debug.LogWarning("[TestSetup] 총 모델이 없어 총 외형 없이 가드를 만듭니다. '캐릭터·무기 에셋 생성'을 먼저 실행하세요.");
            }
            return weaponView;
        }

        /// <summary>감정 표현을 모두가 보도록 네트워크 컨트롤러와 재생 뷰를 나눠 붙인다.</summary>
        private static void AddEmotes(GameObject root)
        {
            root.AddComponent<AnimatorEmoteView>();
            root.AddComponent<EmoteController>();
        }

        /// <summary>
        /// 발소리·변신 소리 같은 소음이 다른 플레이어에게도 들려야 숨바꼭질이 되므로 소음 재생 컴포넌트를 붙이고 라이브러리를 연결한다.
        /// </summary>
        private static void AddNoiseEmitter(GameObject root, NoiseLibrary noiseLibrary)
        {
            NoiseEmitter emitter = root.AddComponent<NoiseEmitter>();
            SetField(emitter, "library", noiseLibrary);
        }

        /// <summary>
        /// 숨은 마네킹을 찾아낼 수단이 총만으로는 부족해 가드에 수류탄 도구와 연출을 붙인다.
        /// </summary>
        private static void AddGrenade(GameObject root)
        {
            // ?? 연산자는 Unity의 "없는 컴포넌트" 가짜 null을 걸러내지 못하므로 TryGetComponent를 쓴다.
            if (!root.TryGetComponent(out GrenadeView view))
                view = root.AddComponent<GrenadeView>();
            SetField(view, "grenadeMaterial", GrenadeAssets.GetGrenadeMaterial());
            SetField(view, "blastMaterial", GrenadeAssets.GetBlastMaterial());

            if (!root.TryGetComponent(out GrenadeTool tool))
                tool = root.AddComponent<GrenadeTool>();
            SetField(tool, "view", view);
        }

        /// <summary>
        /// 수류탄에 맞은 사실이 모든 PC에 같게 보이도록 상태 동기화를 붙이고, 가드에게 위치가 드러나도록 외곽선 표시를 붙인다.
        /// </summary>
        private static void AddGrenadeTargetComponents(GameObject root)
        {
            if (root.GetComponent<StatusEffects>() == null)
                root.AddComponent<StatusEffects>();
            if (root.GetComponent<RevealOutlineView>() == null)
                root.AddComponent<RevealOutlineView>();
        }

        /// <summary>모든 아바타에 공통인 컴포넌트를 가진 루트를 만들고, 입력·드라이버는 소유자에게만 켜지도록 꺼 둔다.</summary>
        /// <param name="kind">아바타 종류.</param>
        /// <param name="character">역할별 참조를 연결할 때 쓰는 붙인 캐릭터 컴포넌트.</param>
        /// <param name="ownerOnly">로컬 소유자에게만 켤 컴포넌트(꺼진 채로 만듦).</param>
        private static GameObject CreateAvatarRoot(string name, AvatarKind kind, InputActionAsset inputActions, out Component character,
            out Behaviour[] ownerOnly)
        {
            var root = new GameObject(name);
            IAvatarMovementRig rig = AvatarMovementRigs.Current;
            character = rig.AddCharacter(root, kind);

            root.AddComponent<NetworkObject>();
            // 아바타 루트 크기는 바뀌지 않아 크기까지 동기화하면 변경 검사와 전송만 늘어난다.
            root.AddComponent<NetworkTransform>().SetSynchronizeScale(false);
            root.AddComponent<PlayerIdentity>();
            root.AddComponent<PlayerHealth>();

            PlayerInputReader inputReader = root.AddComponent<PlayerInputReader>();
            SetField(inputReader, "inputActions", inputActions);

            Behaviour driver = rig.AddMovementDriver(root);

            // 달리기 속도 배율은 MoveSpeedController가 적용한다.
            SprintAbility sprint = root.AddComponent<SprintAbility>();

            ownerOnly = new Behaviour[] { inputReader, driver, sprint };
            foreach (Behaviour behaviour in ownerOnly)
                behaviour.enabled = false;

            if (root.GetComponents<ICharacterMotor>().Length != 1)
                Debug.LogError($"[TestSetup] {name} 프리팹에 캐릭터 이동 구현(ICharacterMotor)이 하나가 아닙니다.");
            return root;
        }

        /// <summary>내 아바타에서만 카메라·입력이 켜지도록 소유권 설정 컴포넌트에 참조를 연결한다.</summary>
        private static void ConfigureOwnership(GameObject root, Camera playerCamera, Behaviour[] ownerOnly, Renderer[] hiddenForOwner,
            bool hideSkinForOwner = false)
        {
            PlayerOwnershipSetup ownership = root.AddComponent<PlayerOwnershipSetup>();
            SetField(ownership, "playerCamera", playerCamera);
            SetArray(ownership, "ownerOnlyBehaviours", ownerOnly);
            SetArray(ownership, "hiddenForOwner", hiddenForOwner);

            // 1인칭 역할(가드)은 외형 모델도 본인 화면에서 그림자만 남긴다.
            var serialized = new SerializedObject(ownership);
            serialized.FindProperty("hideSkinForOwner").boolValue = hideSkinForOwner;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>이펙트가 없어도 사격이 보이도록 선과 탄착 표시를 대체 연출로 둔다.</summary>
        private static ShotTracerView CreateTracer(Transform parent, Transform cameraPivot, Material material, GuardWeaponView weaponView)
        {
            var tracerObject = new GameObject("ShotTracer");
            tracerObject.transform.SetParent(parent, false);

            LineRenderer line = tracerObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.startWidth = 0.05f;
            line.endWidth = 0.02f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false;

            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(cameraPivot, false);
            muzzle.localPosition = new Vector3(0.25f, -0.2f, 0.4f);

            Renderer impact = CreateVisual(parent, PrimitiveType.Sphere, "ImpactMarker", Vector3.zero, Vector3.one * 0.12f, material);
            impact.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            impact.gameObject.SetActive(false);

            ShotTracerView view = tracerObject.AddComponent<ShotTracerView>();
            SetField(view, "muzzle", muzzle);
            SetField(view, "impactMarker", impact.transform);
            SetField(view, "weaponView", weaponView);
            SetField(view, "bulletPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetBuilder.BulletPrefabPath));
            SetField(view, "hitPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetBuilder.HitPrefabPath));
            SetField(view, "muzzleFlashPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetBuilder.MuzzleFlashPrefabPath));
            return view;
        }

        /// <summary>전원 장치는 판마다 위치가 바뀌어 서버가 스폰할 네트워크 프리팹으로 만든다.</summary>
        private static NetworkObject CreateGeneratorPrefab(Dictionary<string, Material> materials, NoiseLibrary noiseLibrary)
        {
            var root = new GameObject("Generator");

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.8f, 0f);
            box.size = new Vector3(1f, 1.6f, 0.8f);

            root.AddComponent<NetworkObject>();
            root.AddComponent<Generator>();
            AddNoiseEmitter(root, noiseLibrary);

            // 가드 스폰 후보가 전원 장치 안에 생기지 않도록 NavMesh에 구멍을 낸다.
            NavMeshObstacle obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = box.center;
            obstacle.size = box.size;
            obstacle.carving = true;

            Renderer body = CreateVisual(root.transform, PrimitiveType.Cube, "Body", new Vector3(0f, 0.8f, 0f), new Vector3(1f, 1.6f, 0.8f), materials["Generator"]);

            // 진행도 바: 배경 + 왼쪽 끝이 피벗인 채움 막대
            Transform barRoot = new GameObject("ProgressBar").transform;
            barRoot.SetParent(root.transform, false);
            barRoot.localPosition = new Vector3(0f, 2.1f, 0f);
            Renderer background = CreateVisual(barRoot, PrimitiveType.Cube, "Background", Vector3.zero, new Vector3(1.1f, 0.16f, 0.02f), materials["ProgressBackground"]);
            background.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Transform fillPivot = new GameObject("FillPivot").transform;
            fillPivot.SetParent(barRoot, false);
            fillPivot.localPosition = new Vector3(-0.5f, 0f, -0.02f);
            Renderer fill = CreateVisual(fillPivot, PrimitiveType.Cube, "Fill", new Vector3(0.5f, 0f, 0f), new Vector3(1f, 0.1f, 0.02f), materials["ProgressFill"]);
            fill.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GeneratorView view = root.AddComponent<GeneratorView>();
            SetField(view, "bodyRenderer", body);
            SetField(view, "destroyedMaterial", materials["GeneratorDestroyed"]);
            SetField(view, "barRoot", barRoot);
            SetField(view, "barFill", fillPivot);

            return SavePrefab(root, GeneratorPrefabPath);
        }

        /// <summary>
        /// 아바타 외형이 캐릭터 캡슐과 따로 충돌하면 이동이 걸리므로 표시용 도형에서는 충돌체를 지운다.
        /// </summary>
        internal static Renderer CreateVisual(Transform parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = localScale;

            Renderer renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        /// <summary>
        /// 모든 아바타의 카메라가 켜지면 화면이 다른 사람 시점으로 바뀌므로 꺼 둔 채 만들고 소유자에게만 켠다.
        /// </summary>
        private static Camera CreatePlayerCamera(Transform parent, Vector3 localPosition)
        {
            var cameraObject = new GameObject("PlayerCamera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = localPosition;

            Camera playerCamera = cameraObject.AddComponent<Camera>();
            playerCamera.nearClipPlane = 0.05f;
            playerCamera.fieldOfView = 70f;
            playerCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true; // 저체력 시야 효과
            cameraObject.AddComponent<AudioListener>();

            cameraObject.SetActive(false);
            return playerCamera;
        }

        /// <summary>
        /// 프리팹은 씬에서 조립해야 저장할 수 있어 임시로 만든 뒤, 열린 씬이 더러워지지 않도록 저장 후 지운다.
        /// </summary>
        private static NetworkObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<NetworkObject>();
        }

        #endregion

        #region 씬

        /// <summary>모드 SDK 시험용 원본으로 쓰도록 게임에는 넣지 않는 테스트 씬을 만든다.</summary>
        private static void CreateTestScene()
        {
            UndergroundLabBuilder.BuildScene(ScenePath, capturePreview: false);
        }

        /// <summary>씬 ID가 0이면 서버가 씬 오브젝트를 스폰하지 않으므로 리플렉션으로 FishNet 씬 ID를 생성한다.</summary>
        internal static void GenerateNetworkSceneIds(Scene scene)
        {
            MethodInfo method = typeof(NetworkObject).GetMethod("CreateSceneId", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(Scene), typeof(bool), typeof(int).MakeByRefType() }, null);

            if (method == null)
            {
                Debug.LogError("[TestSetup] FishNet 씬 ID 생성 메서드를 찾지 못했습니다. " +
                               "Tools > Fish-Networking > Utility > Reserialize NetworkObjects에서 씬을 재직렬화하세요.");
                return;
            }

            object[] args = { scene, true, 0 };
            method.Invoke(null, args);
            Debug.Log($"[TestSetup] 네트워크 씬 ID {(int)args[2]}개 생성: {scene.path}");
        }

        /// <summary>아바타가 없으면 화면이 검게 나오므로 대기·관전에 쓸 씬 카메라를 만든다.</summary>
        internal static SpectatorController CreateSceneCamera()
        {
            var cameraObject = new GameObject("SceneCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 30f, -28f), Quaternion.Euler(50f, 0f, 0f));

            Camera sceneCamera = cameraObject.AddComponent<Camera>();
            sceneCamera.nearClipPlane = 0.05f;
            sceneCamera.fieldOfView = 70f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<SceneCameraSwitcher>();

            SpectatorController spectator = cameraObject.AddComponent<SpectatorController>();
            SetField(spectator, "inputActions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath));
            return spectator;
        }

        /// <summary>
        /// GameSystems에서 UI가 참조할 컴포넌트 묶음.
        /// </summary>
        internal struct GameSystemsRefs
        {
            public GameStateMachine stateMachine;
            public PlayerRegistry playerRegistry;
            public ObjectiveTracker objectiveTracker;
            public MatchResult matchResult;
        }

        /// <summary>판 진행 상태가 모든 PC에 같도록 서버 시스템들을 씬 네트워크 오브젝트 하나에 모은다.</summary>
        internal static GameSystemsRefs CreateGameSystems(ScenePrefabs prefabs)
        {
            var systems = new GameObject("GameSystems");
            systems.AddComponent<NetworkObject>();

            GameStateMachine stateMachine = systems.AddComponent<GameStateMachine>();
            SpawnPointRegistry spawnPoints = systems.AddComponent<SpawnPointRegistry>();
            PlayerRegistry playerRegistry = systems.AddComponent<PlayerRegistry>();
            PlayerSpawner playerSpawner = systems.AddComponent<PlayerSpawner>();
            ObjectiveTracker objectiveTracker = systems.AddComponent<ObjectiveTracker>();
            GeneratorSpawner generatorSpawner = systems.AddComponent<GeneratorSpawner>();
            MatchResult matchResult = systems.AddComponent<MatchResult>();
            MatchRules matchRules = systems.AddComponent<MatchRules>();
            MatchSessionBridge sessionBridge = systems.AddComponent<MatchSessionBridge>();
            // 총에 맞은 변신 사물 물리 동기화.
            systems.AddComponent<PropPhysicsSync>();
            SetField(sessionBridge, "stateMachine", stateMachine);

            SetField(stateMachine, "matchRulesComponent", matchRules);

            SetField(playerSpawner, "stateMachine", stateMachine);
            SetField(playerSpawner, "spawnPoints", spawnPoints);
            SetRolePrefabs(playerSpawner, (PlayerRole.Guard, prefabs.guard), (PlayerRole.Mannequin, prefabs.mannequin));
            SetField(playerSpawner, "matchResult", matchResult);

            SetField(generatorSpawner, "stateMachine", stateMachine);
            SetField(generatorSpawner, "objectiveTracker", objectiveTracker);
            SetField(generatorSpawner, "generatorPrefab", prefabs.generator);

            SetField(matchResult, "stateMachine", stateMachine);

            SetField(matchRules, "playerRegistry", playerRegistry);
            SetField(matchRules, "objectiveTracker", objectiveTracker);
            SetField(matchRules, "matchResult", matchResult);

            return new GameSystemsRefs
            {
                stateMachine = stateMachine,
                playerRegistry = playerRegistry,
                objectiveTracker = objectiveTracker,
                matchResult = matchResult
            };
        }

        /// <summary>씬 네트워크 오브젝트는 스폰 전까지 꺼져 있으므로 HUD와 저체력 효과를 별도 오브젝트에 둔다.</summary>
        internal static void CreateUi(GameSystemsRefs systems, SpectatorController spectator)
        {
            // HUD 프리팹이 없으면 기본 모양으로 만들어 저장한다.
            EditorUiFactory.CreateEventSystem();
            GameObject prefab = UiPrefabLibrary.GetOrBuildRoot(UiPrefabLibrary.HudCanvasPath, BuildHudCanvas);
            GameHud gameHud = UiPrefabLibrary.Instantiate(prefab).GetComponent<GameHud>();

            // 씬 오브젝트 참조는 프리팹에 담을 수 없으므로 씬의 인스턴스에 연결한다.
            SetField(gameHud, "stateMachine", systems.stateMachine);
            SetField(gameHud, "playerRegistry", systems.playerRegistry);
            SetField(gameHud, "objectiveTracker", systems.objectiveTracker);
            SetField(gameHud, "matchResult", systems.matchResult);
            SetField(gameHud, "spectator", spectator);

            // 저체력 시야 효과용 전역 볼륨 (런타임에 프로필 생성)
            new GameObject("LowHealthVolume").AddComponent<LowHealthVignette>();
        }

        /// <summary>HUD 프리팹이 없을 때 쓸 기본 모양을 만들고, 메뉴를 닫으면 바로 조작하도록 커서를 다시 잠근다.</summary>
        private static GameObject BuildHudCanvas()
        {
            Canvas canvas = EditorUiFactory.CreateCanvas("HudCanvas");
            canvas.gameObject.AddComponent<OsFontApplier>();
            SharedUiBuilder.CreateGameHud(canvas.transform);
            SharedUiBuilder.CreatePauseMenu(canvas.transform, lockCursorOnClose: true);
            return canvas.gameObject;
        }

        /// <summary>
        /// 바닥·벽처럼 걸어 다니는 블록은 충돌체가 필요하고, 오클루전·NavMesh 굽기에 쓰이도록 정적으로 둔다.
        /// </summary>
        internal static GameObject CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            block.isStatic = true;
            return block;
        }

        /// <summary>맵 도구들이 같은 스폰 지점을 만들도록 생성을 한곳에 둔다.</summary>
        internal static void CreateSpawnPoint(Transform parent, PlayerRole role, Vector3 position, Quaternion rotation)
        {
            var pointObject = new GameObject($"SpawnPoint_{role}");
            pointObject.transform.SetParent(parent, false);
            pointObject.transform.SetPositionAndRotation(position, rotation);

            SpawnPoint point = pointObject.AddComponent<SpawnPoint>();
            var serialized = new SerializedObject(point);
            serialized.FindProperty("role").enumValueIndex = (int)role;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>사냥 시간 안개가 빌드에서도 동작하도록 선형 안개 셰이더 변형을 빌드에 남긴다.</summary>
        private static void KeepLinearFogShaderVariants()
        {
            Object graphicsSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").FirstOrDefault();
            if (graphicsSettings == null)
            {
                Debug.LogWarning("[TestSetup] GraphicsSettings를 찾지 못했습니다. Project Settings > Graphics > Fog Modes를 Custom(Linear)으로 설정하세요.");
                return;
            }

            var serialized = new SerializedObject(graphicsSettings);
            serialized.FindProperty("m_FogStripping").intValue = 1; // 1 = Custom
            serialized.FindProperty("m_FogKeepLinear").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 빌드 인덱스 0이 시작 씬이 되므로 로비 → 룸 → 게임 순서가 지켜지도록 받은 순서 그대로 빌드 설정을 덮어쓴다.
        /// </summary>
        internal static void SetBuildScenes(params string[] orderedPaths)
        {
            // 지정한 씬을 순서대로 앞에 두고(첫 번째가 시작 씬), 나머지 기존 씬은 뒤에 유지한다.
            List<EditorBuildSettingsScene> others = EditorBuildSettings.scenes.Where(s => !orderedPaths.Contains(s.path)).ToList();
            List<EditorBuildSettingsScene> scenes = orderedPaths.Select(path => new EditorBuildSettingsScene(path, true)).ToList();
            scenes.AddRange(others);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        #endregion

        #region SerializedObject 헬퍼

        /// <summary>
        /// 인스펙터 연결을 코드로 하려면 private 직렬화 필드를 바꿔야 하는데 공개 setter를 만들면 캡슐화가 깨지므로 SerializedObject로 지정한다.
        /// </summary>
        internal static void SetField(Object target, string fieldName, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"[TestSetup] {target.GetType().Name}.{fieldName} 필드를 찾을 수 없습니다.");
                return;
            }
            // C# 참조는 있지만 Unity 오브젝트가 이미 내려간(파괴된) 경우 참조가 조용히 비는 것을 막는다.
            if (value == null && !ReferenceEquals(value, null))
                Debug.LogError($"[TestSetup] {target.GetType().Name}.{fieldName}에 연결할 오브젝트가 이미 해제되었습니다. 경로로 다시 불러와야 합니다.");

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 공개 setter 없이 private 직렬화 배열을 코드로 채우려고 SerializedObject로 크기를 맞춘 뒤 지정한다.
        /// </summary>
        internal static void SetArray(Object target, string fieldName, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 역할을 추가해도 스포너 코드를 고치지 않도록 역할 → 프리팹 대응을 직렬화 목록(rolePrefabs)으로 채운다.
        /// </summary>
        internal static void SetRolePrefabs(PlayerSpawner spawner, params (PlayerRole role, NetworkObject prefab)[] entries)
        {
            var serialized = new SerializedObject(spawner);
            SerializedProperty list = serialized.FindProperty("rolePrefabs");
            list.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("role").intValue = (int)entries[i].role;
                element.FindPropertyRelative("prefab").objectReferenceValue = entries[i].prefab;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        #endregion
    }
}
