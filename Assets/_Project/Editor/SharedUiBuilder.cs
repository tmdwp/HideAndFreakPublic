using System.Linq;
using GuardVsMannequin.Settings;
using GuardVsMannequin.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GuardVsMannequin.Editor
{
    /// <summary>여러 씬이 같은 화면을 쓰도록 설정·크레딧·ESC 메뉴·게임 HUD를 한곳에서 만든다.</summary>
    public static class SharedUiBuilder
    {
        public const string InputActionsPath = "Assets/_Project/Input/GameInput.inputactions";

        // 발각 아이콘·손전등 표시가 기준으로 삼는 하단 중앙 체력·스태미나 앵커.
        private static readonly Vector2 HealthAnchorMin = new(0.4f, 0.064f);
        private static readonly Vector2 HealthAnchorMax = new(0.6f, 0.1f);
        private static readonly Vector2 StaminaAnchorMin = new(0.4f, 0.04f);
        private static readonly Vector2 StaminaAnchorMax = new(0.6f, 0.056f);
        private static readonly Vector2 FlashlightKeySize = new(34f, 30f);
        private const float FlashlightIconSize = 44f;
        private const float FlashlightIconGap = 6f;
        private static readonly Vector2 CrosshairGaugeSize = new(64f, 64f);

        /// <summary>한 번 고치면 모든 화면에 반영되도록 설정 화면을 중첩 프리팹으로 넣는다.</summary>
        public static SettingsPanelView CreateSettingsPanel(Transform canvas)
        {
            GameObject prefab = UiPrefabLibrary.GetOrBuildPanel(UiPrefabLibrary.SettingsPanelPath, parent => BuildSettingsPanel(parent).gameObject);
            return UiPrefabLibrary.Instantiate(prefab, canvas).GetComponent<SettingsPanelView>();
        }

        /// <summary>한 번 고치면 모든 화면에 반영되도록 크레딧 화면을 중첩 프리팹으로 넣는다.</summary>
        public static CreditsView CreateCreditsPanel(Transform canvas)
        {
            GameObject prefab = UiPrefabLibrary.GetOrBuildPanel(UiPrefabLibrary.CreditsPanelPath, parent => BuildCreditsPanel(parent).gameObject);
            return UiPrefabLibrary.Instantiate(prefab, canvas).GetComponent<CreditsView>();
        }

        /// <summary>설정 항목이 많아 찾기 어렵지 않도록 탭으로 페이지를 나눈 기본 모양을 만든다.</summary>
        private static SettingsPanelView BuildSettingsPanel(Transform canvas)
        {
            RectTransform panel = EditorUiFactory.CreatePanel(canvas, "SettingsPanel", SettingsPanelLayout.PanelAnchorMin, SettingsPanelLayout.PanelAnchorMax, new Color(0.06f, 0.07f, 0.09f, 0.98f));
            EditorUiFactory.AddVerticalLayout(panel, 10f, 28);

            Text title = EditorUiFactory.CreateText(panel, "Title", "options.title", 36, TextAnchor.MiddleLeft, EditorUiFactory.AccentColor);
            title.fontStyle = FontStyle.Bold;
            EditorUiFactory.SetHeight(title, 48f);

            // 탭 버튼 (순서 = 페이지 순서)
            RectTransform tabs = EditorUiFactory.CreateRow(panel, "Tabs", 50f, 8f);
            string[] tabKeys = { "options.tab.graphics", "options.tab.sound", "options.tab.controls" };
            Button[] tabButtons = tabKeys.Select(key => EditorUiFactory.CreateButton(tabs, key.Substring(key.LastIndexOf('.') + 1) + "Tab", key, EditorUiFactory.ButtonColor)).ToArray();
            foreach (Button tab in tabButtons)
                EditorUiFactory.SetFlexibleWidth(tab);

            // 페이지를 겹쳐 두고 하나만 켠다.
            RectTransform pagesRoot = EditorUiFactory.CreateRect(panel, "Pages");
            LayoutElement pagesLayout = pagesRoot.gameObject.AddComponent<LayoutElement>();
            pagesLayout.flexibleHeight = 1f;
            pagesLayout.minHeight = 300f;
            RectTransform graphicsPage = CreatePage(pagesRoot, "GraphicsPage");
            RectTransform soundPage = CreatePage(pagesRoot, "SoundPage");
            RectTransform controlsPage = CreatePage(pagesRoot, "ControlsPage");

            BuildGraphicsPage(graphicsPage);
            BuildSoundPage(soundPage);
            BuildControlsPage(controlsPage);

            RectTransform buttons = EditorUiFactory.CreateRow(panel, "Buttons", 54f, 12f);
            Button resetAll = EditorUiFactory.CreateButton(buttons, "ResetAllButton", "options.resetAll", EditorUiFactory.ButtonColor);
            Button close = EditorUiFactory.CreateButton(buttons, "CloseButton", "options.close", EditorUiFactory.AccentColor);
            EditorUiFactory.SetFlexibleWidth(resetAll);
            EditorUiFactory.SetFlexibleWidth(close);

            // 각 설정은 페이지의 설정 묶음이 처리하므로 틀만 연결한다.
            SettingsPanelView view = panel.gameObject.AddComponent<SettingsPanelView>();
            TestEnvironmentSetupTool.SetArray(view, "tabButtons", tabButtons);
            TestEnvironmentSetupTool.SetArray(view, "pages", new Object[] { graphicsPage.gameObject, soundPage.gameObject, controlsPage.gameObject });
            TestEnvironmentSetupTool.SetField(view, "resetAllButton", resetAll);
            TestEnvironmentSetupTool.SetField(view, "closeButton", close);
            // 항목이 늘어도 페이지 밖으로 넘치지 않게 끌거나 휠로 내려 보도록 한다.
            SettingsPanelLayout.MakePagesScrollable(panel.gameObject);
            SettingsPanelLayout.SeparateBackground(panel.gameObject);

            panel.gameObject.SetActive(false);
            return view;
        }

        /// <summary>선택지가 적은 항목은 ◀ ▶로 빠르게 넘기고, 선택지가 많은 해상도만 스크롤 목록으로 만든다.</summary>
        private static void BuildGraphicsPage(RectTransform page)
        {
            CreateSelectorRow(page, "ScreenMode", "options.screenMode", out Button previousMode, out Text modeValue, out Button nextMode);

            RectTransform resolutionRow = EditorUiFactory.CreateRow(page, "ResolutionRow", 44f, 16f);
            Text resolutionLabel = EditorUiFactory.CreateText(resolutionRow, "Label", "options.resolution", 24, TextAnchor.MiddleLeft, EditorUiFactory.TextColor);
            EditorUiFactory.SetWidth(resolutionLabel, 200f);
            Dropdown resolution = EditorUiFactory.CreateDropdown(resolutionRow, "ResolutionDropdown", 24, 320f);
            EditorUiFactory.SetFlexibleWidth(resolution);

            CreateFrameLimitRow(page);
            CreateCameraSwayRow(page);
            CreateOptionRow<FieldOfViewSettingsSection>(page, "FieldOfView", "options.fov");
            CreateStatsDisplayRow(page);
            CreateOptionRow<CrosshairColorSettingsSection>(page, "CrosshairColor", "options.crosshairColor");
            CreateOptionRow<ControlsHintSettingsSection>(page, "ControlsHint", "options.controlsHint");

            // 언어: strings.csv에 열을 추가하면 목록에 나타남
            CreateSelectorRow(page, "Language", "options.language", out Button previousLanguage, out Text languageValue, out Button nextLanguage);

            DisplaySettingsSection display = page.gameObject.AddComponent<DisplaySettingsSection>();
            TestEnvironmentSetupTool.SetField(display, "previousScreenModeButton", previousMode);
            TestEnvironmentSetupTool.SetField(display, "nextScreenModeButton", nextMode);
            TestEnvironmentSetupTool.SetField(display, "screenModeValueText", modeValue);
            TestEnvironmentSetupTool.SetField(display, "resolutionDropdown", resolution);

            LanguageSettingsSection language = page.gameObject.AddComponent<LanguageSettingsSection>();
            TestEnvironmentSetupTool.SetField(language, "previousLanguageButton", previousLanguage);
            TestEnvironmentSetupTool.SetField(language, "nextLanguageButton", nextLanguage);
            TestEnvironmentSetupTool.SetField(language, "languageValueText", languageValue);
        }

        /// <summary>배경 음악만 줄이고 싶은 경우가 많아 전체·배경 음악·효과음 슬라이더를 나눈다.</summary>
        private static void BuildSoundPage(RectTransform page)
        {
            Slider master = CreateSliderRow(page, "MasterVolume", "options.masterVolume", out Text masterValue);
            Slider bgm = CreateSliderRow(page, "BgmVolume", "options.bgmVolume", out Text bgmValue);
            Slider sfx = CreateSliderRow(page, "SfxVolume", "options.sfxVolume", out Text sfxValue);

            SoundSettingsSection sound = page.gameObject.AddComponent<SoundSettingsSection>();
            TestEnvironmentSetupTool.SetField(sound, "masterVolumeSlider", master);
            TestEnvironmentSetupTool.SetField(sound, "masterVolumeValueText", masterValue);
            TestEnvironmentSetupTool.SetField(sound, "bgmVolumeSlider", bgm);
            TestEnvironmentSetupTool.SetField(sound, "bgmVolumeValueText", bgmValue);
            TestEnvironmentSetupTool.SetField(sound, "sfxVolumeSlider", sfx);
            TestEnvironmentSetupTool.SetField(sound, "sfxVolumeValueText", sfxValue);
        }

        /// <summary>
        /// 키 항목이 늘어나도 화면 밖으로 넘치지 않도록 키 설정 목록은 스크롤로 만들고, 자주 바꾸는 마우스 감도는 위에 둔다.
        /// </summary>
        private static void BuildControlsPage(RectTransform page)
        {
            Slider sensitivity = CreateSliderRow(page, "Sensitivity", "options.sensitivity", out Text sensitivityValue);
            CreateOptionRow<InvertLookSettingsSection>(page, "InvertLook", "options.invertLook");
            Text keyHeader = EditorUiFactory.CreateText(page, "KeyHeader", "options.keyHeader", 24, TextAnchor.MiddleLeft, EditorUiFactory.TextColor);
            EditorUiFactory.SetHeight(keyHeader, 36f);

            RectTransform list = EditorUiFactory.CreateScrollList(page, "KeyList", 6f);
            EditorUiFactory.FillRemainingHeight(list, 200f);
            RebindRowView[] rows = KeyBindingCatalog.All.Select(entry => CreateRebindRow(list, entry)).ToArray();

            ControlSettingsSection controls = page.gameObject.AddComponent<ControlSettingsSection>();
            TestEnvironmentSetupTool.SetField(controls, "inputActions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath));
            TestEnvironmentSetupTool.SetField(controls, "sensitivitySlider", sensitivity);
            TestEnvironmentSetupTool.SetField(controls, "sensitivityValueText", sensitivityValue);
            TestEnvironmentSetupTool.SetArray(controls, "rebindRows", rows);
        }

        /// <summary>크레딧 내용은 빌드 후에도 고칠 수 있게 파일에서 읽으므로 스크롤 텍스트 틀만 만든다.</summary>
        private static CreditsView BuildCreditsPanel(Transform canvas)
        {
            RectTransform panel = EditorUiFactory.CreatePanel(canvas, "CreditsPanel", new Vector2(0.25f, 0.1f), new Vector2(0.75f, 0.9f), new Color(0.06f, 0.07f, 0.09f, 0.98f));
            EditorUiFactory.AddVerticalLayout(panel, 12f, 32);

            Text title = EditorUiFactory.CreateText(panel, "Title", "credits.title", 36, TextAnchor.MiddleCenter, EditorUiFactory.AccentColor);
            title.fontStyle = FontStyle.Bold;
            EditorUiFactory.SetHeight(title, 48f);

            // 에셋이 늘어도 넘치지 않도록 스크롤 영역에 둔다.
            Text body = EditorUiFactory.CreateScrollText(panel, "Body", string.Empty, 24, TextAnchor.UpperCenter, EditorUiFactory.TextColor);
            EditorUiFactory.FillRemainingHeight(body.transform.parent, 200f);

            // 전환 버튼 문구는 CreditsView가 채우므로 key를 넘기지 않는다.
            RectTransform buttons = EditorUiFactory.CreateRow(panel, "Buttons", 54f, 12f);
            Button page = EditorUiFactory.CreateButton(buttons, "PageButton", "-", EditorUiFactory.ButtonColor, out Text pageLabel);
            Button close = EditorUiFactory.CreateButton(buttons, "CloseButton", "credits.close", EditorUiFactory.AccentColor);
            EditorUiFactory.SetFlexibleWidth(page);
            EditorUiFactory.SetFlexibleWidth(close);

            CreditsView view = panel.gameObject.AddComponent<CreditsView>();
            TestEnvironmentSetupTool.SetField(view, "bodyText", body);
            TestEnvironmentSetupTool.SetField(view, "closeButton", close);
            TestEnvironmentSetupTool.SetField(view, "pageButton", page);
            TestEnvironmentSetupTool.SetField(view, "pageButtonLabel", pageLabel);

            panel.gameObject.SetActive(false);
            return view;
        }

        /// <summary>룸과 게임 중 모두 같은 ESC 메뉴를 쓰도록 한곳에서 만든다.</summary>
        /// <param name="canvas">메뉴를 올릴 캔버스.</param>
        /// <param name="lockCursorOnClose">메뉴를 닫을 때 커서를 다시 잠글지 여부.</param>
        public static PauseMenuView CreatePauseMenu(Transform canvas, bool lockCursorOnClose)
        {
            RectTransform menu = EditorUiFactory.CreatePanel(canvas, "PauseMenu", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.6f));

            RectTransform box = EditorUiFactory.CreatePanel(menu, "Box", new Vector2(0.38f, 0.3f), new Vector2(0.62f, 0.7f), EditorUiFactory.PanelColor);
            EditorUiFactory.AddVerticalLayout(box, 16f, 32);
            Text title = EditorUiFactory.CreateText(box, "Title", "pause.title", 36, TextAnchor.MiddleCenter, EditorUiFactory.AccentColor);
            title.fontStyle = FontStyle.Bold;
            EditorUiFactory.SetHeight(title, 50f);

            Button resume = EditorUiFactory.CreateButton(box, "ResumeButton", "pause.resume", EditorUiFactory.AccentColor);
            Button settings = EditorUiFactory.CreateButton(box, "SettingsButton", "pause.settings", EditorUiFactory.ButtonColor);
            Button leave = EditorUiFactory.CreateButton(box, "LeaveButton", "pause.leave", EditorUiFactory.ButtonColor);
            foreach (Button button in new[] { resume, settings, leave })
                EditorUiFactory.SetHeight(button, 60f);

            // 설정 화면은 메뉴 위에 그려지도록 나중에 만든다.
            SettingsPanelView settingsPanel = CreateSettingsPanel(canvas);

            PauseMenuView view = canvas.gameObject.AddComponent<PauseMenuView>();
            TestEnvironmentSetupTool.SetField(view, "inputActions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath));
            TestEnvironmentSetupTool.SetField(view, "menuPanel", menu.gameObject);
            TestEnvironmentSetupTool.SetField(view, "resumeButton", resume);
            TestEnvironmentSetupTool.SetField(view, "settingsButton", settings);
            TestEnvironmentSetupTool.SetField(view, "leaveButton", leave);
            TestEnvironmentSetupTool.SetField(view, "settingsPanel", settingsPanel);
            var serialized = new SerializedObject(view);
            serialized.FindProperty("lockCursorOnClose").boolValue = lockCursorOnClose;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            menu.gameObject.SetActive(false);
            MenuSceneBuilder.AddMissingPlayerPanel(canvas.gameObject);
            return view;
        }

        /// <summary>HUD 프리팹이 게임 시스템에 의존하지 않도록 게임 상태 참조는 호출한 쪽에서 연결하게 HUD 모양만 만든다.</summary>
        public static GameHud CreateGameHud(Transform canvas)
        {
            // 상단: 단계·시간·남은 마네킹.
            RectTransform top = EditorUiFactory.CreatePanel(canvas, "TopPanel", new Vector2(0.32f, 0.9f), new Vector2(0.68f, 0.985f), new Color(0f, 0f, 0f, 0.45f));
            Text topText = EditorUiFactory.CreateText(top, "TopText", string.Empty, 26, TextAnchor.MiddleCenter, EditorUiFactory.TextColor);
            EditorUiFactory.Stretch(topText.rectTransform);

            // 중앙: 조준점, 대기 문구
            Text crosshair = EditorUiFactory.CreateText(canvas, "Crosshair", "+", 34, TextAnchor.MiddleCenter, EditorUiFactory.TextColor);
            crosshair.rectTransform.sizeDelta = new Vector2(40f, 40f);
            crosshair.raycastTarget = false;
            Text waiting = EditorUiFactory.CreateText(canvas, "WaitingText", "hud.waiting", 32, TextAnchor.MiddleCenter, EditorUiFactory.TextColor);
            waiting.rectTransform.sizeDelta = new Vector2(900f, 60f);

            // 하단: 사망·미참가 시 관전 안내.
            RectTransform bottom = EditorUiFactory.CreatePanel(canvas, "BottomPanel", new Vector2(0.18f, 0.02f), new Vector2(0.82f, 0.14f), new Color(0f, 0f, 0f, 0.45f));
            EditorUiFactory.AddVerticalLayout(bottom, 4f, 10);
            Text spectator = EditorUiFactory.CreateText(bottom, "ObjectiveText", string.Empty, 22, TextAnchor.MiddleCenter, EditorUiFactory.TextColor);
            spectator.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

            // 결과 화면 아래에 그려지도록 영역들을 먼저 만든다.
            AddMissingHudSections(canvas);

            // 결과
            RectTransform result = EditorUiFactory.CreatePanel(canvas, "ResultPanel", new Vector2(0.3f, 0.2f), new Vector2(0.7f, 0.82f), EditorUiFactory.PanelColor);
            EditorUiFactory.AddVerticalLayout(result, 12f, 28);
            Text headline = EditorUiFactory.CreateText(result, "Headline", string.Empty, 34, TextAnchor.MiddleCenter, EditorUiFactory.AccentColor);
            EditorUiFactory.SetHeight(headline, 150f);
            // 참가자가 많으면 스크롤한다.
            Text participants = EditorUiFactory.CreateScrollText(result, "Participants", string.Empty, 24, TextAnchor.UpperCenter, EditorUiFactory.TextColor);
            EditorUiFactory.FillRemainingHeight(participants.transform.parent, 120f);
            Text countdown = EditorUiFactory.CreateText(result, "Countdown", string.Empty, 22, TextAnchor.MiddleCenter, EditorUiFactory.TextColor);
            EditorUiFactory.SetHeight(countdown, 32f);
            Button returnButton = EditorUiFactory.CreateButton(result, "ReturnToRoomButton", "result.returnToRoom", EditorUiFactory.AccentColor);
            EditorUiFactory.SetHeight(returnButton, 56f);

            GameHud hud = canvas.gameObject.AddComponent<GameHud>();
            TestEnvironmentSetupTool.SetField(hud, "crosshair", crosshair.gameObject);
            TestEnvironmentSetupTool.SetField(hud, "waitingText", waiting);

            // GameHud가 같은 캔버스 아래의 IHudSection을 찾아 갱신한다.
            HudTopPanel topSection = canvas.gameObject.AddComponent<HudTopPanel>();
            TestEnvironmentSetupTool.SetField(topSection, "topPanel", top.gameObject);
            TestEnvironmentSetupTool.SetField(topSection, "topText", topText);

            HudBottomPanel bottomSection = canvas.gameObject.AddComponent<HudBottomPanel>();
            TestEnvironmentSetupTool.SetField(bottomSection, "bottomPanel", bottom.gameObject);
            TestEnvironmentSetupTool.SetField(bottomSection, "spectatorText", spectator);
            TestEnvironmentSetupTool.SetField(bottomSection, "inputActions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath));

            HudResultPanel resultSection = canvas.gameObject.AddComponent<HudResultPanel>();
            TestEnvironmentSetupTool.SetField(resultSection, "resultPanel", result.gameObject);
            TestEnvironmentSetupTool.SetField(resultSection, "resultHeadlineText", headline);
            TestEnvironmentSetupTool.SetField(resultSection, "resultParticipantsText", participants);
            TestEnvironmentSetupTool.SetField(resultSection, "resultCountdownText", countdown);
            TestEnvironmentSetupTool.SetField(resultSection, "returnToRoomButton", returnButton);
            return hud;
        }

        /// <summary>기존 프리팹을 초기화하지 않고 빠진 HUD 영역만 결과 화면 아래에 더한다.</summary>
        /// <returns>새로 추가한 영역 수.</returns>
        public static int AddMissingHudSections(Transform canvas)
        {
            int added = 0;
            if (canvas.GetComponent<HudControlsPanel>() == null)
            {
                InsertBeforeOverlays(canvas, CreateControlsSection(canvas));
                added++;
            }
            if (canvas.GetComponent<HudObjectivePanel>() == null)
            {
                InsertBeforeOverlays(canvas, CreateObjectiveSection(canvas));
                added++;
            }
            if (canvas.GetComponent<HudStaminaBar>() == null)
            {
                InsertBeforeOverlays(canvas, CreateStaminaSection(canvas));
                added++;
            }
            if (canvas.GetComponent<HudHealthBar>() == null)
            {
                InsertBeforeOverlays(canvas, CreateHealthSection(canvas));
                MoveToVitalsLayout(canvas);
                added++;
            }
            if (canvas.GetComponent<HudRevealIndicator>() == null)
            {
                InsertBeforeOverlays(canvas, CreateRevealSection(canvas));
                added++;
            }
            if (canvas.GetComponent<HudSwitchToolIndicator>() == null)
            {
                InsertBeforeOverlays(canvas, CreateSwitchToolSection(canvas));
                added++;
            }
            if (canvas.GetComponent<HudCooldownBars>() == null)
            {
                InsertBeforeOverlays(canvas, CreateCooldownBarsSection(canvas));
                added++;
            }
            HudAmmoDisplay ammoSection = canvas.GetComponent<HudAmmoDisplay>();
            if (ammoSection == null || !HasAmmoGauge(ammoSection))
            {
                CreateAmmoSection(canvas, ammoSection);
                added++;
            }
            if (canvas.GetComponent<HudCooldownRing>() == null)
            {
                CreateCooldownRingSection(canvas);
                added++;
            }
            // 관전 안내가 바꾼 키를 따라가도록 입력 에셋이 비어 있으면 넣는다.
            HudBottomPanel bottomSection = canvas.GetComponent<HudBottomPanel>();
            if (bottomSection != null && new SerializedObject(bottomSection).FindProperty("inputActions").objectReferenceValue == null)
            {
                TestEnvironmentSetupTool.SetField(bottomSection, "inputActions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath));
                added++;
            }
            if (canvas.GetComponent<HudHitMarker>() == null)
            {
                CreateHitMarkerSection(canvas);
                added++;
            }
            if (canvas.GetComponent<HudEliminationFeed>() == null)
            {
                InsertBeforeOverlays(canvas, CreateEliminationFeedSection(canvas));
                added++;
            }
            if (canvas.GetComponent<HudPhaseAnnouncement>() == null)
            {
                InsertBeforeOverlays(canvas, CreatePhaseAnnouncementSection(canvas));
                added++;
            }
            // 예전 HUD의 남은 글자를 지우고 빠진 손전등 아이콘을 넣는다.
            added += RemoveOldHudDecorations(canvas);
            if (canvas.GetComponent<HudTestRoomPanel>() == null)
            {
                InsertBeforeOverlays(canvas, CreateTestRoomSection(canvas));
                added++;
            }
            else if (AddMissingTestRoomButtons(canvas.GetComponent<HudTestRoomPanel>()))
            {
                added++;
            }
            return added;
        }

        /// <summary>
        /// 손으로 다듬은 테스트 메뉴 프리팹을 초기화하지 않고도 새 기능을 쓸 수 있도록, 나중에 생긴 버튼(룸으로 돌아가기)이 없을 때만 닫기 버튼 위에 더한다.
        /// </summary>
        /// <returns>버튼을 더했는지 여부.</returns>
        private static bool AddMissingTestRoomButtons(HudTestRoomPanel section)
        {
            var serialized = new SerializedObject(section);
            SerializedProperty returnButton = serialized.FindProperty("returnToRoomButton");
            SerializedProperty closeButton = serialized.FindProperty("closeButton");
            if (returnButton.objectReferenceValue != null || !(closeButton.objectReferenceValue is Button close))
                return false;

            returnButton.objectReferenceValue = CreateTestReturnButton(close.transform.parent, close.transform);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>새로 만들 때와 기존 프리팹에 더할 때 같은 모양이 되도록 버튼 생성을 한곳에 둔다.</summary>
        private static Button CreateTestReturnButton(Transform panel, Transform closeButton)
        {
            Button button = EditorUiFactory.CreateButton(panel, "ReturnToRoomButton", "test.returnToRoom", EditorUiFactory.ButtonColor);
            EditorUiFactory.SetHeight(button, 48f);
            button.transform.SetSiblingIndex(closeButton.GetSiblingIndex());
            return button;
        }

        /// <summary>혼자 테스트할 때 역할·시점·설정을 판 중에 바로 바꾸도록 시야를 가리지 않는 오른쪽에 테스트 메뉴를 만든다.</summary>
        private static RectTransform CreateTestRoomSection(Transform canvas)
        {
            RectTransform root = EditorUiFactory.CreateRect(canvas, "TestRoomTools");
            EditorUiFactory.Stretch(root);

            RectTransform hint = EditorUiFactory.CreatePanel(root, "OpenHint", new Vector2(0.01f, 0.93f), new Vector2(0.17f, 0.975f), new Color(0f, 0f, 0f, 0.45f));
            hint.GetComponent<Image>().raycastTarget = false;
            Text hintText = EditorUiFactory.CreateText(hint, "Text", "test.openHint", 20, TextAnchor.MiddleCenter, EditorUiFactory.NoticeColor);
            EditorUiFactory.Stretch(hintText.rectTransform);
            hintText.raycastTarget = false;

            RectTransform panel = EditorUiFactory.CreatePanel(root, "Panel", new Vector2(0.62f, 0.08f), new Vector2(0.99f, 0.88f), new Color(0.06f, 0.07f, 0.09f, 0.95f));
            EditorUiFactory.AddVerticalLayout(panel, 8f, 18);

            Text title = EditorUiFactory.CreateText(panel, "Title", "test.title", 28, TextAnchor.MiddleLeft, EditorUiFactory.AccentColor);
            title.fontStyle = FontStyle.Bold;
            EditorUiFactory.SetHeight(title, 38f);
            Text status = EditorUiFactory.CreateText(panel, "Status", string.Empty, 20, TextAnchor.MiddleLeft, EditorUiFactory.TextColor);
            EditorUiFactory.SetHeight(status, 30f);

            RectTransform switchRow = EditorUiFactory.CreateRow(panel, "SwitchButtons", 48f, 8f);
            Button swapRole = EditorUiFactory.CreateButton(switchRow, "SwapRoleButton", "test.swapRole", EditorUiFactory.ButtonColor);
            Button hunt = EditorUiFactory.CreateButton(switchRow, "HuntButton", "test.toggleHunt", EditorUiFactory.ButtonColor);
            Button perspective = EditorUiFactory.CreateButton(switchRow, "PerspectiveButton", "test.togglePerspective", EditorUiFactory.ButtonColor);
            foreach (Button button in new[] { swapRole, hunt, perspective })
                EditorUiFactory.SetFlexibleWidth(button);

            Text settingsHint = EditorUiFactory.CreateText(panel, "SettingsHint", "test.settingsHint", 17, TextAnchor.MiddleLeft, new Color(0.7f, 0.7f, 0.7f));
            EditorUiFactory.SetHeight(settingsHint, 46f);

            RectTransform list = EditorUiFactory.CreateScrollList(panel, "SettingList", 4f);
            EditorUiFactory.FillRemainingHeight(list, 200f);
            SettingRowView[] rows = GuardVsMannequin.Room.GameSettingDefinitions.All
                .Where(definition => definition.id != GuardVsMannequin.Room.GameSettingId.Map)
                .Select(definition => MenuSceneBuilder.CreateSettingRow(list, definition.id))
                .ToArray();

            Button close = EditorUiFactory.CreateButton(panel, "CloseButton", "test.close", EditorUiFactory.AccentColor);
            EditorUiFactory.SetHeight(close, 48f);
            Button returnToRoom = CreateTestReturnButton(panel, close.transform);

            HudTestRoomPanel section = canvas.gameObject.AddComponent<HudTestRoomPanel>();
            TestEnvironmentSetupTool.SetField(section, "returnToRoomButton", returnToRoom);
            TestEnvironmentSetupTool.SetField(section, "openHint", hint.gameObject);
            TestEnvironmentSetupTool.SetField(section, "panel", panel.gameObject);
            TestEnvironmentSetupTool.SetField(section, "statusText", status);
            TestEnvironmentSetupTool.SetField(section, "swapRoleButton", swapRole);
            TestEnvironmentSetupTool.SetField(section, "huntButton", hunt);
            TestEnvironmentSetupTool.SetField(section, "perspectiveButton", perspective);
            TestEnvironmentSetupTool.SetField(section, "closeButton", close);
            TestEnvironmentSetupTool.SetArray(section, "settingRows", rows);

            hint.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
            return root;
        }

        /// <summary>키 설정을 바꾸면 안내도 바뀌어야 하므로 빈 영역만 만들고 내용은 HudControlsPanel이 채운다.</summary>
        private static RectTransform CreateControlsSection(Transform canvas)
        {
            RectTransform panel = EditorUiFactory.CreatePanel(canvas, "ControlsPanel", new Vector2(0.01f, 0.37f), new Vector2(0.17f, 0.63f), new Color(0f, 0f, 0f, 0.35f));
            panel.GetComponent<Image>().raycastTarget = false;
            Text text = EditorUiFactory.CreateText(panel, "ControlsText", string.Empty, 22, TextAnchor.MiddleLeft, EditorUiFactory.TextColor);
            EditorUiFactory.StretchWithPadding(text.rectTransform, 16f);
            text.raycastTarget = false;

            HudControlsPanel section = canvas.gameObject.AddComponent<HudControlsPanel>();
            TestEnvironmentSetupTool.SetField(section, "controlsPanel", panel.gameObject);
            TestEnvironmentSetupTool.SetField(section, "controlsText", text);
            TestEnvironmentSetupTool.SetField(section, "inputActions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath));
            return panel;
        }

        /// <summary>무엇을 해야 하는지 잊지 않도록 조준점 근처에 목표 영역을 둔다.</summary>
        private static RectTransform CreateObjectiveSection(Transform canvas)
        {
            RectTransform panel = EditorUiFactory.CreatePanel(canvas, "ObjectivePanel", new Vector2(0.83f, 0.42f), new Vector2(0.99f, 0.58f), new Color(0f, 0f, 0f, 0.35f));
            panel.GetComponent<Image>().raycastTarget = false;
            EditorUiFactory.AddVerticalLayout(panel, 4f, 12);

            Text title = EditorUiFactory.CreateText(panel, "Title", "hud.objective.title", 20, TextAnchor.MiddleRight, EditorUiFactory.NoticeColor);
            EditorUiFactory.SetHeight(title, 26f);
            Text goal = EditorUiFactory.CreateText(panel, "GoalText", string.Empty, 30, TextAnchor.MiddleRight, EditorUiFactory.AccentColor);
            goal.fontStyle = FontStyle.Bold;
            EditorUiFactory.SetHeight(goal, 42f);
            Text detail = EditorUiFactory.CreateText(panel, "DetailText", string.Empty, 22, TextAnchor.MiddleRight, EditorUiFactory.TextColor);
            EditorUiFactory.SetHeight(detail, 30f);
            foreach (Text text in new[] { title, goal, detail })
                text.raycastTarget = false;

            HudObjectivePanel section = canvas.gameObject.AddComponent<HudObjectivePanel>();
            TestEnvironmentSetupTool.SetField(section, "objectivePanel", panel.gameObject);
            TestEnvironmentSetupTool.SetField(section, "goalText", goal);
            TestEnvironmentSetupTool.SetField(section, "detailText", detail);
            return panel;
        }

        /// <summary>숫자 없이도 남은 체력을 한눈에 알도록 글자 없는 채움 막대로 만든다.</summary>
        private static RectTransform CreateHealthSection(Transform canvas)
        {
            RectTransform bar = EditorUiFactory.CreatePanel(canvas, "HealthBar", HealthAnchorMin, HealthAnchorMax, new Color(0f, 0f, 0f, 0.55f));
            bar.GetComponent<Image>().raycastTarget = false;

            RectTransform fill = EditorUiFactory.CreatePanel(bar, "Fill", Vector2.zero, Vector2.one, new Color(0.85f, 0.25f, 0.25f, 1f));
            Image fillImage = fill.GetComponent<Image>();
            fillImage.raycastTarget = false;

            HudHealthBar section = canvas.gameObject.AddComponent<HudHealthBar>();
            TestEnvironmentSetupTool.SetField(section, "healthPanel", bar.gameObject);
            TestEnvironmentSetupTool.SetField(section, "fill", fill);
            TestEnvironmentSetupTool.SetField(section, "fillGraphic", fillImage);
            return bar;
        }

        /// <summary>예전 HUD에서 지금은 쓰지 않는 글자를 지우고 빠진 손전등 아이콘을 다시 넣는다.</summary>
        /// <returns>지우거나 더한 오브젝트 수.</returns>
        private static int RemoveOldHudDecorations(Transform canvas)
        {
            int changed = 0;
            foreach (string path in new[] { "HealthBar/Label", "StaminaBar/Label", "CooldownBars/BarTemplate/Label" })
            {
                Transform target = canvas.Find(path);
                if (target == null)
                    continue;
                Object.DestroyImmediate(target.gameObject, true);
                changed++;
            }

            HudSwitchToolIndicator flashlight = canvas.GetComponent<HudSwitchToolIndicator>();
            Transform indicator = canvas.Find("FlashlightIndicator");
            // 아이콘이 없거나, 지웠던 그림 파일을 가리켜 비어 있으면 다시 연결한다.
            if (flashlight != null && indicator != null && !HasFlashlightIcon(flashlight))
            {
                AddFlashlightIcon((RectTransform)indicator, flashlight);
                changed++;
            }
            return changed;
        }

        /// <summary>사라진 그림도 없는 것으로 보도록 Unity의 == 비교로 손전등 아이콘 연결을 확인한다.</summary>
        private static bool HasFlashlightIcon(HudSwitchToolIndicator section)
        {
            Image icon = new SerializedObject(section).FindProperty("icon").objectReferenceValue as Image;
            return icon != null && icon.sprite != null;
        }

        /// <summary>손전등 표시 왼쪽에 아이콘을 두고 키 상자를 오른쪽으로 옮긴다.</summary>
        private static void AddFlashlightIcon(RectTransform root, HudSwitchToolIndicator section)
        {
            Transform existing = root.Find("Icon");
            RectTransform iconRect = existing != null ? (RectTransform)existing : EditorUiFactory.CreateRect(root, "Icon");
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(FlashlightIconSize, FlashlightIconSize);
            iconRect.SetAsFirstSibling();

            Image icon = iconRect.GetComponent<Image>();
            if (icon == null)
                icon = iconRect.gameObject.AddComponent<Image>();
            icon.sprite = HudSpriteFactory.GetFlashlight();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            Transform key = root.Find("Key");
            if (key != null)
                ((RectTransform)key).anchoredPosition = new Vector2(FlashlightIconSize + FlashlightIconGap, 0f);
            root.sizeDelta = new Vector2(FlashlightIconSize + FlashlightIconGap + FlashlightKeySize.x, FlashlightIconSize);

            TestEnvironmentSetupTool.SetField(section, "icon", icon);
        }

        /// <summary>예전 배치의 스태미나 막대를 체력 게이지 아래로 옮기고 쓰지 않는 상태 문구 줄을 지운다.</summary>
        private static void MoveToVitalsLayout(Transform canvas)
        {
            HudStaminaBar stamina = canvas.GetComponent<HudStaminaBar>();
            if (stamina != null && new SerializedObject(stamina).FindProperty("staminaPanel").objectReferenceValue is GameObject staminaPanel)
            {
                var rect = (RectTransform)staminaPanel.transform;
                rect.anchorMin = StaminaAnchorMin;
                rect.anchorMax = StaminaAnchorMax;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            Transform statusText = canvas.Find("BottomPanel/StatusText");
            if (statusText != null)
                Object.DestroyImmediate(statusText.gameObject, true);
        }

        /// <summary>
        /// 위치가 드러난 것은 체력만큼 중요한 상태라 체력 게이지 바로 위 가운데에 깜빡이는 눈 아이콘을 둔다.
        /// </summary>
        private static RectTransform CreateRevealSection(Transform canvas)
        {
            RectTransform rect = EditorUiFactory.CreateRect(canvas, "RevealIcon");
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, HealthAnchorMax.y);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 6f);
            rect.sizeDelta = new Vector2(44f, 44f);
            Image icon = rect.gameObject.AddComponent<Image>();
            icon.sprite = HudSpriteFactory.GetRevealed();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            rect.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            HudRevealIndicator section = canvas.gameObject.AddComponent<HudRevealIndicator>();
            TestEnvironmentSetupTool.SetField(section, "indicator", rect.gameObject);
            TestEnvironmentSetupTool.SetField(section, "icon", icon);
            rect.gameObject.SetActive(false);
            return rect;
        }

        /// <summary>어두운 사냥 시간에 손전등 키를 잊지 않도록 체력 바 옆에 아이콘과 단축키를 두고 색으로 상태를 구분한다.</summary>
        private static RectTransform CreateSwitchToolSection(Transform canvas)
        {
            RectTransform root = EditorUiFactory.CreateRect(canvas, "FlashlightIndicator");
            float centerY = (StaminaAnchorMin.y + HealthAnchorMax.y) * 0.5f;
            root.anchorMin = root.anchorMax = new Vector2(HealthAnchorMax.x, centerY);
            root.pivot = new Vector2(0f, 0.5f);
            root.anchoredPosition = new Vector2(12f, 0f);
            root.sizeDelta = FlashlightKeySize;

            RectTransform key = EditorUiFactory.CreatePanel(root, "Key", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Color(0f, 0f, 0f, 0.55f));
            key.pivot = new Vector2(0f, 0.5f);
            key.anchoredPosition = Vector2.zero;
            key.sizeDelta = FlashlightKeySize;
            key.GetComponent<Image>().raycastTarget = false;
            key.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.6f);
            Text keyText = EditorUiFactory.CreateText(key, "Text", string.Empty, 18, TextAnchor.MiddleCenter, EditorUiFactory.TextColor);
            EditorUiFactory.Stretch(keyText.rectTransform);
            keyText.raycastTarget = false;
            keyText.horizontalOverflow = HorizontalWrapMode.Overflow;

            HudSwitchToolIndicator section = canvas.gameObject.AddComponent<HudSwitchToolIndicator>();
            TestEnvironmentSetupTool.SetField(section, "indicatorPanel", root.gameObject);
            TestEnvironmentSetupTool.SetField(section, "keyText", keyText);
            TestEnvironmentSetupTool.SetField(section, "inputActions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath));
            AddFlashlightIcon(root, section);
            return root;
        }

        /// <summary>시선을 옮기지 않고 쿨타임을 보도록 조준점 왼쪽에 두고, 역할마다 도구 수가 달라 바를 실행 중에 복제한다.</summary>
        private static RectTransform CreateCooldownBarsSection(Transform canvas)
        {
            RectTransform list = EditorUiFactory.CreateRect(canvas, "CooldownBars");
            list.anchorMin = list.anchorMax = new Vector2(0.5f, 0.5f);
            list.pivot = new Vector2(1f, 0.5f);
            list.anchoredPosition = new Vector2(-44f, 0f);
            list.sizeDelta = new Vector2(160f, 84f);
            HorizontalLayoutGroup layout = list.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            RectTransform template = EditorUiFactory.CreateRect(list, "BarTemplate");
            template.sizeDelta = new Vector2(16f, 84f);

            RectTransform track = EditorUiFactory.CreatePanel(template, "Track", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0f, 0f, 0f, 0.45f));
            track.sizeDelta = new Vector2(10f, 62f);
            track.GetComponent<Image>().raycastTarget = false;

            RectTransform fill = EditorUiFactory.CreatePanel(track, "Fill", Vector2.zero, Vector2.one, Color.white);
            Image fillImage = fill.GetComponent<Image>();
            fillImage.raycastTarget = false;

            HudCooldownBarView bar = template.gameObject.AddComponent<HudCooldownBarView>();
            TestEnvironmentSetupTool.SetField(bar, "fill", fill);
            TestEnvironmentSetupTool.SetField(bar, "fillGraphic", fillImage);
            template.gameObject.SetActive(false);

            HudCooldownBars section = canvas.gameObject.AddComponent<HudCooldownBars>();
            TestEnvironmentSetupTool.SetField(section, "barsPanel", list.gameObject);
            TestEnvironmentSetupTool.SetField(section, "barTemplate", bar);
            return list;
        }

        /// <summary>
        /// 체력 게이지와 같은 모양으로 맞춰 함께 읽히도록 어두운 바탕 위 채움 막대(글자 없음)를 체력 게이지 바로 아래에 둔다.
        /// </summary>
        private static RectTransform CreateStaminaSection(Transform canvas)
        {
            RectTransform bar = EditorUiFactory.CreatePanel(canvas, "StaminaBar", StaminaAnchorMin, StaminaAnchorMax, new Color(0f, 0f, 0f, 0.55f));
            bar.GetComponent<Image>().raycastTarget = false;

            RectTransform fill = EditorUiFactory.CreatePanel(bar, "Fill", Vector2.zero, Vector2.one, new Color(0.95f, 0.8f, 0.3f, 1f));
            Image fillImage = fill.GetComponent<Image>();
            fillImage.raycastTarget = false;

            HudStaminaBar section = canvas.gameObject.AddComponent<HudStaminaBar>();
            TestEnvironmentSetupTool.SetField(section, "staminaPanel", bar.gameObject);
            TestEnvironmentSetupTool.SetField(section, "fill", fill);
            TestEnvironmentSetupTool.SetField(section, "fillGraphic", fillImage);
            return bar;
        }

        /// <summary>예전 탄환 아이콘 영역이 남은 프리팹을 바꾸려고 조준점 게이지가 연결돼 있는지 본다.</summary>
        private static bool HasAmmoGauge(HudAmmoDisplay section)
        {
            return new SerializedObject(section).FindProperty("gaugeFill").objectReferenceValue != null;
        }

        /// <summary>예전 탄환 아이콘 영역을 지우고 조준점 둘레의 원형 탄 게이지로 바꾼다.</summary>
        /// <param name="section">이미 붙어 있는 영역 컴포넌트(없으면 새로 붙임).</param>
        private static void CreateAmmoSection(Transform canvas, HudAmmoDisplay section)
        {
            Transform oldPanel = canvas.Find("AmmoPanel");
            if (oldPanel != null)
                Object.DestroyImmediate(oldPanel.gameObject, true);

            Image fill = CreateCrosshairGauge(canvas, "AmmoGauge");

            if (section == null)
                section = canvas.gameObject.AddComponent<HudAmmoDisplay>();
            TestEnvironmentSetupTool.SetField(section, "gaugePanel", fill.transform.parent.gameObject);
            TestEnvironmentSetupTool.SetField(section, "gaugeFill", fill);
        }

        /// <summary>조준과 함께 쓰는 쿨타임은 조준점에서 바로 보이도록 둘레에 고리로 둔다.</summary>
        private static void CreateCooldownRingSection(Transform canvas)
        {
            Image fill = CreateCrosshairGauge(canvas, "CooldownRing");

            HudCooldownRing section = canvas.gameObject.AddComponent<HudCooldownRing>();
            TestEnvironmentSetupTool.SetField(section, "ringPanel", fill.transform.parent.gameObject);
            TestEnvironmentSetupTool.SetField(section, "ringFill", fill);
        }

        /// <summary>시선을 옮기지 않고 명중을 알도록 조준점 바로 둘레에 대각선 네 개로 X를 그린다.</summary>
        private static void CreateHitMarkerSection(Transform canvas)
        {
            RectTransform marker = EditorUiFactory.CreateRect(canvas, "HitMarker");
            marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(40f, 40f);

            var lines = new Graphic[4];
            for (int i = 0; i < lines.Length; i++)
            {
                // 가운데를 비워 조준점을 가리지 않도록 중심에서 떨어진 곳에 짧은 선을 둔다.
                float angle = 45f + 90f * i;
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
                RectTransform line = EditorUiFactory.CreateRect(marker, "Line" + i);
                line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
                line.sizeDelta = new Vector2(11f, 3f);
                line.anchoredPosition = direction * 12f;
                line.localRotation = Quaternion.Euler(0f, 0f, angle);
                Image image = line.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                line.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);
                lines[i] = image;
            }

            Transform crosshair = canvas.Find("Crosshair");
            if (crosshair != null)
                marker.SetSiblingIndex(crosshair.GetSiblingIndex() + 1);
            else
                InsertBeforeOverlays(canvas, marker);

            HudHitMarker section = canvas.gameObject.AddComponent<HudHitMarker>();
            TestEnvironmentSetupTool.SetField(section, "marker", marker);
            TestEnvironmentSetupTool.SetArray(section, "markerLines", lines);
            marker.gameObject.SetActive(false);
        }

        /// <summary>시야 중앙을 가리지 않도록 FPS 표시 아래 오른쪽 위에 새 소식이 위로 쌓이는 목록을 둔다.</summary>
        private static RectTransform CreateEliminationFeedSection(Transform canvas)
        {
            RectTransform panel = EditorUiFactory.CreateRect(canvas, "EliminationFeed");
            panel.anchorMin = new Vector2(0.7f, 0.68f);
            panel.anchorMax = new Vector2(0.99f, 0.88f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperRight;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Text template = EditorUiFactory.CreateText(panel, "RowTemplate", string.Empty, 20, TextAnchor.MiddleRight, EditorUiFactory.TextColor);
            template.rectTransform.sizeDelta = new Vector2(0f, 28f);
            template.raycastTarget = false;
            template.horizontalOverflow = HorizontalWrapMode.Overflow;
            template.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            HudEliminationFeed section = canvas.gameObject.AddComponent<HudEliminationFeed>();
            TestEnvironmentSetupTool.SetField(section, "feedPanel", panel);
            TestEnvironmentSetupTool.SetField(section, "rowTemplate", template);
            template.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>할 일이 바뀌는 순간을 놓치지 않도록 조준점 위쪽에 크게 띄우되, 조준점 자체는 가리지 않는다.</summary>
        private static RectTransform CreatePhaseAnnouncementSection(Transform canvas)
        {
            RectTransform panel = EditorUiFactory.CreateRect(canvas, "PhaseAnnouncement");
            panel.anchorMin = new Vector2(0.15f, 0.62f);
            panel.anchorMax = new Vector2(0.85f, 0.8f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            Text title = EditorUiFactory.CreateText(panel, "Title", string.Empty, 64, TextAnchor.MiddleCenter, EditorUiFactory.AccentColor);
            title.rectTransform.anchorMin = new Vector2(0f, 0.42f);
            title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
            title.fontStyle = FontStyle.Bold;
            title.raycastTarget = false;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);

            Text subtitle = EditorUiFactory.CreateText(panel, "Subtitle", string.Empty, 30, TextAnchor.MiddleCenter, EditorUiFactory.TextColor);
            subtitle.rectTransform.anchorMin = Vector2.zero;
            subtitle.rectTransform.anchorMax = new Vector2(1f, 0.42f);
            subtitle.rectTransform.offsetMin = subtitle.rectTransform.offsetMax = Vector2.zero;
            subtitle.raycastTarget = false;
            subtitle.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);

            HudPhaseAnnouncement section = canvas.gameObject.AddComponent<HudPhaseAnnouncement>();
            TestEnvironmentSetupTool.SetField(section, "announcementPanel", panel.gameObject);
            TestEnvironmentSetupTool.SetField(section, "canvasGroup", group);
            TestEnvironmentSetupTool.SetField(section, "titleText", title);
            TestEnvironmentSetupTool.SetField(section, "subtitleText", subtitle);
            panel.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>탄 게이지와 쿨타임 고리가 같은 모양이 되도록 바탕·채움 고리 생성을 한곳에 둔다.</summary>
        private static Image CreateCrosshairGauge(Transform canvas, string name)
        {
            Sprite ring = HudSpriteFactory.GetAmmoGauge();
            RectTransform gauge = EditorUiFactory.CreateRect(canvas, name);
            gauge.anchorMin = gauge.anchorMax = new Vector2(0.5f, 0.5f);
            gauge.sizeDelta = CrosshairGaugeSize;

            CreateGaugeImage(gauge, "Track", ring, new Color(0f, 0f, 0f, 0.35f));
            Image fill = CreateGaugeImage(gauge, "Fill", ring, Color.white);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = false;
            fill.fillAmount = 1f;

            Transform crosshair = canvas.Find("Crosshair");
            if (crosshair != null)
                gauge.SetSiblingIndex(crosshair.GetSiblingIndex());
            else
                InsertBeforeOverlays(canvas, gauge);
            return fill;
        }

        /// <summary>HUD 이미지가 결과 화면 버튼 클릭을 막지 않도록 클릭을 통과시킨다.</summary>
        private static Image CreateGaugeImage(RectTransform gauge, string name, Sprite sprite, Color color)
        {
            RectTransform rect = EditorUiFactory.CreateRect(gauge, name);
            EditorUiFactory.Stretch(rect);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>나중에 더한 HUD 영역이 결과 화면·ESC 메뉴 버튼을 가리지 않도록 그 앞에 끼워 넣는다.</summary>
        private static void InsertBeforeOverlays(Transform canvas, Transform section)
        {
            foreach (string overlayName in new[] { "ResultPanel", "PauseMenu" })
            {
                Transform overlay = canvas.Find(overlayName);
                if (overlay == null)
                    continue;
                section.SetSiblingIndex(overlay.GetSiblingIndex());
                return;
            }
        }

        /// <summary>손으로 다듬은 설정 화면을 지키려고 초기화하지 않고 목록에 없는 키의 줄만 더한다.</summary>
        /// <param name="settingsPanel">설정 화면 프리팹의 루트.</param>
        /// <returns>새로 추가한 줄 수(키 설정 목록이 없으면 -1).</returns>
        public static int AddMissingRebindRows(GameObject settingsPanel)
        {
            ControlSettingsSection controls = settingsPanel.GetComponentInChildren<ControlSettingsSection>(true);
            if (controls == null)
                return -1;

            var serialized = new SerializedObject(controls);
            SerializedProperty rows = serialized.FindProperty("rebindRows");
            var existing = new System.Collections.Generic.List<RebindRowView>();
            for (int i = 0; i < rows.arraySize; i++)
            {
                if (rows.GetArrayElementAtIndex(i).objectReferenceValue is RebindRowView row)
                    existing.Add(row);
            }
            if (existing.Count == 0)
                return -1;

            Transform list = existing[0].transform.parent;
            int added = 0;
            foreach (KeyBindingCatalog.Entry entry in KeyBindingCatalog.All)
            {
                string part = entry.partName ?? string.Empty;
                if (existing.Any(row => row.ActionName == entry.actionName && row.PartName == part))
                    continue;

                RebindRowView newRow = CreateRebindRow(list, entry);
                rows.InsertArrayElementAtIndex(rows.arraySize);
                rows.GetArrayElementAtIndex(rows.arraySize - 1).objectReferenceValue = newRow;
                added++;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return added;
        }

        /// <summary>기존 프리팹을 초기화하지 않고 새 그래픽 설정 줄만 관련 항목 아래에 더한다.</summary>
        /// <param name="settingsPanel">설정 화면 프리팹의 루트.</param>
        /// <returns>새로 추가한 줄 수(그래픽 페이지가 없으면 -1).</returns>
        public static int AddMissingGraphicsRows(GameObject settingsPanel)
        {
            DisplaySettingsSection display = settingsPanel.GetComponentInChildren<DisplaySettingsSection>(true);
            if (display == null)
                return -1;

            Transform page = display.transform;
            int added = 0;
            if (settingsPanel.GetComponentInChildren<FrameLimitSettingsSection>(true) == null)
            {
                InsertAfter(page, CreateFrameLimitRow(page), "ResolutionRow");
                added++;
            }
            if (settingsPanel.GetComponentInChildren<CameraSwaySettingsSection>(true) == null)
            {
                InsertAfter(page, CreateCameraSwayRow(page), "FrameLimitRow");
                added++;
            }
            if (settingsPanel.GetComponentInChildren<StatsDisplaySettingsSection>(true) == null)
            {
                InsertAfter(page, CreateStatsDisplayRow(page), "CameraSwayRow");
                added++;
            }
            added += AddOptionRowIfMissing<FieldOfViewSettingsSection>(settingsPanel, page, "FieldOfView", "options.fov", "CameraSwayRow");
            added += RemoveObsoleteRows(page);
            added += AddOptionRowIfMissing<CrosshairColorSettingsSection>(settingsPanel, page, "CrosshairColor", "options.crosshairColor", "StatsDisplayRow");
            added += AddOptionRowIfMissing<ControlsHintSettingsSection>(settingsPanel, page, "ControlsHint", "options.controlsHint", "CrosshairColorRow");

            // 마우스 조작과 함께 찾도록 Y축 반전은 조작 페이지의 감도 아래에 둔다.
            ControlSettingsSection controls = settingsPanel.GetComponentInChildren<ControlSettingsSection>(true);
            if (controls != null)
                added += AddOptionRowIfMissing<InvertLookSettingsSection>(settingsPanel, controls.transform, "InvertLook", "options.invertLook", "SensitivityRow");
            return added;
        }

        // 기능을 뺀 설정 줄. 스크립트가 지워져 빈 컴포넌트로 남으므로 이름으로 찾아 지운다.
        private static readonly string[] ObsoleteRowNames = { "UiScaleRow" };

        /// <summary>
        /// 뺀 설정의 줄과, 그 설정 스크립트가 지워져 페이지에 남은 빈(Missing) 컴포넌트를 지운다.
        /// </summary>
        /// <returns>지운 줄·컴포넌트 수.</returns>
        private static int RemoveObsoleteRows(Transform page)
        {
            int removed = 0;
            foreach (string rowName in ObsoleteRowNames)
            {
                Transform row = page.Find(rowName);
                if (row == null)
                    continue;
                Object.DestroyImmediate(row.gameObject, true);
                removed++;
            }
            removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(page.gameObject);
            return removed;
        }

        /// <summary>손으로 다듬은 설정 화면을 지키려고 그 항목이 없을 때만 기준 줄 아래에 더한다.</summary>
        /// <returns>더했으면 1.</returns>
        private static int AddOptionRowIfMissing<T>(GameObject settingsPanel, Transform page, string name, string labelKey, string anchorRowName)
            where T : OptionSelectorSection
        {
            if (settingsPanel.GetComponentInChildren<T>(true) != null)
                return 0;
            InsertAfter(page, CreateOptionRow<T>(page, name, labelKey), anchorRowName);
            return 1;
        }

        /// <summary>◀ ▶ 개인 설정 줄은 모양과 연결이 같아 종류만 받아 만든다.</summary>
        private static Transform CreateOptionRow<T>(Transform page, string name, string labelKey) where T : OptionSelectorSection
        {
            CreateSelectorRow(page, name, labelKey, out Button previous, out Text value, out Button next);
            T section = page.gameObject.AddComponent<T>();
            TestEnvironmentSetupTool.SetField(section, "previousButton", previous);
            TestEnvironmentSetupTool.SetField(section, "nextButton", next);
            TestEnvironmentSetupTool.SetField(section, "valueText", value);
            return previous.transform.parent;
        }

        /// <summary>새 설정 화면과 기존 프리팹 보강이 같은 줄을 쓰도록 생성을 한곳에 둔다.</summary>
        private static Transform CreateStatsDisplayRow(Transform page)
        {
            CreateSelectorRow(page, "StatsDisplay", "options.statsDisplay", out Button previous, out Text value, out Button next);
            StatsDisplaySettingsSection section = page.gameObject.AddComponent<StatsDisplaySettingsSection>();
            TestEnvironmentSetupTool.SetField(section, "previousStatsDisplayButton", previous);
            TestEnvironmentSetupTool.SetField(section, "nextStatsDisplayButton", next);
            TestEnvironmentSetupTool.SetField(section, "statsDisplayValueText", value);
            return previous.transform.parent;
        }

        /// <summary>새 줄이 관련 항목과 떨어지지 않도록 기준 줄 바로 아래로 옮긴다.</summary>
        private static void InsertAfter(Transform page, Transform row, string anchorRowName)
        {
            Transform anchorRow = page.Find(anchorRowName);
            if (anchorRow != null)
                row.SetSiblingIndex(anchorRow.GetSiblingIndex() + 1);
        }

        /// <summary>새 설정 화면과 기존 프리팹 보강이 같은 줄을 쓰도록 생성을 한곳에 둔다.</summary>
        private static Transform CreateCameraSwayRow(Transform page)
        {
            CreateSelectorRow(page, "CameraSway", "options.cameraSway", out Button previous, out Text value, out Button next);
            CameraSwaySettingsSection section = page.gameObject.AddComponent<CameraSwaySettingsSection>();
            TestEnvironmentSetupTool.SetField(section, "previousCameraSwayButton", previous);
            TestEnvironmentSetupTool.SetField(section, "nextCameraSwayButton", next);
            TestEnvironmentSetupTool.SetField(section, "cameraSwayValueText", value);
            return previous.transform.parent;
        }

        /// <summary>새 설정 화면과 기존 프리팹 보강이 같은 줄을 쓰도록 생성을 한곳에 둔다.</summary>
        private static Transform CreateFrameLimitRow(Transform page)
        {
            CreateSelectorRow(page, "FrameLimit", "options.frameLimit", out Button previous, out Text value, out Button next);
            FrameLimitSettingsSection section = page.gameObject.AddComponent<FrameLimitSettingsSection>();
            TestEnvironmentSetupTool.SetField(section, "previousFrameLimitButton", previous);
            TestEnvironmentSetupTool.SetField(section, "nextFrameLimitButton", next);
            TestEnvironmentSetupTool.SetField(section, "frameLimitValueText", value);
            return previous.transform.parent;
        }

        /// <summary>탭마다 같은 틀을 쓰도록 위에서부터 쌓는 페이지 생성을 한곳에 둔다.</summary>
        private static RectTransform CreatePage(Transform parent, string name)
        {
            RectTransform page = EditorUiFactory.CreateRect(parent, name);
            EditorUiFactory.Stretch(page);
            EditorUiFactory.AddVerticalLayout(page, 10f, 4);
            return page;
        }

        /// <summary>◀ ▶ 선택 줄이 같은 모양이 되도록 한곳에서 만든다.</summary>
        private static void CreateSelectorRow(Transform parent, string name, string label, out Button previous, out Text value, out Button next)
        {
            RectTransform row = EditorUiFactory.CreateRow(parent, name + "Row", 44f, 16f);
            Text labelText = EditorUiFactory.CreateText(row, "Label", label, 24, TextAnchor.MiddleLeft, EditorUiFactory.TextColor);
            EditorUiFactory.SetWidth(labelText, 200f);
            previous = EditorUiFactory.CreateButton(row, "Previous" + name + "Button", "◀", EditorUiFactory.ButtonColor);
            EditorUiFactory.SetWidth(previous, 56f);
            value = EditorUiFactory.CreateText(row, "Value", "-", 24, TextAnchor.MiddleCenter, EditorUiFactory.TextColor);
            EditorUiFactory.SetFlexibleWidth(value);
            next = EditorUiFactory.CreateButton(row, "Next" + name + "Button", "▶", EditorUiFactory.ButtonColor);
            EditorUiFactory.SetWidth(next, 56f);
        }

        /// <summary>음량·감도 줄이 같은 모양이 되도록 한곳에서 만든다.</summary>
        private static Slider CreateSliderRow(Transform parent, string name, string label, out Text valueText)
        {
            RectTransform row = EditorUiFactory.CreateRow(parent, name + "Row", 44f, 16f);
            Text labelText = EditorUiFactory.CreateText(row, "Label", label, 24, TextAnchor.MiddleLeft, EditorUiFactory.TextColor);
            EditorUiFactory.SetWidth(labelText, 200f);

            Slider slider = EditorUiFactory.CreateSlider(row, name + "Slider");
            EditorUiFactory.SetFlexibleWidth(slider);

            valueText = EditorUiFactory.CreateText(row, "Value", "-", 24, TextAnchor.MiddleRight, EditorUiFactory.TextColor);
            EditorUiFactory.SetWidth(valueText, 90f);
            return slider;
        }

        /// <summary>새 화면과 기존 프리팹 보강이 같은 키 줄을 쓰도록 생성을 한곳에 둔다.</summary>
        private static RebindRowView CreateRebindRow(Transform parent, KeyBindingCatalog.Entry entry)
        {
            RectTransform row = EditorUiFactory.CreateRow(parent, $"Rebind_{entry.actionName}_{entry.partName}", 42f, 10f);

            // 이름은 RebindRowView가 현재 언어로 채우므로 key를 넘기지 않는다.
            Text label = EditorUiFactory.CreateText(row, "Label", entry.actionName, 22, TextAnchor.MiddleLeft, EditorUiFactory.TextColor);
            EditorUiFactory.SetFlexibleWidth(label);
            Text key = EditorUiFactory.CreateText(row, "Key", "-", 22, TextAnchor.MiddleCenter, EditorUiFactory.NoticeColor);
            EditorUiFactory.SetWidth(key, 260f);
            Button rebind = EditorUiFactory.CreateButton(row, "RebindButton", "options.rebind", EditorUiFactory.ButtonColor);
            EditorUiFactory.SetWidth(rebind, 110f);
            Button reset = EditorUiFactory.CreateButton(row, "ResetButton", "options.reset", EditorUiFactory.ButtonColor);
            EditorUiFactory.SetWidth(reset, 110f);

            RebindRowView view = row.gameObject.AddComponent<RebindRowView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("actionName").stringValue = entry.actionName;
            serialized.FindProperty("partName").stringValue = entry.partName ?? string.Empty;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            TestEnvironmentSetupTool.SetField(view, "labelText", label);
            TestEnvironmentSetupTool.SetField(view, "keyText", key);
            TestEnvironmentSetupTool.SetField(view, "rebindButton", rebind);
            TestEnvironmentSetupTool.SetField(view, "resetButton", reset);
            return view;
        }
    }
}
