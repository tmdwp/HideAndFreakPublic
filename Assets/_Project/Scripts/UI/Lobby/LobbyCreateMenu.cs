using GuardVsMannequin.Network;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>방 만들기 관련 화면들의 버튼을 맡는다.</summary>
    public class LobbyCreateMenu : MonoBehaviour
    {
        [Header("방 만들기 화면")]
        [SerializeField] private Button steamCreateMenuButton;
        [Tooltip("직접 방 만들기. 직접 방 만들기 화면이 있으면 그 화면을 열고, 없으면(예전 프리팹) 바로 일반 방을 만든다.")]
        [SerializeField] private Button hostDirectButton;

        [Header("스팀 방 만들기 화면")]
        [SerializeField] private InputField hostPasswordInput;
        [Tooltip("방 이름 입력칸. 비우면 '<Steam 이름>의 방'.")]
        [SerializeField] private InputField roomNameInput;
        [Tooltip("공개방 토글. 끄면 친구 전용. 비우면(예전 프리팹) 공개방.")]
        [SerializeField] private Toggle publicRoomToggle;
        [SerializeField] private Button hostSteamButton;

        [Header("직접 방 만들기 화면")]
        [SerializeField] private Button hostDirectNormalButton;
        [SerializeField] private Button hostTestButton;

        private ILobbyNavigator navigator;

        /// <summary>메뉴가 접속 방법을 몰라도 되도록 방 생성은 세션 서비스에 맡기고, 예전 프리팹에 없는 버튼은 있을 때만 연결한다.</summary>
        private void Start()
        {
            navigator = UiLookup.FindInParent<ILobbyNavigator>(this);
            steamCreateMenuButton.onClick.AddListener(() => navigator?.Show(LobbyScreen.SteamCreate));
            hostDirectButton.onClick.AddListener(OnDirectCreatePressed);
            hostSteamButton.onClick.AddListener(HostSteamRoom);
            if (hostDirectNormalButton != null)
                hostDirectNormalButton.onClick.AddListener(() => SessionServices.Session?.HostDirectRoom());
            if (hostTestButton != null)
                hostTestButton.onClick.AddListener(() => SessionServices.Session?.HostTestRoom());

            hostPasswordInput.contentType = InputField.ContentType.Password;
            if (roomNameInput != null)
                roomNameInput.characterLimit = SteamRoomOptions.MaxNameLength;
        }

        /// <summary>
        /// 예전 프리팹에는 공개방 토글·방 이름 칸이 없으므로, 그때는 이전 동작(공개방, 기본 이름)이 되도록 기본값을 채운다.
        /// </summary>
        private void HostSteamRoom()
        {
            SessionServices.Session?.HostSteamRoom(new SteamRoomOptions
            {
                password = hostPasswordInput.text,
                isPublic = publicRoomToggle == null || publicRoomToggle.isOn,
                roomName = roomNameInput != null ? roomNameInput.text : string.Empty
            });
        }

        /// <summary>
        /// Steam 상태와 접속 진행 여부는 알림 없이 바뀌므로 매 프레임 확인해, 실패하거나 겹칠 버튼을 미리 막는다.
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
        /// 예전 프리팹에는 직접 방 만들기 화면이 없어 버튼이 아무 일도 하지 않게 되므로, 그때는 바로 일반 방을 만든다.
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
