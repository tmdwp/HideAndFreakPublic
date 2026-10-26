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
    /// <summary>
    /// 테스트 환경(머티리얼, 설정/사물 데이터, 프리팹, 테스트 씬)을 메뉴 한 번으로 생성한다.
    /// 다시 실행하면 프리팹과 씬을 덮어써서 항상 같은 결과를 만든다.
    /// (게임 설정·문장·크레딧은 Resources 폴더의 텍스트 파일이라 이 도구가 건드리지 않는다)
    /// </summary>
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

        /// <summary>게임 이름. (창 제목, 빌드 정보)</summary>
        internal const string ProductName = "Hide&Freak";

        /// <summary>
        /// 전체 테스트 환경을 생성한다.
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

            // 캐릭터 외형(마네킹 3종, 가드)과 리볼버 모델을 먼저 만든다. (프리팹이 참조)
            CharacterAssetBuilder.BuildAll();

            Dictionary<string, Material> materials = CreateMaterials();
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (inputActions == null)
            {
                Debug.LogError($"[TestSetup] 입력 에셋을 찾을 수 없습니다: {InputActionsPath}");
                return;
            }

            NoiseLibrary noiseLibrary = CreateNoiseLibrary();

            // 아바타 이동 구현(ECM2)은 별도 어셈블리가 등록한다. 없으면(이동 라이브러리 미설치) 아바타 프리팹을 만들 수 없다.
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

            // 테스트 맵 → 게임 시스템 씬 → 룸 씬 → 로비 씬(시작 씬) 순서로 만든다. 마지막에 로비 씬이 열린 상태로 끝난다.
            CreateTestScene();
            GameSystemsSceneBuilder.Build();
            MenuSceneBuilder.BuildRoomScene(RoomScenePath, AssetDatabase.LoadAssetAtPath<NetworkObject>(RoomStatePrefabPath));
            MenuSceneBuilder.BuildLobbyScene(LobbyScenePath, RoomScenePath,
                AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(DefaultPrefabObjectsPath));

            RegisterBuildScenes();
            // 게임 이름(창 제목). 저장 위치(PlayerPrefs, 로그)도 이 이름을 따른다.
            PlayerSettings.productName = ProductName;
            KeepLinearFogShaderVariants();
            SteamBuildProcessor.EnsureRunInBackground();

            AssetDatabase.SaveAssets();
            Debug.Log("[TestSetup] 테스트 환경 생성 완료. 로비 씬에서 Play 하세요: " + LobbyScenePath);
        }

        /// <summary>
        /// 룸 상태 프리팹을 만든다. 전역(Global) 네트워크 오브젝트라 씬이 바뀌어도 유지된다.
        /// (기본 게임 설정은 Resources/Data/default_game_settings.json에서 읽는다)
        /// </summary>
        private static NetworkObject CreateRoomStatePrefab()
        {
            var root = new GameObject("RoomState");
            NetworkObject networkObject = root.AddComponent<NetworkObject>();
            var serialized = new SerializedObject(networkObject);
            serialized.FindProperty("_isGlobal").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // 맵 목록은 실행 중에 등록소(Resources/Data/maps.json + 켜진 모드)에서 읽는다.
            root.AddComponent<RoomState>();
            return SavePrefab(root, RoomStatePrefabPath);
        }

        /// <summary>
        /// 저장된 네트워크 프리팹(가드, 마네킹, 전원 장치)을 불러온다. 하나라도 없으면 false.
        /// (맵 생성 도구가 "테스트 환경 생성"으로 만든 프리팹을 재사용할 때 사용)
        /// </summary>
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

        /// <summary>
        /// 빌드 설정에 로비 → 룸 → 게임 시스템 → 맵 목록의 맵 순서로 씬을 등록한다. (없는 맵 씬은 건너뛴다)
        /// </summary>
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
        /// 필요한 폴더를 만든다.
        /// </summary>
        private static void EnsureFolders()
        {
            foreach (string folder in new[] { MaterialFolder, PrefabFolder, SoFolder, SceneFolder })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 테스트용 단색 URP 머티리얼을 만든다.
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

            // 외곽선은 머티리얼 대신 PC_Renderer의 OutlineRendererFeature가 그린다. (Phase 7 메뉴로 설치)
            return result;
        }

        /// <summary>
        /// 셰이더와 색 목록으로 머티리얼을 만들거나 갱신한다.
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
        /// 소음 라이브러리를 만든다. 이미 있으면 사용자가 지정한 클립과 값은 유지하고 새 소음 종류만 추가한다.
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
        /// 스폰할 프리팹을 FishNet의 DefaultPrefabObjects에 등록한다.
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

        /// <summary>
        /// FishNet 내부의 DefaultPrefabObjects.SetAssetPathHashes(int)를 호출해 모든 프리팹의 해시를 다시 계산한다.
        /// 공개 API가 없어 리플렉션을 사용하며, 실패하면 FishNet 메뉴를 안내한다.
        /// </summary>
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

        /// <summary>
        /// 1인칭 가드 프리팹을 만든다.
        /// </summary>
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

            // 외형: SWAT + 더미 애니메이션 (외형이 없으면 기본 도형)
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

        /// <summary>
        /// 카메라 피벗 아래에 손전등용 스포트라이트를 만든다. (기본 꺼짐)
        /// </summary>
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

        /// <summary>
        /// 3인칭 마네킹 프리팹을 만든다.
        /// </summary>
        private static NetworkObject CreateMannequinPrefab(InputActionAsset inputActions, Dictionary<string, Material> materials,
            NoiseLibrary noiseLibrary)
        {
            GameObject root = CreateAvatarRoot("Mannequin", AvatarKind.Mannequin, inputActions, out Component character, out Behaviour[] ownerOnly);

            // 외형은 맵이 바꿀 수 있다. (MapInfo)
            Transform visual = CreateMannequinVisual(root.transform, materials, out Renderer body, out Renderer face);

            Transform followTarget = new GameObject("FollowTarget").transform;
            followTarget.SetParent(root.transform, false);
            followTarget.localPosition = new Vector3(0f, 1.5f, 0f);
            Camera playerCamera = CreatePlayerCamera(root.transform, new Vector3(0f, 2.5f, -3.5f));

            SetField(character, "followTarget", followTarget);
            ConfigureOwnership(root, playerCamera, ownerOnly, new Renderer[0]);
            AddMoveSpeedController(root, PlayerRole.Mannequin);

            // 변신: 조준 → 변신 상태 → 외형 적용(충돌체 크기는 ICharacterMotor로 바꿈), 상호작용
            AvatarMovementRigs.Current.AddAimProvider(root);
            root.AddComponent<MannequinTransformer>();

            PropDisguiseView disguiseView = root.AddComponent<PropDisguiseView>();
            SetArray(disguiseView, "bodyRenderers", new Object[] { body, face });

            // 변신 중 총에 맞으면 진짜 사물처럼 날아감 (서버 대리 물체, 멈추면 넘어진 모습 유지)
            root.AddComponent<DisguiseKnockback>();

            root.AddComponent<MannequinInteractor>();
            root.AddComponent<InteractAnimationView>();

            // 조준한 변신 가능 사물에 외곽선 표시 (본인 화면만, 그리기는 PC_Renderer의 OutlineRendererFeature)
            root.AddComponent<PropHighlighter>();

            // 수류탄 효과: 외곽선 표시·감속 동기화, 가드 화면의 벽 너머 외곽선
            AddGrenadeTargetComponents(root);

            // Phase 2: 소음(탐지/변신 중 주기 소음), 벽타기, 감정 표현
            AddNoiseEmitter(root, noiseLibrary);
            root.AddComponent<DisguiseNoiseEmitter>();
            AvatarMovementRigs.Current.AddMannequinAbilities(root);
            AddEmotes(root, visual);

            // Phase 3: 사냥 시간 모드(1인칭, 체력 배율, 속도), 근접 공격
            MannequinHuntingMode huntingMode = root.AddComponent<MannequinHuntingMode>();
            SetArray(huntingMode, "bodyRenderers", new Object[] { body, face });

            // 사냥 시간 시야: 눈 높이의 개인 조명(본인 화면에만 켜짐) + 로컬 안개
            HuntVisionEffect huntVision = root.AddComponent<HuntVisionEffect>();
            SetField(huntVision, "visionLight", CreateHuntVisionLight(followTarget));
            root.AddComponent<MannequinAttackTool>();

            // Phase 4: 시선 동기화와 관전 시점 (상하 시선을 적용할 피벗은 없음)
            AddViewSync(root, null, followTarget, isThirdPerson: true);

            return SavePrefab(root, MannequinPrefabPath);
        }

        /// <summary>
        /// 게임 설정의 이동 속도를 적용하는 컴포넌트를 역할과 함께 추가한다.
        /// </summary>
        private static void AddMoveSpeedController(GameObject root, PlayerRole role)
        {
            MoveSpeedController speedController = root.AddComponent<MoveSpeedController>();
            var serialized = new SerializedObject(speedController);
            serialized.FindProperty("role").enumValueIndex = (int)role;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // 소유자 권한 이동을 서버에서 검증한다. (스피드핵, 순간이동, 시작 준비 중 이동 방지)
            root.AddComponent<MovementValidator>();
        }

        /// <summary>
        /// 시선 동기화(PlayerViewSync)와 관전 시점(SpectatorViewPoint)을 추가한다.
        /// </summary>
        /// <param name="root">아바타 루트.</param>
        /// <param name="pitchPivot">원격 피어에서 상하 시선을 적용할 피벗. (가드의 카메라 피벗, 없으면 null)</param>
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

        /// <summary>
        /// 사냥 시간 마네킹의 개인 조명을 만든다. 그림자 없이 주변만 은은하게 비춘다. (기본 꺼짐)
        /// </summary>
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

        /// <summary>
        /// 마네킹 외형(본체 + 얼굴 표시)을 감정 표현용 컨테이너 아래에 만든다.
        /// </summary>
        private static Transform CreateMannequinVisual(Transform parent, Dictionary<string, Material> materials, out Renderer body, out Renderer face)
        {
            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(parent, false);

            body = CreateVisual(visual, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(0.8f, 0.9f, 0.8f), materials["Mannequin"]);
            face = CreateVisual(visual, PrimitiveType.Cube, "FaceMarker", new Vector3(0f, 1.5f, 0.3f), new Vector3(0.3f, 0.1f, 0.2f), materials["Visor"]);

            // 기본 외형은 더미. 맵(MapInfo)이 마네킹 외형을 지정하면 실행 시 그것으로 바뀐다.
            AddSkinView(parent.gameObject, PlayerRole.Mannequin, CharacterAssetBuilder.DummySkinPath, visual, new[] { body, face });
            return visual;
        }

        /// <summary>
        /// 캐릭터 외형 컴포넌트를 추가한다. 외형 에셋이 없으면 기본 도형이 그대로 보인다.
        /// </summary>
        private static void AddSkinView(GameObject root, PlayerRole role, string defaultSkinPath, Transform visualRoot, Renderer[] fallback)
        {
            CharacterSkinView skinView = root.AddComponent<CharacterSkinView>();
            var serialized = new SerializedObject(skinView);
            serialized.FindProperty("role").enumValueIndex = (int)role;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SetField(skinView, "defaultSkin", AssetDatabase.LoadAssetAtPath<CharacterSkin>(defaultSkinPath));

            // 마네킹은 사냥 시간에 Ghoul로 바뀐다. (맵이 사냥 외형을 지정하면 그것을 우선)
            if (role == PlayerRole.Mannequin)
                SetField(skinView, "huntSkin", AssetDatabase.LoadAssetAtPath<CharacterSkin>(CharacterAssetBuilder.GhoulSkinPath));
            SetField(skinView, "visualRoot", visualRoot);
            SetArray(skinView, "fallbackRenderers", fallback);

            // 체력이 있는 아바타는 피격 시 Hit 트리거를 발동한다.
            if (root.GetComponent<PlayerHealth>() != null)
                root.AddComponent<HitReactionAnimator>();
        }

        /// <summary>
        /// 가드 총 외형을 추가한다. 1인칭 총은 카메라 피벗 아래 오른쪽에 둔다.
        /// </summary>
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

        /// <summary>
        /// 감정 표현 뷰와 네트워크 컨트롤러를 추가한다.
        /// </summary>
        private static void AddEmotes(GameObject root, Transform visual)
        {
            // 순서가 우선순위: 외형 애니메이터에 감정 표현이 있으면 그것을, 없으면 절차적 움직임을 쓴다.
            root.AddComponent<AnimatorEmoteView>();
            ProceduralEmoteView emoteView = root.AddComponent<ProceduralEmoteView>();
            SetField(emoteView, "body", visual);
            root.AddComponent<EmoteController>();
        }

        /// <summary>
        /// 소음 재생 컴포넌트를 추가하고 라이브러리를 연결한다.
        /// </summary>
        private static void AddNoiseEmitter(GameObject root, NoiseLibrary noiseLibrary)
        {
            NoiseEmitter emitter = root.AddComponent<NoiseEmitter>();
            SetField(emitter, "library", noiseLibrary);
        }

        /// <summary>
        /// 가드에 수류탄 도구와 연출을 붙인다.
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
        /// 마네킹에 수류탄 효과(상태 동기화, 상대 화면 외곽선)를 붙인다.
        /// </summary>
        private static void AddGrenadeTargetComponents(GameObject root)
        {
            if (root.GetComponent<StatusEffects>() == null)
                root.AddComponent<StatusEffects>();
            if (root.GetComponent<RevealOutlineView>() == null)
                root.AddComponent<RevealOutlineView>();
        }

        /// <summary>
        /// 모든 아바타에 공통인 네트워크/이동/입력/체력 컴포넌트를 가진 루트를 만든다.
        /// 입력과 드라이버는 소유자에게만 켜지도록 비활성 상태로 둔다.
        /// 캐릭터·이동 드라이버는 이동 구현(<see cref="AvatarMovementRigs"/>)이 붙인다. 드라이버가 캐릭터를 요구하므로 캐릭터를 먼저 붙인다.
        /// </summary>
        /// <param name="kind">아바타 종류. (이동 구현이 역할별 캐릭터를 고름)</param>
        /// <param name="character">붙인 캐릭터 컴포넌트. (카메라 피벗 등 역할별 참조를 연결할 때 사용)</param>
        /// <param name="ownerOnly">로컬 소유자에게만 켤 컴포넌트. (입력, 이동 드라이버, 달리기 — 모두 꺼진 채로 만든다)</param>
        private static GameObject CreateAvatarRoot(string name, AvatarKind kind, InputActionAsset inputActions, out Component character,
            out Behaviour[] ownerOnly)
        {
            var root = new GameObject(name);
            IAvatarMovementRig rig = AvatarMovementRigs.Current;
            character = rig.AddCharacter(root, kind);

            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            root.AddComponent<PlayerIdentity>();
            root.AddComponent<PlayerHealth>();

            PlayerInputReader inputReader = root.AddComponent<PlayerInputReader>();
            SetField(inputReader, "inputActions", inputActions);

            Behaviour driver = rig.AddMovementDriver(root);

            // 달리기(Shift): 스태미나를 쓰며 빨리 달린다. 속도 배율은 MoveSpeedController가 적용한다.
            SprintAbility sprint = root.AddComponent<SprintAbility>();

            ownerOnly = new Behaviour[] { inputReader, driver, sprint };
            foreach (Behaviour behaviour in ownerOnly)
                behaviour.enabled = false;

            if (root.GetComponents<ICharacterMotor>().Length != 1)
                Debug.LogError($"[TestSetup] {name} 프리팹에 캐릭터 이동 구현(ICharacterMotor)이 하나가 아닙니다.");
            return root;
        }

        /// <summary>
        /// 소유권 설정 컴포넌트를 추가하고 참조를 연결한다. (캐릭터 이동 구현은 실행 중 ICharacterMotor로 찾는다)
        /// </summary>
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

        /// <summary>
        /// 사격 연출(총알·명중·총구 화염 이펙트)을 만든다. 이펙트가 없으면 선(LineRenderer)과 탄착 표시로 대신한다.
        /// 연출은 총 외형의 총구에서 시작하고, 총 외형이 없으면 카메라 피벗 오른쪽 아래의 예비 총구를 쓴다.
        /// </summary>
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

        /// <summary>
        /// 전원 장치 프리팹을 만든다. (충돌체, 본체, 마네킹 전용 진행도 바)
        /// </summary>
        private static NetworkObject CreateGeneratorPrefab(Dictionary<string, Material> materials, NoiseLibrary noiseLibrary)
        {
            var root = new GameObject("Generator");

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.8f, 0f);
            box.size = new Vector3(1f, 1.6f, 0.8f);

            root.AddComponent<NetworkObject>();
            root.AddComponent<Generator>();
            AddNoiseEmitter(root, noiseLibrary);

            // 가드 스폰 후보(NavMesh 무작위 지점)가 전원 장치 안에 생기지 않도록 NavMesh에 구멍을 낸다.
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
        /// 충돌체가 없는 표시용 도형을 만든다.
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
        /// 소유자에게만 켜질 플레이어 카메라를 만든다. (기본 비활성)
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
        /// 씬의 임시 오브젝트를 프리팹으로 저장하고 삭제한다.
        /// </summary>
        private static NetworkObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<NetworkObject>();
        }

        #endregion

        #region 씬

        /// <summary>
        /// 테스트 씬(지하 연구소 맵)을 새로 만들어 저장한다. 게임(빌드 설정·맵 목록)에는 넣지 않으며, 모드 SDK 시험용 원본으로 쓴다.
        /// 맵 구성은 <see cref="UndergroundLabBuilder"/>가 맡는다. (메뉴 "맵 생성/지하 연구소"로 맵만 다시 만들 수 있다)
        /// </summary>
        private static void CreateTestScene()
        {
            UndergroundLabBuilder.BuildScene(ScenePath, capturePreview: false);
        }

        /// <summary>
        /// 씬의 NetworkObject에 FishNet 씬 ID를 강제로 생성한다. (씬 ID가 0이면 서버가 씬 오브젝트를 스폰하지 않는다)
        /// FishNet 내부의 NetworkObject.CreateSceneId(Scene, bool, out int)를 호출한다.
        /// 공개 API가 없어 리플렉션을 사용하며, 실패하면 FishNet 메뉴를 안내한다.
        /// </summary>
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

        /// <summary>
        /// 아바타가 없을 때 쓰는 씬 카메라를 만든다.
        /// 대기 중에는 맵을 내려다보고, 사망/미참가 상태에서는 생존자의 시야로 관전한다.
        /// </summary>
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

        /// <summary>
        /// 게임 진행에 필요한 서버 시스템을 가진 씬 네트워크 오브젝트를 만든다.
        /// </summary>
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
            // 총에 맞은 변신 사물 물리 진행·동기화 (참조 없음, GameServices로 등록)
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

        /// <summary>
        /// 게임 HUD와 저체력 효과 볼륨을 만든다. (접속은 로비/룸 씬에서 처리하므로 게임 씬에는 NetworkManager가 없다)
        /// 씬 네트워크 오브젝트는 클라이언트에서 스폰 전까지 비활성이므로 별도 오브젝트에 둔다.
        /// </summary>
        internal static void CreateUi(GameSystemsRefs systems, SpectatorController spectator)
        {
            // uGUI HUD와 ESC 메뉴: 프리팹(Prefabs/UI/HudCanvas)을 쓰고, 없으면 기본 모양으로 만들어 저장한다.
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

        /// <summary>
        /// 게임 HUD 캔버스의 기본 모양(HUD + ESC 메뉴, 닫으면 커서를 다시 잠금)을 만든다.
        /// </summary>
        private static GameObject BuildHudCanvas()
        {
            Canvas canvas = EditorUiFactory.CreateCanvas("HudCanvas");
            canvas.gameObject.AddComponent<OsFontApplier>();
            SharedUiBuilder.CreateGameHud(canvas.transform);
            SharedUiBuilder.CreatePauseMenu(canvas.transform, lockCursorOnClose: true);
            return canvas.gameObject;
        }

        /// <summary>
        /// 충돌체가 있는 정적 블록을 만든다.
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

        /// <summary>
        /// 역할이 지정된 스폰 지점을 만든다.
        /// </summary>
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

        /// <summary>
        /// 빌드에 선형 안개 셰이더 변형이 포함되도록 Graphics 설정의 안개 스트리핑을 Custom(Linear 유지)으로 바꾼다.
        /// 기본값(Automatic)은 씬에서 안개를 쓰지 않으면 빌드에서 안개 셰이더를 제거해,
        /// 런타임에 켜는 사냥 시간 시야 안개가 빌드에서만 동작하지 않는다.
        /// </summary>
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
        /// 빌드 설정의 씬 순서를 정한다. (로비 → 룸 → 게임)
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
        /// private 직렬화 필드에 오브젝트 참조를 지정한다.
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
        /// private 직렬화 배열 필드에 오브젝트 참조 목록을 지정한다.
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
        /// PlayerSpawner의 역할별 아바타 프리팹 목록(rolePrefabs)을 채운다.
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
