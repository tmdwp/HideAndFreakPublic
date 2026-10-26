using GuardVsMannequin.Network;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 방 만들기 화면(스팀 방 만들기 / 직접 방 만들기), 스팀 방 만들기 화면(암호 입력),
    /// 직접 방 만들기 화면(일반 방 / 테스트 방)의 버튼.
    /// </summary>
    public class LobbyCreateMenu : MonoBehaviour
    {
        [Header("방 만들기 화면")]
        [SerializeField] private Button steamCreateMenuButton;
        [Tooltip("직접 방 만들기. 직접 방 만들기 화면이 있으면 그 화면을 열고, 없으면(예전 프리팹) 바로 일반 방을 만든다.")]
        [SerializeField] private Button hostDirectButton;

        [Header("스팀 방 만들기 화면")]
        [SerializeField] private InputField hostPasswordInput;
        [SerializeField] private Button hostSteamButton;

        [Header("직접 방 만들기 화면")]
        [SerializeField] private Button hostDirectNormalButton;
        [SerializeField] private Button hostTestButton;

        private ILobbyNavigator navigator;

        /// <summary>
        /// 버튼 동작을 연결한다. 방 생성은 세션 서비스에 맡긴다.
        /// </summary>
        private void Start()
        {
            navigator = UiLookup.FindInParent<ILobbyNavigator>(this);
            steamCreateMenuButton.onClick.AddListener(() => navigator?.Show(LobbyScreen.SteamCreate));
            hostDirectButton.onClick.AddListener(OnDirectCreatePressed);
            hostSteamButton.onClick.AddListener(() => SessionServices.Session?.HostSteamRoom(hostPasswordInput.text));
            if (hostDirectNormalButton != null)
                hostDirectNormalButton.onClick.AddListener(() => SessionServices.Session?.HostDirectRoom());
            if (hostTestButton != null)
                hostTestButton.onClick.AddListener(() => SessionServices.Session?.HostTestRoom());

            hostPasswordInput.contentType = InputField.ContentType.Password;
        }

        /// <summary>
        /// Steam 연결과 진행 중인 작업 여부에 따라 버튼을 켜고 끈다.
        /// </summary>
        private void Update()
        {
            ISessionService session = SessionServices.Session;
            if (session == null)
                return;

            steamCreateMenuButton.interactable = session.CanOpenSteamMenu();
            hostSteamButton.interactable = session.CanConnectSteam();
            bool canConnectDirect = session.CanConnectDirect();
            hostDirectButton.interactable = canConnectDirect;
            if (hostDirectNormalButton != null)
                hostDirectNormalButton.interactable = canConnectDirect;
            if (hostTestButton != null)
                hostTestButton.interactable = canConnectDirect;
        }

        /// <summary>
        /// 직접 방 만들기 화면(일반 방 / 테스트 방)을 연다. 화면이 없는 예전 프리팹이면 바로 일반 방을 만든다.
        /// </summary>
        private void OnDirectCreatePressed()
        {
            if (navigator != null && navigator.HasScreen(LobbyScreen.DirectCreate))
                navigator.Show(LobbyScreen.DirectCreate);
            else
                SessionServices.Session?.HostDirectRoom();
        }
    }
}
