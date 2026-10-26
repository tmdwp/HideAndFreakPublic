using System;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Network;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 로비(메인 메뉴) 화면 관리. 화면(패널)을 하나씩 보여주며 이동하고, 공통 상태 문구와 뒤로 가기(버튼·ESC)를 처리한다.
    /// 화면과 "뒤로 가면 어디로 가는지"(부모 화면)는 인스펙터의 화면 목록으로 정한다. 화면을 추가해도 이 코드는 바꾸지 않는다.
    /// 기본 구성: 메인 ─ 방 만들기 ─ 스팀 방 만들기(암호) / 직접 방 만들기
    ///               ─ 참가     ─ 스팀 방 입장(방 코드, 암호) / IP로 참가(주소)
    ///               ─ 설정, 크레딧, 종료
    /// 각 화면의 버튼 동작은 메뉴 코드가 맡는다: <see cref="LobbyMainMenu"/>, <see cref="LobbyCreateMenu"/>, <see cref="LobbyJoinMenu"/>.
    /// </summary>
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

        /// <summary>
        /// 뒤로 버튼을 연결하고 시작 화면을 보여준다.
        /// </summary>
        private void Start()
        {
            foreach (Button back in backButtons)
                back.onClick.AddListener(Back);

            settingsPanel.gameObject.SetActive(false);
            creditsPanel.gameObject.SetActive(false);
            if (screens.Length > 0)
                Show(screens[0].screen);
        }

        /// <summary>
        /// 상태 문구를 갱신하고, ESC로 이전 화면으로 돌아간다.
        /// </summary>
        private void Update()
        {
            RefreshStatus();

            // 설정·크레딧이 열려 있으면 그쪽이 ESC를 처리한다.
            bool overlayOpen = settingsPanel.IsOpen || creditsPanel.gameObject.activeSelf;
            if (!overlayOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Back();
        }

        /// <summary>
        /// Steam 연결 상태와 세션 안내 문구를 표시한다.
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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public void Back()
        {
            ScreenEntry entry = FindEntry(currentScreen);
            if (entry != null && entry.parent != currentScreen)
                Show(entry.parent);
        }

        /// <inheritdoc />
        public bool HasScreen(LobbyScreen screen)
        {
            ScreenEntry entry = FindEntry(screen);
            return entry != null && entry.panel != null;
        }

        /// <inheritdoc />
        public void OpenSettings()
        {
            settingsPanel.Open();
        }

        /// <inheritdoc />
        public void OpenCredits()
        {
            creditsPanel.Open();
        }

        /// <summary>
        /// 화면 종류의 목록 항목을 찾는다. 없으면 null.
        /// </summary>
        private ScreenEntry FindEntry(LobbyScreen screen)
        {
            return Array.Find(screens, entry => entry.screen == screen);
        }
    }
}
