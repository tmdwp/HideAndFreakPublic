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
    /// <summary>로비 씬과 룸 씬을 코드로 만들어 항상 같은 구성을 보장한다.</summary>
    public static class MenuSceneBuilder
    {
        private const string FishySteamworksTypeName = "FishySteamworks.FishySteamworks";
        private const string SceneConditionPath = "Assets/FishNet/Runtime/Observing/Conditions/ScriptableObjects/SceneCondition.asset";

        /// <summary>로비로 돌아올 때 두 벌이 되지 않도록 씬 전환에도 유지되는 네트워크 루트를 첫 씬에 둔다.</summary>
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

            // 배경 연출이 보이도록 메뉴 카메라를 방 안으로 옮긴다.
            LobbyBackdropBuilder.AddToScene(scene);

            SceneLightingSetup.Apply(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        /// <summary>방 상태는 네트워크 오브젝트라 씬에 미리 둘 수 없어 서버가 스폰하도록 부트스트랩을 둔다.</summary>
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

        /// <summary>씬 전환 후에도 서비스가 유지되도록 NetworkManager와 같은 오브젝트에 트랜스포트·세션 서비스를 둔다.</summary>
        private static void CreateNetworkRoot(string lobbyScenePath, string roomScenePath, DefaultPrefabObjects spawnablePrefabs)
        {
            var root = new GameObject("NetworkRoot");

            NetworkManager networkManager = root.AddComponent<NetworkManager>();
            if (spawnablePrefabs != null)
                networkManager.SpawnablePrefabs = spawnablePrefabs;

            // 로딩 중인 게스트가 GameSystems 스폰 정보를 먼저 받아 놓치지 않도록 씬 오브젝트는 그 씬을 불러온 클라이언트에게만 보이게 한다.
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

        /// <summary>FishySteamworks는 Assembly-CSharp에 있어 타입 이름으로 찾아 P2P로 붙인다.</summary>
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
        /// 카메라가 없으면 Overlay가 아닌 화면이 검게 나오고 배경 연출도 보이지 않으므로 메뉴용 카메라를 둔다.
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

        /// <summary>기존 프리팹을 고치지 않고 로비 캔버스에 빠진 영역만 더하고 없어진 경고 창은 지운다.</summary>
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
            added += RemoveOldModMismatchPopup(canvas);
            if (canvas.GetComponent<LobbyView>() != null && canvas.GetComponent<LobbyRejoinView>() == null)
            {
                CreateRejoinButtons(canvas);
                added++;
            }
            if (canvas.GetComponent<LobbyCreateMenu>() != null && AddSteamRoomOptions(canvas))
                added++;
            if (canvas.GetComponent<LobbyJoinMenu>() != null && !HasLobbyScreen(canvas.GetComponent<LobbyView>(), LobbyScreen.Browse))
            {
                CreateBrowseScreen(canvas);
                added++;
            }
            if (canvas.GetComponent<LobbyCreateMenu>() != null && !HasLobbyScreen(canvas.GetComponent<LobbyView>(), LobbyScreen.DirectCreate))
            {
                CreateDirectCreateScreen(canvas);
                added++;
            }
            return added;
        }

        /// <summary>실수로 테스트 방을 열지 않도록 일반 방과 테스트 방 만들기를 따로 나눈 화면을 만든다.</summary>
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
        /// 보강을 여러 번 실행해도 화면이 두 번 생기지 않도록 이미 패널과 함께 등록되어 있는지 확인한다.
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
        /// 화면 목록·뒤로 버튼 목록은 직렬화 배열이라 코드로 더하려면 SerializedObject로 늘려야 하므로 한곳에서 처리한다.
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
        /// 나중에 더한 화면이 설정·크레딧 패널 위에 그려지면 패널을 가리므로 그 앞에 끼워 넣는다.
        /// </summary>
        private static void PlaceBeforeOverlayPanels(GameObject canvas, Transform screen)
        {
            SettingsPanelView settings = canvas.GetComponentInChildren<SettingsPanelView>(true);
            if (settings != null && settings.transform.parent == canvas.transform)
                screen.SetSiblingIndex(settings.transform.GetSiblingIndex());
        }

        /// <summary>끊긴 사람이 다시 참가할 방법을 찾기 쉽도록 로비 아래쪽에 다시 참가·포기 버튼을 둔다.</summary>
        private static void CreateRejoinButtons(GameObject canvas)
        {
            RectTransform root = EditorUiFactory.CreateRect(canvas.transform, "RejoinButtons");
            EditorUiFactory.Place(root, new Vector2(0.5f, 0f), new Vector2(0f, 185f), new Vector2(640f, 56f));

            Button rejoin = EditorUiFactory.CreateButton(root, "RejoinButton", string.Empty, EditorUiFactory.AccentColor, out Text rejoinLabel);
            EditorUiFactory.Place(rejoin, Center, new Vector2(-90f, 0f), new Vector2(440f, 56f));
            Button discard = EditorUiFactory.CreateButton(root, "DiscardButton", "lobby.rejoin.discard", EditorUiFactory.ButtonColor);
            EditorUiFactory.Place(discard, Center, new Vector2(230f, 0f), new Vector2(180f, 56f));

            LobbyRejoinView view = canvas.AddComponent<LobbyRejoinView>();
            TestEnvironmentSetupTool.SetField(view, "root", root.gameObject);
            TestEnvironmentSetupTool.SetField(view, "rejoinButton", rejoin);
            TestEnvironmentSetupTool.SetField(view, "rejoinLabel", rejoinLabel);
            TestEnvironmentSetupTool.SetField(view, "discardButton", discard);
            root.gameObject.SetActive(false);
        }

        /// <summary>판 중에도 문제 있는 플레이어를 내보낼 수 있도록 ESC 메뉴와 방 코드 줄에 플레이어 관리 창을 더한다.</summary>
        /// <returns>추가했으면 1, 대상이 없거나 이미 있으면 0.</returns>
        public static int AddMissingPlayerPanel(GameObject canvas)
        {
            PauseMenuView pauseMenu = canvas.GetComponent<PauseMenuView>();
            if (pauseMenu == null || canvas.GetComponent<HostPlayerPanel>() != null)
                return 0;

            var openButtons = new List<UnityEngine.Object>();
            if (new SerializedObject(pauseMenu).FindProperty("leaveButton").objectReferenceValue is Button leaveButton)
            {
                Button pauseOpen = EditorUiFactory.CreateButton(leaveButton.transform.parent, "PlayersButton", "room.players.open", EditorUiFactory.ButtonColor);
                EditorUiFactory.SetHeight(pauseOpen, 60f);
                pauseOpen.transform.SetSiblingIndex(leaveButton.transform.GetSiblingIndex());
                openButtons.Add(pauseOpen);
            }
            RoomCodeView roomCode = canvas.GetComponent<RoomCodeView>();
            if (roomCode != null && new SerializedObject(roomCode).FindProperty("inviteButton").objectReferenceValue is Button inviteButton)
            {
                Button roomOpen = EditorUiFactory.CreateButton(inviteButton.transform.parent, "PlayersButton", "room.players.open", EditorUiFactory.ButtonColor);
                EditorUiFactory.SetFlexibleWidth(roomOpen);
                openButtons.Add(roomOpen);
            }

            RectTransform dim = EditorUiFactory.CreatePanel(canvas.transform, "PlayerPanel", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.65f));
            RectTransform box = EditorUiFactory.CreatePanel(dim, "Box", new Vector2(0.33f, 0.2f), new Vector2(0.67f, 0.8f), EditorUiFactory.PanelColor);
            EditorUiFactory.AddVerticalLayout(box, 12f, 28);
            Text title = EditorUiFactory.CreateText(box, "Title", "room.players.title", 32, TextAnchor.MiddleCenter, EditorUiFactory.AccentColor);
            title.fontStyle = FontStyle.Bold;
            EditorUiFactory.SetHeight(title, 48f);
            Text hint = EditorUiFactory.CreateText(box, "Hint", "room.players.hint", 20, TextAnchor.MiddleCenter, HintColor);
            EditorUiFactory.SetHeight(hint, 56f);

            RectTransform listContent = EditorUiFactory.CreateScrollList(box, "PlayerList", 6f);
            EditorUiFactory.FillRemainingHeight(listContent, 220f);
            Button rowTemplate = EditorUiFactory.CreateButton(listContent, "PlayerRowTemplate", string.Empty, EditorUiFactory.ButtonColor, out Text rowLabel);
            rowLabel.fontSize = 22;
            EditorUiFactory.SetHeight(rowTemplate, 56f);
            rowTemplate.gameObject.SetActive(false);

            Button close = EditorUiFactory.CreateButton(box, "CloseButton", "room.players.close", EditorUiFactory.AccentColor);
            EditorUiFactory.SetHeight(close, 56f);

            RectTransform confirm = EditorUiFactory.CreatePanel(dim, "KickConfirm", new Vector2(0.36f, 0.38f), new Vector2(0.64f, 0.62f), EditorUiFactory.PanelColor);
            EditorUiFactory.AddVerticalLayout(confirm, 12f, 24);
            Text confirmText = EditorUiFactory.CreateText(confirm, "Text", string.Empty, 24, TextAnchor.MiddleCenter, EditorUiFactory.TextColor);
            EditorUiFactory.SetHeight(confirmText, 110f);
            RectTransform confirmButtons = EditorUiFactory.CreateRow(confirm, "Buttons", 52f, 12f);
            Button no = EditorUiFactory.CreateButton(confirmButtons, "NoButton", "room.players.confirmNo", EditorUiFactory.ButtonColor);
            Button yes = EditorUiFactory.CreateButton(confirmButtons, "YesButton", "room.players.confirmYes", EditorUiFactory.AccentColor);
            EditorUiFactory.SetFlexibleWidth(no);
            EditorUiFactory.SetFlexibleWidth(yes);
            confirm.gameObject.SetActive(false);

            dim.SetAsLastSibling();
            dim.gameObject.SetActive(false);

            HostPlayerPanel panel = canvas.AddComponent<HostPlayerPanel>();
            TestEnvironmentSetupTool.SetArray(panel, "openButtons", openButtons.ToArray());
            TestEnvironmentSetupTool.SetField(panel, "panel", dim.gameObject);
            TestEnvironmentSetupTool.SetField(panel, "listContent", listContent);
            TestEnvironmentSetupTool.SetField(panel, "rowTemplate", rowTemplate);
            TestEnvironmentSetupTool.SetField(panel, "closeButton", close);
            TestEnvironmentSetupTool.SetField(panel, "confirmPanel", confirm.gameObject);
            TestEnvironmentSetupTool.SetField(panel, "confirmText", confirmText);
            TestEnvironmentSetupTool.SetField(panel, "confirmYesButton", yes);
            TestEnvironmentSetupTool.SetField(panel, "confirmNoButton", no);
            return 1;
        }

        /// <summary>사용자가 옮긴 요소는 그대로 두고 스팀 방 만들기 화면에 방 이름 입력칸과 공개방 토글을 더한다.</summary>
        /// <returns>추가했는지 여부(이미 있으면 false).</returns>
        private static bool AddSteamRoomOptions(GameObject canvas)
        {
            LobbyCreateMenu menu = canvas.GetComponent<LobbyCreateMenu>();
            var serialized = new SerializedObject(menu);
            if (serialized.FindProperty("roomNameInput").objectReferenceValue != null
                || !(serialized.FindProperty("hostPasswordInput").objectReferenceValue is InputField password))
                return false;

            Transform screen = password.transform.parent;
            MoveIfAtDefault(screen.Find("PasswordHint"), 130f, 100f);
            MoveIfAtDefault(screen.Find("CreateButton"), -60f, -90f);

            InputField roomName = CreateInput(screen, "RoomNameInput", "lobby.roomName.placeholder", 160f);
            Toggle publicToggle = EditorUiFactory.CreateToggle(screen, "PublicRoomToggle", "lobby.steamCreate.public", true);
            EditorUiFactory.Place(publicToggle, Center, new Vector2(0f, -20f), new Vector2(480f, 40f));

            TestEnvironmentSetupTool.SetField(menu, "roomNameInput", roomName);
            TestEnvironmentSetupTool.SetField(menu, "publicRoomToggle", publicToggle);
            return true;
        }

        /// <summary>
        /// 사용자가 직접 옮긴 배치를 덮어쓰지 않도록, 요소가 기본 세로 위치에 그대로 있을 때만 옮긴다.
        /// </summary>
        private static void MoveIfAtDefault(Transform target, float defaultY, float newY)
        {
            if (target is RectTransform rect && Mathf.Approximately(rect.anchoredPosition.y, defaultY))
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, newY);
        }

        /// <summary>방 코드를 모르는 사람도 함께 할 사람을 찾도록 공개방 찾기 화면을 만든다.</summary>
        private static void CreateBrowseScreen(GameObject canvas)
        {
            LobbyJoinMenu joinMenu = canvas.GetComponent<LobbyJoinMenu>();
            var serializedJoin = new SerializedObject(joinMenu);
            Transform joinScreen = (serializedJoin.FindProperty("steamJoinMenuButton").objectReferenceValue as Button)?.transform.parent;
            Button openButton = CreateMenuButton(joinScreen != null ? joinScreen : canvas.transform, "BrowseRoomsButton", "lobby.browse.open", 175f, EditorUiFactory.AccentColor);

            RectTransform screen = CreateScreen(canvas.transform, "BrowsePanel", null);
            EditorUiFactory.Place(screen, Center, new Vector2(0f, -30f), new Vector2(1000f, 620f));
            Text header = EditorUiFactory.CreateText(screen, "Header", "lobby.browse.title", 36, TextAnchor.MiddleCenter, EditorUiFactory.AccentColor);
            header.fontStyle = FontStyle.Bold;
            EditorUiFactory.Place(header, Center, new Vector2(0f, 265f), new Vector2(640f, 60f));

            Toggle hidePassword = EditorUiFactory.CreateToggle(screen, "HidePasswordToggle", "lobby.browse.hidePassword", false);
            EditorUiFactory.Place(hidePassword, Center, new Vector2(-240f, 205f), new Vector2(440f, 40f));
            Toggle hideMissingMap = EditorUiFactory.CreateToggle(screen, "HideMissingMapToggle", "lobby.browse.hideMissingMap", false);
            EditorUiFactory.Place(hideMissingMap, Center, new Vector2(240f, 205f), new Vector2(440f, 40f));

            RectTransform listContent = EditorUiFactory.CreateScrollList(screen, "RoomList", 6f);
            EditorUiFactory.Place(listContent.parent.parent, Center, new Vector2(0f, 10f), new Vector2(940f, 330f));
            Button rowTemplate = EditorUiFactory.CreateButton(listContent, "RoomRowTemplate", string.Empty, EditorUiFactory.ButtonColor, out Text rowLabel);
            rowLabel.alignment = TextAnchor.MiddleLeft;
            rowLabel.fontSize = 22;
            EditorUiFactory.StretchWithPadding(rowLabel.rectTransform, 14f);
            EditorUiFactory.SetHeight(rowTemplate, 56f);
            rowTemplate.gameObject.SetActive(false);

            Text status = EditorUiFactory.CreateText(screen, "Status", string.Empty, 20, TextAnchor.MiddleCenter, HintColor);
            EditorUiFactory.Place(status, Center, new Vector2(0f, -185f), new Vector2(940f, 40f));

            Button refreshButton = EditorUiFactory.CreateButton(screen, "RefreshButton", "lobby.browse.refresh", EditorUiFactory.ButtonColor);
            EditorUiFactory.Place(refreshButton, Center, new Vector2(-320f, -255f), new Vector2(290f, 64f));
            Button quickJoinButton = EditorUiFactory.CreateButton(screen, "QuickJoinButton", "lobby.browse.quickJoin", EditorUiFactory.AccentColor);
            EditorUiFactory.Place(quickJoinButton, Center, new Vector2(0f, -255f), new Vector2(290f, 64f));
            Button backButton = EditorUiFactory.CreateButton(screen, "BackButton", "lobby.back", EditorUiFactory.ButtonColor);
            EditorUiFactory.Place(backButton, Center, new Vector2(320f, -255f), new Vector2(290f, 64f));

            PlaceBeforeOverlayPanels(canvas, screen);
            screen.gameObject.SetActive(false);
            RegisterLobbyScreen(canvas.GetComponent<LobbyView>(), LobbyScreen.Browse, screen.gameObject, LobbyScreen.Join, backButton);

            LobbyRoomBrowserView view = canvas.AddComponent<LobbyRoomBrowserView>();
            TestEnvironmentSetupTool.SetField(view, "openButton", openButton);
            TestEnvironmentSetupTool.SetField(view, "listContent", listContent);
            TestEnvironmentSetupTool.SetField(view, "rowTemplate", rowTemplate);
            TestEnvironmentSetupTool.SetField(view, "refreshButton", refreshButton);
            TestEnvironmentSetupTool.SetField(view, "quickJoinButton", quickJoinButton);
            TestEnvironmentSetupTool.SetField(view, "hidePasswordToggle", hidePassword);
            TestEnvironmentSetupTool.SetField(view, "hideMissingMapToggle", hideMissingMap);
            TestEnvironmentSetupTool.SetField(view, "statusText", status);
        }

        /// <summary>
        /// 스크립트가 지워진 컴포넌트가 남으면 "Missing Script" 경고가 계속 나므로, 없어진 입장 거부 경고 창의 오브젝트와 빈 컴포넌트를 없앤다.
        /// </summary>
        /// <returns>프리팹을 저장할지 정하도록 지운 것이 있으면 1, 없으면 0.</returns>
        private static int RemoveOldModMismatchPopup(GameObject canvas)
        {
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(canvas);
            Transform oldPopup = canvas.transform.Find("ModMismatchPopup");
            if (oldPopup != null)
            {
                UnityEngine.Object.DestroyImmediate(oldPopup.gameObject);
                removed++;
            }
            return removed > 0 ? 1 : 0;
        }

        /// <summary>
        /// 손으로 다듬은 룸 프리팹을 초기화하지 않고 새 기능을 쓸 수 있도록 없는 영역(모드 설치 안내 창)만 만들어 붙인다.
        /// </summary>
        /// <returns>새로 추가한 영역 수.</returns>
        public static int AddMissingRoomSections(GameObject canvas)
        {
            if (canvas.GetComponent<RoomModRequiredPopup>() != null || canvas.GetComponent<RoomFooterView>() == null)
                return 0;

            CreateRoomModRequiredPopup(canvas);
            return 1;
        }

        /// <summary>모드가 없는 게스트가 직접 설치할 수 있도록 다른 화면 위에 설치 안내 창을 만든다.</summary>
        private static void CreateRoomModRequiredPopup(GameObject canvas)
        {
            Button openButton = EditorUiFactory.CreateButton(canvas.transform, "ModRequiredButton", "room.modRequired.openButton", EditorUiFactory.AccentColor);
            EditorUiFactory.Place(openButton, new Vector2(0.5f, 0.975f), Vector2.zero, new Vector2(560f, 44f));

            RectTransform dim = EditorUiFactory.CreatePanel(canvas.transform, "ModRequiredPopup", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.65f));
            RectTransform box = EditorUiFactory.CreatePanel(dim, "Box", new Vector2(0.25f, 0.18f), new Vector2(0.75f, 0.82f), EditorUiFactory.PanelColor);
            EditorUiFactory.AddVerticalLayout(box, 12f, 28);

            Text title = EditorUiFactory.CreateText(box, "Title", "room.modRequired.title", 34, TextAnchor.MiddleCenter, EditorUiFactory.NoticeColor);
            title.fontStyle = FontStyle.Bold;
            EditorUiFactory.SetHeight(title, 48f);
            Text warning = EditorUiFactory.CreateText(box, "Warning", "room.modRequired.warning", 22, TextAnchor.MiddleCenter, EditorUiFactory.TextColor);
            EditorUiFactory.SetHeight(warning, 100f);

            // 모드가 많아도 넘치지 않도록 스크롤한다.
            RectTransform listContent = EditorUiFactory.CreateScrollList(box, "MissingList", 6f);
            EditorUiFactory.FillRemainingHeight(listContent, 200f);
            Button rowTemplate = EditorUiFactory.CreateButton(listContent, "ModRowTemplate", string.Empty, EditorUiFactory.ButtonColor, out Text rowLabel);
            rowLabel.alignment = TextAnchor.MiddleLeft;
            rowLabel.fontSize = 20;
            EditorUiFactory.StretchWithPadding(rowLabel.rectTransform, 14f);
            EditorUiFactory.SetHeight(rowTemplate, 72f);
            rowTemplate.gameObject.SetActive(false);

            RectTransform buttons = EditorUiFactory.CreateRow(box, "Buttons", 56f, 12f);
            Button recheck = EditorUiFactory.CreateButton(buttons, "RecheckButton", "room.modRequired.recheck", EditorUiFactory.ButtonColor);
            Button close = EditorUiFactory.CreateButton(buttons, "CloseButton", "room.modRequired.close", EditorUiFactory.AccentColor);
            EditorUiFactory.SetFlexibleWidth(recheck);
            EditorUiFactory.SetFlexibleWidth(close);

            // 링크 열기 확인 창: 전체 주소를 보여 주고 한 번 더 묻는다.
            RectTransform confirm = EditorUiFactory.CreatePanel(dim, "LinkConfirm", new Vector2(0.3f, 0.32f), new Vector2(0.7f, 0.68f), EditorUiFactory.PanelColor);
            EditorUiFactory.AddVerticalLayout(confirm, 12f, 24);
            Text confirmTitle = EditorUiFactory.CreateText(confirm, "Title", "room.modRequired.confirm.title", 28, TextAnchor.MiddleCenter, EditorUiFactory.NoticeColor);
            EditorUiFactory.SetHeight(confirmTitle, 40f);
            Text confirmWarning = EditorUiFactory.CreateText(confirm, "Warning", "room.modRequired.confirm.warning", 20, TextAnchor.MiddleCenter, EditorUiFactory.TextColor);
            EditorUiFactory.SetHeight(confirmWarning, 76f);
            Text confirmUrl = EditorUiFactory.CreateText(confirm, "Url", string.Empty, 20, TextAnchor.MiddleCenter, EditorUiFactory.AccentColor);
            EditorUiFactory.SetHeight(confirmUrl, 76f);
            RectTransform confirmButtons = EditorUiFactory.CreateRow(confirm, "Buttons", 52f, 12f);
            Button confirmCancel = EditorUiFactory.CreateButton(confirmButtons, "CancelButton", "room.modRequired.confirm.cancel", EditorUiFactory.ButtonColor);
            Button confirmOpen = EditorUiFactory.CreateButton(confirmButtons, "OpenButton", "room.modRequired.confirm.open", EditorUiFactory.AccentColor);
            EditorUiFactory.SetFlexibleWidth(confirmCancel);
            EditorUiFactory.SetFlexibleWidth(confirmOpen);
            confirm.gameObject.SetActive(false);

            dim.SetAsLastSibling();
            dim.gameObject.SetActive(false);

            RoomModRequiredPopup popup = canvas.AddComponent<RoomModRequiredPopup>();
            TestEnvironmentSetupTool.SetField(popup, "popupPanel", dim.gameObject);
            TestEnvironmentSetupTool.SetField(popup, "openButton", openButton);
            TestEnvironmentSetupTool.SetField(popup, "listContent", listContent);
            TestEnvironmentSetupTool.SetField(popup, "rowTemplate", rowTemplate);
            TestEnvironmentSetupTool.SetField(popup, "recheckButton", recheck);
            TestEnvironmentSetupTool.SetField(popup, "closeButton", close);
            TestEnvironmentSetupTool.SetField(popup, "confirmPanel", confirm.gameObject);
            TestEnvironmentSetupTool.SetField(popup, "confirmUrlText", confirmUrl);
            TestEnvironmentSetupTool.SetField(popup, "confirmOpenButton", confirmOpen);
            TestEnvironmentSetupTool.SetField(popup, "confirmCancelButton", confirmCancel);
        }

        /// <summary>게임을 끄지 않고 모드를 켜고 끄거나 다시 읽을 수 있도록 "모드" 화면을 만든다.</summary>
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
        /// 씬을 다시 만들어도 손으로 다듬은 로비 UI가 사라지지 않도록 프리팹이 있으면 그대로 쓰고, 없을 때만 기본 모양으로 만들어 저장한 뒤 넣는다.
        /// </summary>
        private static void CreateLobbyUi()
        {
            GameObject prefab = UiPrefabLibrary.GetOrBuildRoot(UiPrefabLibrary.LobbyCanvasPath, BuildLobbyCanvas);
            UiPrefabLibrary.Instantiate(prefab);
        }

        // 프리팹에서 자유롭게 옮길 수 있도록 Layout Group 없이 쓰는 로비 화면 배치(1920×1080 기준).
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 ScreenPosition = new Vector2(0f, -40f);
        private static readonly Vector2 ScreenSize = new Vector2(720f, 560f);
        private static readonly Vector2 ButtonSize = new Vector2(480f, 70f);
        private static readonly Vector2 InputSize = new Vector2(480f, 64f);
        private static readonly Color HintColor = new Color(0.7f, 0.7f, 0.7f);

        /// <summary>화면 전환 때마다 로딩이 생기지 않도록 화면들을 한 캔버스에 겹쳐 두고 하나씩 보여 준다.</summary>
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
            // 화면 목록(첫 항목이 시작 화면).
            SetLobbyScreens(view,
                (LobbyScreen.Main, main.gameObject, LobbyScreen.Main),
                (LobbyScreen.Create, createMenu.gameObject, LobbyScreen.Main),
                (LobbyScreen.SteamCreate, steamCreate.gameObject, LobbyScreen.Create),
                (LobbyScreen.Join, joinMenu.gameObject, LobbyScreen.Main),
                (LobbyScreen.SteamJoin, steamJoin.gameObject, LobbyScreen.Join),
                (LobbyScreen.DirectJoin, directJoin.gameObject, LobbyScreen.Join));

            // 화면이 꺼져 있어도 이벤트를 받도록 버튼 코드는 모두 캔버스에 붙인다.
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

            // 버전 표시와 "모드" 화면.
            AddMissingLobbySections(canvas.gameObject);

            // 메뉴 위에 그려지도록 설정과 크레딧 화면은 마지막에 만든다.
            TestEnvironmentSetupTool.SetField(view, "settingsPanel", SharedUiBuilder.CreateSettingsPanel(canvas.transform));
            TestEnvironmentSetupTool.SetField(view, "creditsPanel", SharedUiBuilder.CreateCreditsPanel(canvas.transform));
            return canvas.gameObject;
        }

        /// <summary>화면을 추가해도 LobbyView를 고치지 않도록 화면 이동 규칙을 목록 데이터로 채운다.</summary>
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

        /// <summary>전환할 때 튀지 않도록 로비 화면들을 같은 크기·위치의 틀로 만든다.</summary>
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

        /// <summary>로비 버튼이 같은 크기·정렬이 되면서 에디터에서 옮길 수 있도록 레이아웃 없이 고정 배치한다.</summary>
        private static Button CreateMenuButton(Transform screen, string name, string labelKey, float y, Color color)
        {
            Button button = EditorUiFactory.CreateButton(screen, name, labelKey, color);
            EditorUiFactory.Place(button, Center, new Vector2(0f, y), ButtonSize);
            return button;
        }

        /// <summary>
        /// 입력 칸이 버튼과 같은 폭으로 정렬되도록 가운데 정렬로 세로 위치(y)만 받아 만든다.
        /// </summary>
        private static InputField CreateInput(Transform screen, string name, string placeholderKey, float y)
        {
            InputField input = EditorUiFactory.CreateInputField(screen, name, placeholderKey);
            EditorUiFactory.Place(input, Center, new Vector2(0f, y), InputSize);
            return input;
        }

        /// <summary>
        /// 입력 칸 옆에 무엇을 넣는지 알려 주는 문구가 버튼·입력 칸과 같은 정렬이 되도록 세로 위치(y)만 받아 만든다.
        /// </summary>
        private static void CreateHint(Transform screen, string name, string key, float y)
        {
            Text hint = EditorUiFactory.CreateText(screen, name, key, 20, TextAnchor.MiddleCenter, HintColor);
            EditorUiFactory.Place(hint, Center, new Vector2(0f, y), new Vector2(640f, 70f));
        }

        #endregion

        #region 룸 UI

        /// <summary>
        /// 씬을 다시 만들어도 손으로 다듬은 룸 UI가 사라지지 않도록 프리팹이 있으면 그대로 쓰고, 없을 때만 기본 모양으로 만들어 저장한 뒤 넣는다.
        /// </summary>
        private static void CreateRoomUi()
        {
            GameObject prefab = UiPrefabLibrary.GetOrBuildRoot(UiPrefabLibrary.RoomCanvasPath, BuildRoomCanvas);
            UiPrefabLibrary.Instantiate(prefab);
        }

        /// <summary>대기 중에 참가자와 판 설정을 한 화면에서 보도록 룸 화면을 만들고, 설정 줄은 game_settings.csv 항목 수만큼 만든다.</summary>
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
            // 상황에 따라 문구가 바뀌어 RoomFooterView가 채우므로 key를 넘기지 않는다.
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
            AddMissingRoomSections(canvas.gameObject);
            AddMissingPlayerPanel(canvas.gameObject);
            return canvas.gameObject;
        }

        /// <summary>손으로 다듬은 모양을 지키려고 초기화하지 않고 빠진 설정 줄만 더한다.</summary>
        /// <param name="owner">settingRows 필드를 가진 컴포넌트.</param>
        /// <param name="include">이 화면에 보여줄 항목인지.</param>
        /// <returns>추가한 줄 수(대상이 없으면 -1).</returns>
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

        /// <summary>룸과 테스트 메뉴가 같은 설정 줄을 쓰도록 생성을 한곳에 두고, 숫자는 키보드로 입력할 수 있게 한다.</summary>
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
        /// 룸 화면 구역(참가자·설정)이 같은 제목 모양이 되도록 한곳에서 만든다.
        /// </summary>
        private static void AddHeader(Transform parent, string content)
        {
            Text header = EditorUiFactory.CreateText(parent, "Header", content, 30, TextAnchor.MiddleLeft, EditorUiFactory.AccentColor);
            header.fontStyle = FontStyle.Bold;
            EditorUiFactory.SetHeight(header, 40f);
        }

        /// <summary>
        /// 세로 레이아웃 안에서 문구가 차지할 높이를 글자 크기에 맞춰 고정해야 줄이 겹치지 않으므로 높이까지 정해 추가한다.
        /// </summary>
        private static Text AddLine(Transform parent, string name, string content, int fontSize, Color color)
        {
            Text text = EditorUiFactory.CreateText(parent, name, content, fontSize, TextAnchor.MiddleLeft, color);
            EditorUiFactory.SetHeight(text, fontSize + 14f);
            return text;
        }
    }
}
