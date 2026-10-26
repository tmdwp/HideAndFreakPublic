using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Component.Scenes;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Managing.Observing;
using FishNet.Observing;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using GuardVsMannequin.Network;
using GuardVsMannequin.Room;
using GuardVsMannequin.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 로비 씬(메인 메뉴 + 네트워크 루트)과 룸 씬(대기방)을 만든다.
    /// </summary>
    public static class MenuSceneBuilder
    {
        private const string FishySteamworksTypeName = "FishySteamworks.FishySteamworks";
        private const string SceneConditionPath = "Assets/FishNet/Runtime/Observing/Conditions/ScriptableObjects/SceneCondition.asset";

        /// <summary>
        /// 로비 씬을 만든다. 씬이 바뀌어도 유지되는 네트워크 루트(NetworkManager와 세션 서비스)를 포함한다.
        /// </summary>
        /// <param name="scenePath">저장할 경로.</param>
        /// <param name="roomScenePath">서버 시작 시 불러올 룸 씬 경로.</param>
        /// <param name="spawnablePrefabs">스폰 가능한 프리팹 목록.</param>
        public static void BuildLobbyScene(string scenePath, string roomScenePath, DefaultPrefabObjects spawnablePrefabs)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMenuCamera();
            CreateNetworkRoot(scenePath, roomScenePath, spawnablePrefabs);
            EditorUiFactory.CreateEventSystem();
            CreateLobbyUi();

            // 배경 연출(어두운 방에서 가드가 구울에게 쫓김). 메뉴 카메라를 방 안으로 옮긴다.
            LobbyBackdropBuilder.AddToScene(scene);

            SceneLightingSetup.Apply(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        /// <summary>
        /// 룸 씬을 만든다. 서버에서 방 상태를 스폰하는 부트스트랩과 룸 UI를 포함한다.
        /// </summary>
        public static void BuildRoomScene(string scenePath, NetworkObject roomStatePrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMenuCamera();
            EditorUiFactory.CreateEventSystem();

            RoomBootstrap bootstrap = new GameObject("RoomBootstrap").AddComponent<RoomBootstrap>();
            TestEnvironmentSetupTool.SetField(bootstrap, "roomStatePrefab", roomStatePrefab);

            CreateRoomUi();

            SceneLightingSetup.Apply(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        #region 네트워크 루트

        /// <summary>
        /// NetworkManager + Multipass(Tugboat, FishySteamworks) + DefaultScene + Steam/세션 서비스를 가진 루트를 만든다.
        /// NetworkManager가 DontDestroyOnLoad로 만들기 때문에 같은 오브젝트의 서비스도 씬 전환 후 유지된다.
        /// </summary>
        private static void CreateNetworkRoot(string lobbyScenePath, string roomScenePath, DefaultPrefabObjects spawnablePrefabs)
        {
            var root = new GameObject("NetworkRoot");

            NetworkManager networkManager = root.AddComponent<NetworkManager>();
            if (spawnablePrefabs != null)
                networkManager.SpawnablePrefabs = spawnablePrefabs;

            // 씬 오브젝트는 그 씬을 불러온 클라이언트에게만 보이게 한다.
            // 없으면 호스트가 게임 씬을 먼저 불러온 순간 로딩 중인 게스트에게 스폰 정보를 보내
            // 게스트가 GameSystems를 찾지 못한다(SceneId not found → HUD/문 동작 안 함).
            // 전역 오브젝트(RoomState)는 FishNet이 조건에서 자동으로 제외한다.
            ObserverManager observerManager = root.AddComponent<ObserverManager>();
            TestEnvironmentSetupTool.SetArray(observerManager, "_defaultConditions",
                new UnityEngine.Object[] { AssetDatabase.LoadAssetAtPath<ObserverCondition>(SceneConditionPath) });

            TransportManager transportManager = root.AddComponent<TransportManager>();
            Tugboat tugboat = root.AddComponent<Tugboat>();
            Transport steamTransport = AddFishySteamworks(root);

            Multipass multipass = root.AddComponent<Multipass>();
            Transport[] transports = steamTransport != null ? new Transport[] { tugboat, steamTransport } : new Transport[] { tugboat };
            TestEnvironmentSetupTool.SetArray(multipass, "_transports", transports);
            transportManager.Transport = multipass;

            // 서버가 시작되면 룸 씬을, 연결이 끊기면 로비 씬을 불러온다.
            DefaultScene defaultScene = root.AddComponent<DefaultScene>();
            var sceneSettings = new SerializedObject(defaultScene);
            sceneSettings.FindProperty("_offlineScene").stringValue = lobbyScenePath;
            sceneSettings.FindProperty("_onlineScene").stringValue = roomScenePath;
            sceneSettings.FindProperty("_startInOffline").boolValue = false;
            sceneSettings.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<SteamBootstrap>();
            SteamLobbyService steamLobby = root.AddComponent<SteamLobbyService>();

            TransportConnector connector = root.AddComponent<TransportConnector>();
            TestEnvironmentSetupTool.SetField(connector, "networkManager", networkManager);

            SessionService session = root.AddComponent<SessionService>();
            TestEnvironmentSetupTool.SetField(session, "connector", connector);
            TestEnvironmentSetupTool.SetField(session, "steamLobby", steamLobby);
        }

        /// <summary>
        /// FishySteamworks 트랜스포트를 추가하고 P2P(SteamID 접속)를 켠다.
        /// FishySteamworks는 Assembly-CSharp에 있어 타입 이름으로 찾는다. 없으면 경고 후 null.
        /// </summary>
        private static Transport AddFishySteamworks(GameObject root)
        {
            Type steamType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(FishySteamworksTypeName))
                .FirstOrDefault(type => type != null);

            if (steamType == null)
            {
                Debug.LogWarning("[TestSetup] FishySteamworks를 찾지 못했습니다. Steam 방 없이 직접 접속만 사용합니다.");
                return null;
            }

            var transport = (Transport)root.AddComponent(steamType);
            var serialized = new SerializedObject(transport);
            SerializedProperty peerToPeer = serialized.FindProperty("_peerToPeer");
            if (peerToPeer != null)
            {
                peerToPeer.boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            return transport;
        }

        /// <summary>
        /// 메뉴 화면용 카메라를 만든다.
        /// </summary>
        private static void CreateMenuCamera()
        {
            var cameraObject = new GameObject("MenuCamera");
            cameraObject.tag = "MainCamera";
            Camera menuCamera = cameraObject.AddComponent<Camera>();
            menuCamera.clearFlags = CameraClearFlags.SolidColor;
            menuCamera.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
            cameraObject.AddComponent<AudioListener>();
        }

        #endregion

        #region 로비 UI

        /// <summary>
        /// 로비 캔버스에 없는 영역(왼쪽 아래 버전 표시, "모드" 화면, 모드 불일치 경고 창)만 만들어 붙인다. 이미 있는 프리팹을 고치지 않고 더할 때도 쓴다.
        /// </summary>
        /// <returns>새로 추가한 영역 수.</returns>
        public static int AddMissingLobbySections(GameObject canvas)
        {
            int added = 0;
            if (canvas.GetComponent<VersionLabel>() == null)
            {
                Text versionText = EditorUiFactory.CreateText(canvas.transform, "VersionText", string.Empty, 20, TextAnchor.LowerLeft, HintColor);
                EditorUiFactory.Place(versionText, Vector2.zero, new Vector2(320f, 30f), new Vector2(600f, 36f));
                versionText.raycastTarget = false;
                TestEnvironmentSetupTool.SetField(canvas.AddComponent<VersionLabel>(), "versionText", versionText);
                added++;
            }
            if (canvas.GetComponent<LobbyModsMenu>() == null && canvas.GetComponent<LobbyView>() != null)
            {
                CreateModsScreen(canvas);
                added++;
            }
            if (canvas.GetComponent<LobbyModMismatchPopup>() == null && canvas.GetComponent<LobbyView>() != null)
            {
                CreateModMismatchPopup(canvas);
                added++;
            }
            if (canvas.GetComponent<LobbyCreateMenu>() != null && !HasLobbyScreen(canvas.GetComponent<LobbyView>(), LobbyScreen.DirectCreate))
            {
                CreateDirectCreateScreen(canvas);
                added++;
            }
            return added;
        }

        /// <summary>
        /// "직접 방 만들기" 아래 화면(일반 방 만들기 / 테스트 방 만들기)을 만들어 로비 화면 목록에 등록한다.
        /// 방 만들기 화면의 "직접 방 만들기" 버튼이 이 화면을 연다.
        /// </summary>
        private static void CreateDirectCreateScreen(GameObject canvas)
        {
            RectTransform screen = CreateScreen(canvas.transform, "DirectCreatePanel", "lobby.create.direct");
            CreateHint(screen, "DirectHint", "lobby.direct.hint", 140f);
            Button normalButton = CreateMenuButton(screen, "HostNormalButton", "lobby.directCreate.normal", 60f, EditorUiFactory.AccentColor);
            Button testButton = CreateMenuButton(screen, "HostTestButton", "lobby.directCreate.test", -30f, EditorUiFactory.ButtonColor);
            CreateHint(screen, "TestHint", "lobby.directCreate.testHint", -115f);
            Button backButton = CreateMenuButton(screen, "BackButton", "lobby.back", -210f, EditorUiFactory.ButtonColor);

            PlaceBeforeOverlayPanels(canvas, screen);
            screen.gameObject.SetActive(false);
            RegisterLobbyScreen(canvas.GetComponent<LobbyView>(), LobbyScreen.DirectCreate, screen.gameObject, LobbyScreen.Create, backButton);

            LobbyCreateMenu menu = canvas.GetComponent<LobbyCreateMenu>();
            TestEnvironmentSetupTool.SetField(menu, "hostDirectNormalButton", normalButton);
            TestEnvironmentSetupTool.SetField(menu, "hostTestButton", testButton);
        }

        /// <summary>
        /// 로비 화면 목록에 그 화면이 패널과 함께 등록되어 있는지 확인한다.
        /// </summary>
        private static bool HasLobbyScreen(LobbyView view, LobbyScreen screen)
        {
            if (view == null)
                return false;

            SerializedProperty screens = new SerializedObject(view).FindProperty("screens");
            for (int i = 0; i < screens.arraySize; i++)
            {
                SerializedProperty entry = screens.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("screen").enumValueIndex == (int)screen && entry.FindPropertyRelative("panel").objectReferenceValue != null)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 로비 화면 목록과 뒤로 버튼 목록에 화면 하나를 더한다.
        /// </summary>
        private static void RegisterLobbyScreen(LobbyView view, LobbyScreen screen, GameObject panel, LobbyScreen parent, Button backButton)
        {
            var serializedView = new SerializedObject(view);
            SerializedProperty screens = serializedView.FindProperty("screens");
            int index = screens.arraySize;
            screens.arraySize++;
            SerializedProperty added = screens.GetArrayElementAtIndex(index);
            added.FindPropertyRelative("screen").enumValueIndex = (int)screen;
            added.FindPropertyRelative("panel").objectReferenceValue = panel;
            added.FindPropertyRelative("parent").enumValueIndex = (int)parent;

            SerializedProperty backButtons = serializedView.FindProperty("backButtons");
            backButtons.arraySize++;
            backButtons.GetArrayElementAtIndex(backButtons.arraySize - 1).objectReferenceValue = backButton;
            serializedView.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 설정·크레딧 패널이 있으면 그 앞에 둔다. (그 패널들이 화면 위에 그려지도록)
        /// </summary>
        private static void PlaceBeforeOverlayPanels(GameObject canvas, Transform screen)
        {
            SettingsPanelView settings = canvas.GetComponentInChildren<SettingsPanelView>(true);
            if (settings != null && settings.transform.parent == canvas.transform)
                screen.SetSiblingIndex(settings.transform.GetSiblingIndex());
        }

        /// <summary>
        /// 모드가 달라 입장이 거부됐을 때 뜨는 경고 창(어두운 배경 + 경고문, 부족한 모드 목록, 모드 화면 열기 / 닫기)을 만든다.
        /// 다른 화면 위에 그려지도록 캔버스 맨 뒤에 둔다.
        /// </summary>
        private static void CreateModMismatchPopup(GameObject canvas)
        {
            RectTransform dim = EditorUiFactory.CreatePanel(canvas.transform, "ModMismatchPopup", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.65f));

            RectTransform box = EditorUiFactory.CreatePanel(dim, "Box", new Vector2(0.3f, 0.22f), new Vector2(0.7f, 0.78f), EditorUiFactory.PanelColor);
            EditorUiFactory.AddVerticalLayout(box, 12f, 28);

            Text title = EditorUiFactory.CreateText(box, "Title", "lobby.modMismatch.title", 34, TextAnchor.MiddleCenter, EditorUiFactory.NoticeColor);
            title.fontStyle = FontStyle.Bold;
            EditorUiFactory.SetHeight(title, 48f);
            Text warning = EditorUiFactory.CreateText(box, "Warning", "lobby.modMismatch.warning", 22, TextAnchor.MiddleCenter, EditorUiFactory.TextColor);
            EditorUiFactory.SetHeight(warning, 70f);
            Text listHeader = EditorUiFactory.CreateText(box, "ListHeader", "lobby.modMismatch.listHeader", 22, TextAnchor.MiddleLeft, EditorUiFactory.AccentColor);
            EditorUiFactory.SetHeight(listHeader, 32f);

            // 부족한 모드가 많아도 넘치지 않도록 스크롤한다.
            Text list = EditorUiFactory.CreateScrollText(box, "MissingList", string.Empty, 22, TextAnchor.UpperLeft, EditorUiFactory.TextColor);
            EditorUiFactory.FillRemainingHeight(list.transform.parent, 140f);

            RectTransform buttons = EditorUiFactory.CreateRow(box, "Buttons", 56f, 12f);
            Button openMods = EditorUiFactory.CreateButton(buttons, "OpenModsButton", "lobby.modMismatch.openMods", EditorUiFactory.ButtonColor);
            Button close = EditorUiFactory.CreateButton(buttons, "CloseButton", "lobby.modMismatch.close", EditorUiFactory.AccentColor);
            EditorUiFactory.SetFlexibleWidth(openMods);
            EditorUiFactory.SetFlexibleWidth(close);

            dim.SetAsLastSibling();
            dim.gameObject.SetActive(false);

            LobbyModMismatchPopup popup = canvas.AddComponent<LobbyModMismatchPopup>();
            TestEnvironmentSetupTool.SetField(popup, "popupPanel", dim.gameObject);
            TestEnvironmentSetupTool.SetField(popup, "missingListText", list);
            TestEnvironmentSetupTool.SetField(popup, "closeButton", close);
            TestEnvironmentSetupTool.SetField(popup, "openModsButton", openMods);
        }

        /// <summary>
        /// "모드" 화면(모드 목록, 다시 읽기, 모드 폴더 열기, 뒤로)과 메인 화면 오른쪽 위의 "모드" 버튼을 만들어 로비 화면 목록에 등록한다.
        /// 설정·크레딧 패널보다 아래에 그려지도록 그 앞에 끼워 넣는다.
        /// </summary>
        private static void CreateModsScreen(GameObject canvas)
        {
            LobbyView view = canvas.GetComponent<LobbyView>();
            var serializedView = new SerializedObject(view);
            SerializedProperty screens = serializedView.FindProperty("screens");
            Transform mainPanel = null;
            for (int i = 0; i < screens.arraySize; i++)
            {
                SerializedProperty entry = screens.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("screen").enumValueIndex == (int)LobbyScreen.Main)
                    mainPanel = (entry.FindPropertyRelative("panel").objectReferenceValue as GameObject)?.transform;
            }

            Button openButton = EditorUiFactory.CreateButton(mainPanel != null ? mainPanel : canvas.transform, "ModsMenuButton", "lobby.menu.mods", EditorUiFactory.ButtonColor);
            EditorUiFactory.Place(openButton, Center, new Vector2(250f, 230f), new Vector2(180f, 50f));

            RectTransform modsScreen = CreateScreen(canvas.transform, "ModsMenu", "lobby.mods.title");
            CreateHint(modsScreen, "ModsHint", "lobby.mods.hint", 170f);
            RectTransform listContent = EditorUiFactory.CreateScrollList(modsScreen, "ModList", 6f);
            EditorUiFactory.Place(listContent.parent.parent, Center, new Vector2(0f, 10f), new Vector2(660f, 250f));

            Button rowTemplate = EditorUiFactory.CreateButton(listContent, "ModRowTemplate", string.Empty, EditorUiFactory.ButtonColor, out Text rowLabel);
            rowLabel.alignment = TextAnchor.MiddleLeft;
            rowLabel.fontSize = 20;
            EditorUiFactory.StretchWithPadding(rowLabel.rectTransform, 14f);
            EditorUiFactory.SetHeight(rowTemplate, 64f);
            rowTemplate.gameObject.SetActive(false);

            Text summary = EditorUiFactory.CreateText(modsScreen, "Summary", string.Empty, 18, TextAnchor.MiddleCenter, HintColor);
            EditorUiFactory.Place(summary, Center, new Vector2(0f, -150f), new Vector2(660f, 56f));

            Button refreshButton = EditorUiFactory.CreateButton(modsScreen, "RefreshButton", "lobby.mods.refresh", EditorUiFactory.ButtonColor);
            EditorUiFactory.Place(refreshButton, Center, new Vector2(-225f, -220f), new Vector2(210f, 56f));
            Button folderButton = EditorUiFactory.CreateButton(modsScreen, "OpenFolderButton", "lobby.mods.openFolder", EditorUiFactory.ButtonColor);
            EditorUiFactory.Place(folderButton, Center, new Vector2(0f, -220f), new Vector2(210f, 56f));
            Button backButton = EditorUiFactory.CreateButton(modsScreen, "BackButton", "lobby.back", EditorUiFactory.ButtonColor);
            EditorUiFactory.Place(backButton, Center, new Vector2(225f, -220f), new Vector2(210f, 56f));

            PlaceBeforeOverlayPanels(canvas, modsScreen);
            modsScreen.gameObject.SetActive(false);
            RegisterLobbyScreen(view, LobbyScreen.Mods, modsScreen.gameObject, LobbyScreen.Main, backButton);

            LobbyModsMenu menu = canvas.AddComponent<LobbyModsMenu>();
            TestEnvironmentSetupTool.SetField(menu, "openButton", openButton);
            TestEnvironmentSetupTool.SetField(menu, "listContent", listContent);
            TestEnvironmentSetupTool.SetField(menu, "rowTemplate", rowTemplate);
            TestEnvironmentSetupTool.SetField(menu, "summaryText", summary);
            TestEnvironmentSetupTool.SetField(menu, "refreshButton", refreshButton);
            TestEnvironmentSetupTool.SetField(menu, "openFolderButton", folderButton);
        }

        /// <summary>
        /// 로비 화면 프리팹(없으면 기본 모양으로 만들어 저장)을 씬에 넣는다. 직접 고친 프리팹은 그대로 쓴다.
        /// </summary>
        private static void CreateLobbyUi()
        {
            GameObject prefab = UiPrefabLibrary.GetOrBuildRoot(UiPrefabLibrary.LobbyCanvasPath, BuildLobbyCanvas);
            UiPrefabLibrary.Instantiate(prefab);
        }

        // 로비 화면 배치 (1920×1080 기준, 화면 가운데 기준 픽셀). Layout Group을 쓰지 않아 프리팹에서 자유롭게 옮길 수 있다.
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 ScreenPosition = new Vector2(0f, -40f);
        private static readonly Vector2 ScreenSize = new Vector2(720f, 560f);
        private static readonly Vector2 ButtonSize = new Vector2(480f, 70f);
        private static readonly Vector2 InputSize = new Vector2(480f, 64f);
        private static readonly Color HintColor = new Color(0.7f, 0.7f, 0.7f);

        /// <summary>
        /// 로비 화면의 기본 모양을 만든다. 화면(패널)을 겹쳐 두고 하나씩 보여준다.
        /// 메인 ─ 방 만들기 ─ 스팀 방 만들기(암호) / 직접 방 만들기
        ///      ─ 참가     ─ 스팀 방 입장(방 코드, 암호) / IP로 참가
        ///      ─ 설정, 크레딧, 종료
        /// 모든 요소는 고정 위치·크기(Layout Group 없음)라 프리팹에서 끌어서 옮기거나 크기를 바꿀 수 있다.
        /// </summary>
        private static GameObject BuildLobbyCanvas()
        {
            Canvas canvas = EditorUiFactory.CreateCanvas("LobbyCanvas");
            canvas.gameObject.AddComponent<OsFontApplier>();

            Text title = EditorUiFactory.CreateText(canvas.transform, "Title", "lobby.title", 72, TextAnchor.MiddleCenter, EditorUiFactory.TextColor);
            EditorUiFactory.Place(title, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1200f, 140f));

            // 메인
            RectTransform main = CreateScreen(canvas.transform, "MainMenu", null);
            Button createMenuButton = CreateMenuButton(main, "CreateMenuButton", "lobby.menu.create", 150f, EditorUiFactory.AccentColor);
            Button joinMenuButton = CreateMenuButton(main, "JoinMenuButton", "lobby.menu.join", 60f, EditorUiFactory.AccentColor);
            Button settingsButton = CreateMenuButton(main, "SettingsButton", "lobby.settings", -30f, EditorUiFactory.ButtonColor);
            Button creditsButton = CreateMenuButton(main, "CreditsButton", "lobby.credits", -120f, EditorUiFactory.ButtonColor);
            Button quitButton = CreateMenuButton(main, "QuitButton", "lobby.quit", -210f, EditorUiFactory.ButtonColor);

            // 방 만들기
            RectTransform createMenu = CreateScreen(canvas.transform, "CreateMenu", "lobby.menu.create");
            Button steamCreateMenuButton = CreateMenuButton(createMenu, "SteamCreateMenuButton", "lobby.create.steam", 100f, EditorUiFactory.AccentColor);
            Button hostDirectButton = CreateMenuButton(createMenu, "HostDirectButton", "lobby.create.direct", 10f, EditorUiFactory.ButtonColor);
            CreateHint(createMenu, "DirectHint", "lobby.direct.hint", -80f);
            Button createBack = CreateMenuButton(createMenu, "BackButton", "lobby.back", -210f, EditorUiFactory.ButtonColor);

            // 스팀 방 만들기 (암호)
            RectTransform steamCreate = CreateScreen(canvas.transform, "SteamCreatePanel", "lobby.create.steam");
            CreateHint(steamCreate, "PasswordHint", "lobby.steamCreate.hint", 130f);
            InputField hostPassword = CreateInput(steamCreate, "PasswordInput", "lobby.password.placeholder", 40f);
            Button hostSteamButton = CreateMenuButton(steamCreate, "CreateButton", "lobby.steamCreate.confirm", -60f, EditorUiFactory.AccentColor);
            Button steamCreateBack = CreateMenuButton(steamCreate, "BackButton", "lobby.back", -210f, EditorUiFactory.ButtonColor);

            // 참가
            RectTransform joinMenu = CreateScreen(canvas.transform, "JoinMenu", "lobby.menu.join");
            Button steamJoinMenuButton = CreateMenuButton(joinMenu, "SteamJoinMenuButton", "lobby.join.steam", 100f, EditorUiFactory.AccentColor);
            Button directJoinMenuButton = CreateMenuButton(joinMenu, "DirectJoinMenuButton", "lobby.join.direct", 10f, EditorUiFactory.ButtonColor);
            CreateHint(joinMenu, "InviteHint", "lobby.steam.inviteHint", -80f);
            Button joinBack = CreateMenuButton(joinMenu, "BackButton", "lobby.back", -210f, EditorUiFactory.ButtonColor);

            // 스팀 방 입장 (방 코드, 암호)
            RectTransform steamJoin = CreateScreen(canvas.transform, "SteamJoinPanel", "lobby.join.steam");
            InputField roomCode = CreateInput(steamJoin, "RoomCodeInput", "lobby.steam.codePlaceholder", 120f);
            InputField joinPassword = CreateInput(steamJoin, "PasswordInput", "lobby.steamJoin.passwordPlaceholder", 40f);
            Button joinSteamButton = CreateMenuButton(steamJoin, "JoinButton", "lobby.steamJoin.confirm", -60f, EditorUiFactory.AccentColor);
            Button steamJoinBack = CreateMenuButton(steamJoin, "BackButton", "lobby.back", -210f, EditorUiFactory.ButtonColor);

            // IP로 참가
            RectTransform directJoin = CreateScreen(canvas.transform, "DirectJoinPanel", "lobby.join.direct");
            CreateHint(directJoin, "DirectHint", "lobby.direct.hint", 130f);
            InputField address = CreateInput(directJoin, "AddressInput", "lobby.direct.addressPlaceholder", 40f);
            Button joinDirectButton = CreateMenuButton(directJoin, "JoinButton", "lobby.directJoin.confirm", -60f, EditorUiFactory.AccentColor);
            Button directJoinBack = CreateMenuButton(directJoin, "BackButton", "lobby.back", -210f, EditorUiFactory.ButtonColor);

            // 아래: Steam 상태와 안내 문구 (모든 화면 공통)
            Text steamStatus = EditorUiFactory.CreateText(canvas.transform, "SteamStatus", string.Empty, 22, TextAnchor.MiddleCenter, HintColor);
            EditorUiFactory.Place(steamStatus, new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(1400f, 40f));
            Text notice = EditorUiFactory.CreateText(canvas.transform, "Notice", string.Empty, 24, TextAnchor.MiddleCenter, EditorUiFactory.NoticeColor);
            EditorUiFactory.Place(notice, new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(1600f, 50f));

            canvas.gameObject.AddComponent<CursorUnlockOnStart>();
            LobbyView view = canvas.gameObject.AddComponent<LobbyView>();
            // 화면 목록: (화면, 패널, 뒤로 가면 돌아갈 화면). 첫 항목이 시작 화면.
            SetLobbyScreens(view,
                (LobbyScreen.Main, main.gameObject, LobbyScreen.Main),
                (LobbyScreen.Create, createMenu.gameObject, LobbyScreen.Main),
                (LobbyScreen.SteamCreate, steamCreate.gameObject, LobbyScreen.Create),
                (LobbyScreen.Join, joinMenu.gameObject, LobbyScreen.Main),
                (LobbyScreen.SteamJoin, steamJoin.gameObject, LobbyScreen.Join),
                (LobbyScreen.DirectJoin, directJoin.gameObject, LobbyScreen.Join));

            // 화면별 버튼 코드 (모두 캔버스에 붙여, 화면이 꺼져 있어도 이벤트를 받을 수 있게 한다)
            LobbyMainMenu mainMenuCode = canvas.gameObject.AddComponent<LobbyMainMenu>();
            TestEnvironmentSetupTool.SetField(mainMenuCode, "createMenuButton", createMenuButton);
            TestEnvironmentSetupTool.SetField(mainMenuCode, "joinMenuButton", joinMenuButton);
            TestEnvironmentSetupTool.SetField(mainMenuCode, "settingsButton", settingsButton);
            TestEnvironmentSetupTool.SetField(mainMenuCode, "creditsButton", creditsButton);
            TestEnvironmentSetupTool.SetField(mainMenuCode, "quitButton", quitButton);

            LobbyCreateMenu createMenuCode = canvas.gameObject.AddComponent<LobbyCreateMenu>();
            TestEnvironmentSetupTool.SetField(createMenuCode, "steamCreateMenuButton", steamCreateMenuButton);
            TestEnvironmentSetupTool.SetField(createMenuCode, "hostDirectButton", hostDirectButton);
            TestEnvironmentSetupTool.SetField(createMenuCode, "hostPasswordInput", hostPassword);
            TestEnvironmentSetupTool.SetField(createMenuCode, "hostSteamButton", hostSteamButton);

            LobbyJoinMenu joinMenuCode = canvas.gameObject.AddComponent<LobbyJoinMenu>();
            TestEnvironmentSetupTool.SetField(joinMenuCode, "steamJoinMenuButton", steamJoinMenuButton);
            TestEnvironmentSetupTool.SetField(joinMenuCode, "directJoinMenuButton", directJoinMenuButton);
            TestEnvironmentSetupTool.SetField(joinMenuCode, "roomCodeInput", roomCode);
            TestEnvironmentSetupTool.SetField(joinMenuCode, "joinPasswordInput", joinPassword);
            TestEnvironmentSetupTool.SetField(joinMenuCode, "joinSteamButton", joinSteamButton);
            TestEnvironmentSetupTool.SetField(joinMenuCode, "addressInput", address);
            TestEnvironmentSetupTool.SetField(joinMenuCode, "joinDirectButton", joinDirectButton);

            TestEnvironmentSetupTool.SetArray(view, "backButtons", new UnityEngine.Object[] { createBack, steamCreateBack, joinBack, steamJoinBack, directJoinBack });
            TestEnvironmentSetupTool.SetField(view, "steamStatusText", steamStatus);
            TestEnvironmentSetupTool.SetField(view, "noticeText", notice);

            // 버전 표시와 "모드" 화면 (기존 프리팹에는 "빠진 UI 영역 추가" 메뉴로 더한다)
            AddMissingLobbySections(canvas.gameObject);

            // 설정과 크레딧 화면 (메뉴 위에 그려지도록 마지막에 만든다)
            TestEnvironmentSetupTool.SetField(view, "settingsPanel", SharedUiBuilder.CreateSettingsPanel(canvas.transform));
            TestEnvironmentSetupTool.SetField(view, "creditsPanel", SharedUiBuilder.CreateCreditsPanel(canvas.transform));
            return canvas.gameObject;
        }

        /// <summary>
        /// 로비 화면 목록(LobbyView.screens)을 채운다. 항목 = (화면, 패널, 뒤로 가면 돌아갈 화면)
        /// </summary>
        private static void SetLobbyScreens(LobbyView view, params (LobbyScreen screen, GameObject panel, LobbyScreen parent)[] entries)
        {
            var serialized = new SerializedObject(view);
            SerializedProperty screens = serialized.FindProperty("screens");
            screens.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                SerializedProperty element = screens.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("screen").enumValueIndex = (int)entries[i].screen;
                element.FindPropertyRelative("panel").objectReferenceValue = entries[i].panel;
                element.FindPropertyRelative("parent").enumValueIndex = (int)entries[i].parent;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 로비 화면(패널) 하나를 화면 가운데에 만든다. 제목 key가 있으면 위쪽에 제목을 둔다.
        /// </summary>
        private static RectTransform CreateScreen(Transform canvas, string name, string titleKey)
        {
            RectTransform screen = EditorUiFactory.CreateRect(canvas, name);
            screen.gameObject.AddComponent<Image>().color = EditorUiFactory.PanelColor;
            EditorUiFactory.Place(screen, Center, ScreenPosition, ScreenSize);

            if (!string.IsNullOrEmpty(titleKey))
            {
                Text header = EditorUiFactory.CreateText(screen, "Header", titleKey, 36, TextAnchor.MiddleCenter, EditorUiFactory.AccentColor);
                header.fontStyle = FontStyle.Bold;
                EditorUiFactory.Place(header, Center, new Vector2(0f, 220f), new Vector2(640f, 60f));
            }
            return screen;
        }

        /// <summary>
        /// 화면 가운데 세로 위치(y)에 메뉴 버튼을 만든다.
        /// </summary>
        private static Button CreateMenuButton(Transform screen, string name, string labelKey, float y, Color color)
        {
            Button button = EditorUiFactory.CreateButton(screen, name, labelKey, color);
            EditorUiFactory.Place(button, Center, new Vector2(0f, y), ButtonSize);
            return button;
        }

        /// <summary>
        /// 화면 가운데 세로 위치(y)에 입력 칸을 만든다.
        /// </summary>
        private static InputField CreateInput(Transform screen, string name, string placeholderKey, float y)
        {
            InputField input = EditorUiFactory.CreateInputField(screen, name, placeholderKey);
            EditorUiFactory.Place(input, Center, new Vector2(0f, y), InputSize);
            return input;
        }

        /// <summary>
        /// 화면 가운데 세로 위치(y)에 안내 문구를 만든다.
        /// </summary>
        private static void CreateHint(Transform screen, string name, string key, float y)
        {
            Text hint = EditorUiFactory.CreateText(screen, name, key, 20, TextAnchor.MiddleCenter, HintColor);
            EditorUiFactory.Place(hint, Center, new Vector2(0f, y), new Vector2(640f, 70f));
        }

        #endregion

        #region 룸 UI

        /// <summary>
        /// 룸 화면 프리팹(없으면 기본 모양으로 만들어 저장)을 씬에 넣는다. 직접 고친 프리팹은 그대로 쓴다.
        /// </summary>
        private static void CreateRoomUi()
        {
            GameObject prefab = UiPrefabLibrary.GetOrBuildRoot(UiPrefabLibrary.RoomCanvasPath, BuildRoomCanvas);
            UiPrefabLibrary.Instantiate(prefab);
        }

        /// <summary>
        /// 룸 화면(참가자·방 코드 / 게임 설정 / 하단 버튼)의 기본 모양을 만든다.
        /// 게임 설정 줄은 game_settings.csv의 항목 수만큼 만든다. (항목을 추가하면 UI 프리팹 초기화가 필요)
        /// </summary>
        private static GameObject BuildRoomCanvas()
        {
            Canvas canvas = EditorUiFactory.CreateCanvas("RoomCanvas");
            canvas.gameObject.AddComponent<OsFontApplier>();

            // 왼쪽: 참가자와 방 코드
            RectTransform left = EditorUiFactory.CreatePanel(canvas.transform, "MembersPanel", new Vector2(0.03f, 0.16f), new Vector2(0.45f, 0.95f), EditorUiFactory.PanelColor);
            EditorUiFactory.AddVerticalLayout(left, 14f, 28);
            AddHeader(left, "room.members.header");
            // 참가자(최대 10명)가 넘치면 스크롤한다.
            Text memberList = EditorUiFactory.CreateScrollText(left, "MemberList", string.Empty, 26, TextAnchor.UpperLeft, EditorUiFactory.TextColor);
            EditorUiFactory.FillRemainingHeight(memberList.transform.parent, 200f);

            Text roomCode = AddLine(left, "RoomCode", string.Empty, 22, EditorUiFactory.TextColor);
            RectTransform codeRow = EditorUiFactory.CreateRow(left, "CodeButtons", 52f, 10f);
            Button copyButton = EditorUiFactory.CreateButton(codeRow, "CopyCodeButton", "room.copyCode", EditorUiFactory.ButtonColor);
            Button inviteButton = EditorUiFactory.CreateButton(codeRow, "InviteButton", "room.invite", EditorUiFactory.ButtonColor);
            EditorUiFactory.SetFlexibleWidth(copyButton);
            EditorUiFactory.SetFlexibleWidth(inviteButton);

            // 오른쪽: 게임 설정
            RectTransform right = EditorUiFactory.CreatePanel(canvas.transform, "SettingsPanel", new Vector2(0.47f, 0.16f), new Vector2(0.97f, 0.95f), EditorUiFactory.PanelColor);
            EditorUiFactory.AddVerticalLayout(right, 8f, 20);
            Text settingsHint = AddLine(right, "SettingsHint", string.Empty, 26, EditorUiFactory.TextColor);

            // 설정 항목이 많아 패널을 넘치면 스크롤한다.
            RectTransform settingList = EditorUiFactory.CreateScrollList(right, "SettingList", 4f);
            EditorUiFactory.FillRemainingHeight(settingList, 200f);
            SettingRowView[] rows = GameSettingDefinitions.All
                .Where(definition => GameSettingDefinitions.IsShownInRoom(definition.id))
                .Select(definition => CreateSettingRow(settingList, definition.id)).ToArray();

            // 하단: 상태, 나가기, 시작/준비
            RectTransform bottom = EditorUiFactory.CreatePanel(canvas.transform, "BottomBar", new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.13f), EditorUiFactory.PanelColor);
            HorizontalLayoutGroup bottomLayout = bottom.gameObject.AddComponent<HorizontalLayoutGroup>();
            bottomLayout.padding = new RectOffset(20, 20, 14, 14);
            bottomLayout.spacing = 16f;
            bottomLayout.childControlWidth = true;
            bottomLayout.childControlHeight = true;
            bottomLayout.childForceExpandWidth = false;

            Text status = EditorUiFactory.CreateText(bottom, "Status", string.Empty, 24, TextAnchor.MiddleLeft, EditorUiFactory.NoticeColor);
            EditorUiFactory.SetFlexibleWidth(status);
            Button leaveButton = EditorUiFactory.CreateButton(bottom, "LeaveButton", "room.leave", EditorUiFactory.ButtonColor);
            EditorUiFactory.SetWidth(leaveButton, 220f);
            // 시작/준비/준비 해제로 바뀌는 버튼이라 RoomFooterView가 문구를 채운다. (key를 넘기지 않음)
            Button primaryButton = EditorUiFactory.CreateButton(bottom, "PrimaryButton", string.Empty, EditorUiFactory.AccentColor, out Text primaryLabel);
            EditorUiFactory.SetWidth(primaryButton, 320f);

            // 화면 관리 + 영역별 코드 (모두 캔버스에 붙인다)
            canvas.gameObject.AddComponent<CursorUnlockOnStart>();

            RoomMemberListView members = canvas.gameObject.AddComponent<RoomMemberListView>();
            TestEnvironmentSetupTool.SetField(members, "memberListText", memberList);

            RoomCodeView code = canvas.gameObject.AddComponent<RoomCodeView>();
            TestEnvironmentSetupTool.SetField(code, "roomCodeText", roomCode);
            TestEnvironmentSetupTool.SetField(code, "copyCodeButton", copyButton);
            TestEnvironmentSetupTool.SetField(code, "inviteButton", inviteButton);

            RoomSettingsView settings = canvas.gameObject.AddComponent<RoomSettingsView>();
            TestEnvironmentSetupTool.SetField(settings, "settingsHintText", settingsHint);
            TestEnvironmentSetupTool.SetArray(settings, "settingRows", rows);

            RoomFooterView footer = canvas.gameObject.AddComponent<RoomFooterView>();
            TestEnvironmentSetupTool.SetField(footer, "primaryButton", primaryButton);
            TestEnvironmentSetupTool.SetField(footer, "primaryButtonLabel", primaryLabel);
            TestEnvironmentSetupTool.SetField(footer, "leaveButton", leaveButton);
            TestEnvironmentSetupTool.SetField(footer, "statusText", status);

            // ESC 메뉴: 룸에서는 커서를 잠그지 않는다.
            SharedUiBuilder.CreatePauseMenu(canvas.transform, lockCursorOnClose: false);
            return canvas.gameObject;
        }

        /// <summary>
        /// 설정 줄 목록(settingRows 필드)을 가진 화면에, 코드에는 있지만 프리팹에 빠진 설정 줄을 기존 줄 옆에 더한다.
        /// (룸 설정 화면, HUD 테스트 메뉴. 새 설정 항목을 기존 프리팹에 넣을 때 사용)
        /// </summary>
        /// <param name="owner">settingRows 필드를 가진 컴포넌트. 없으면 -1.</param>
        /// <param name="include">이 화면에 보여줄 항목인지.</param>
        /// <returns>추가한 줄 수. 대상을 찾지 못하면 -1.</returns>
        internal static int AddMissingSettingRows(Component owner, Func<GameSettingId, bool> include)
        {
            if (owner == null)
                return -1;

            var serialized = new SerializedObject(owner);
            SerializedProperty rows = serialized.FindProperty("settingRows");
            var existing = new HashSet<GameSettingId>();
            Transform list = null;
            for (int i = 0; i < rows.arraySize; i++)
            {
                if (rows.GetArrayElementAtIndex(i).objectReferenceValue is SettingRowView row)
                {
                    existing.Add(row.SettingId);
                    list = row.transform.parent;
                }
            }
            if (list == null)
                return -1;

            int added = 0;
            foreach (GameSettingDefinitions.Definition definition in GameSettingDefinitions.All)
            {
                if (existing.Contains(definition.id) || !include(definition.id))
                    continue;

                SettingRowView row = CreateSettingRow(list, definition.id);
                rows.arraySize++;
                rows.GetArrayElementAtIndex(rows.arraySize - 1).objectReferenceValue = row;
                added++;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return added;
        }

        /// <summary>
        /// 설정 한 줄(이름 | - | 값 | +)을 만든다. 맵 이외의 값은 눌러서 키보드로 입력할 수 있는 칸이다.
        /// </summary>
        internal static SettingRowView CreateSettingRow(Transform parent, GameSettingId id)
        {
            RectTransform row = EditorUiFactory.CreateRow(parent, $"Row_{id}", 38f, 8f);

            // 이름은 SettingRowView가 현재 언어로 채운다.
            Text label = EditorUiFactory.CreateText(row, "Label", id.ToString(), 21, TextAnchor.MiddleLeft, EditorUiFactory.TextColor);
            EditorUiFactory.SetFlexibleWidth(label);

            Button decrease = EditorUiFactory.CreateButton(row, "Decrease", "-", EditorUiFactory.ButtonColor);
            EditorUiFactory.SetWidth(decrease, 48f);

            // 맵은 이름으로 고르므로 -/+ 버튼만, 나머지는 값을 눌러 키보드로도 입력할 수 있게 한다.
            Text value;
            InputField valueInput = null;
            if (id == GameSettingId.Map)
            {
                value = EditorUiFactory.CreateText(row, "Value", "-", 21, TextAnchor.MiddleCenter, EditorUiFactory.TextColor);
                EditorUiFactory.SetWidth(value, 150f);
            }
            else
            {
                valueInput = EditorUiFactory.CreateInputField(row, "ValueInput", string.Empty);
                value = valueInput.textComponent;
                value.alignment = TextAnchor.MiddleCenter;
                value.fontSize = 21;
                EditorUiFactory.SetWidth(valueInput, 150f);
            }

            Button increase = EditorUiFactory.CreateButton(row, "Increase", "+", EditorUiFactory.ButtonColor);
            EditorUiFactory.SetWidth(increase, 48f);

            SettingRowView view = row.gameObject.AddComponent<SettingRowView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("settingId").enumValueIndex = (int)id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            TestEnvironmentSetupTool.SetField(view, "labelText", label);
            TestEnvironmentSetupTool.SetField(view, "valueText", value);
            TestEnvironmentSetupTool.SetField(view, "valueInput", valueInput);
            TestEnvironmentSetupTool.SetField(view, "decreaseButton", decrease);
            TestEnvironmentSetupTool.SetField(view, "increaseButton", increase);
            return view;
        }

        #endregion

        /// <summary>
        /// 구역 제목을 추가한다.
        /// </summary>
        private static void AddHeader(Transform parent, string content)
        {
            Text header = EditorUiFactory.CreateText(parent, "Header", content, 30, TextAnchor.MiddleLeft, EditorUiFactory.AccentColor);
            header.fontStyle = FontStyle.Bold;
            EditorUiFactory.SetHeight(header, 40f);
        }

        /// <summary>
        /// 한 줄 텍스트를 추가한다.
        /// </summary>
        private static Text AddLine(Transform parent, string name, string content, int fontSize, Color color)
        {
            Text text = EditorUiFactory.CreateText(parent, name, content, fontSize, TextAnchor.MiddleLeft, color);
            EditorUiFactory.SetHeight(text, fontSize + 14f);
            return text;
        }
    }
}
