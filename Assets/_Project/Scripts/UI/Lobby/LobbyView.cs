using System;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Network;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>화면을 추가해도 코드를 고치지 않도록 인스펙터 목록으로 로비 화면 이동과 뒤로 가기를 처리한다.</summary>
    public class LobbyView : MonoBehaviour, ILobbyNavigator, ILobbyOverlays
    {
        /// <summary>화면 하나: 종류, 패널, 뒤로 가면 돌아갈 화면.</summary>
        [Serializable]
        private class ScreenEntry
        {
            [Tooltip("화면 종류")]
            public LobbyScreen screen;

            [Tooltip("이 화면의 패널 오브젝트")]
            public GameObject panel;

            [Tooltip("뒤로 가기(버튼·ESC) 때 돌아갈 화면. 자기 자신이면 더 돌아가지 않는다.")]
            public LobbyScreen parent;
        }

        [Header("화면 (첫 항목이 시작 화면)")]
        [SerializeField] private ScreenEntry[] screens = Array.Empty<ScreenEntry>();

        [Header("뒤로 (각 화면의 뒤로 버튼)")]
        [SerializeField] private Button[] backButtons;

        [Header("상태")]
        [SerializeField] private Text steamStatusText;
        [SerializeField] private Text noticeText;

        [Header("패널")]
        [SerializeField] private SettingsPanelView settingsPanel;
        [SerializeField] private CreditsView creditsPanel;

        private LobbyScreen currentScreen;

        /// <summary>뒤로 가는 곳이 달라도 동작은 같아 모두 Back에 연결하고, 켜진 채 저장된 패널은 시작할 때 정리한다.</summary>
        private void Start()
        {
            foreach (Button backButton in backButtons)
                backButton.onClick.AddListener(Back);

            settingsPanel.gameObject.SetActive(false);
            creditsPanel.gameObject.SetActive(false);
            if (screens.Length > 0)
                Show(screens[0].screen);
        }

        /// <summary>세션 상태는 알림 없이 바뀌므로 매 프레임 문구를 갱신하고, 로비에는 아바타 입력이 없어 ESC를 직접 읽는다.</summary>
        private void Update()
        {
            RefreshStatus();

            // 설정·크레딧이 열려 있으면 그쪽이 ESC를 처리한다.
            bool isOverlayOpen = settingsPanel.IsOpen || creditsPanel.gameObject.activeSelf;
            if (!isOverlayOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Back();
        }

        /// <summary>
        /// 세션 서비스가 없으면 Steam 상태를 알 수 없으므로, 그때는 서비스가 없다는 안내만 보인다.
        /// </summary>
        private void RefreshStatus()
        {
            ISessionService session = SessionServices.Session;
            if (session == null)
            {
                noticeText.text = Loc.Get("lobby.noService");
                return;
            }

            steamStatusText.text = Loc.Get(session.IsSteamAvailable ? "lobby.steam.connected" : "lobby.steam.unavailable");
            noticeText.text = session.Notice;
        }

        /// <summary>
        /// 인스펙터 목록에 빠진 화면을 열면 모든 패널이 꺼져 빈 화면이 되므로, 그때는 경고만 남기고 지금 화면을 유지한다.
        /// </summary>
        public void Show(LobbyScreen screen)
        {
            if (FindEntry(screen) == null)
            {
                Debug.LogWarning($"[LobbyView] 화면 목록에 {screen}이 없습니다. 인스펙터의 화면 목록을 확인하세요.", this);
                return;
            }

            currentScreen = screen;
            foreach (ScreenEntry entry in screens)
            {
                if (entry.panel != null)
                    entry.panel.SetActive(entry.screen == screen);
            }
        }

        /// <summary>
        /// 시작 화면은 부모를 자기 자신으로 두어 더 돌아가지 않게 한다.
        /// </summary>
        public void Back()
        {
            ScreenEntry entry = FindEntry(currentScreen);
            if (entry != null && entry.parent != currentScreen)
                Show(entry.parent);
        }

        /// <summary>
        /// 목록에 있어도 패널을 연결하지 않았으면 열 수 없으므로 패널까지 확인한다.
        /// </summary>
        public bool HasScreen(LobbyScreen screen)
        {
            ScreenEntry entry = FindEntry(screen);
            return entry != null && entry.panel != null;
        }

        /// <summary>
        /// 메뉴가 패널 참조를 몰라도 되도록 로비 화면이 대신 연다.
        /// </summary>
        public void OpenSettings()
        {
            settingsPanel.Open();
        }

        /// <summary>
        /// 메뉴가 패널 참조를 몰라도 되도록 로비 화면이 대신 연다.
        /// </summary>
        public void OpenCredits()
        {
            creditsPanel.Open();
        }

        /// <summary>
        /// 인스펙터 목록에 빠진 화면이 있을 수 있어, 호출하는 쪽이 확인할 수 있게 없으면 null을 돌려준다.
        /// </summary>
        private ScreenEntry FindEntry(LobbyScreen screen)
        {
            return Array.Find(screens, entry => entry.screen == screen);
        }
    }
}
