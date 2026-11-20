using GuardVsMannequin.Network;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>참가 관련 화면들의 버튼을 맡고, 초대로 들어가려는 방에 암호가 필요하면 입장 화면을 연다.</summary>
    public class LobbyJoinMenu : MonoBehaviour
    {
        [Header("참가 화면")]
        [SerializeField] private Button steamJoinMenuButton;
        [SerializeField] private Button directJoinMenuButton;

        [Header("스팀 방 입장 화면")]
        [SerializeField] private InputField roomCodeInput;
        [SerializeField] private InputField joinPasswordInput;
        [SerializeField] private Button joinSteamButton;

        [Header("IP로 참가 화면")]
        [SerializeField] private InputField addressInput;
        [SerializeField] private Button joinDirectButton;

        private ILobbyNavigator navigator;

        /// <summary>메뉴가 접속 방법을 몰라도 되도록 접속은 세션 서비스에 맡긴다.</summary>
        private void Start()
        {
            navigator = UiLookup.FindInParent<ILobbyNavigator>(this);
            steamJoinMenuButton.onClick.AddListener(() => navigator?.Show(LobbyScreen.SteamJoin));
            directJoinMenuButton.onClick.AddListener(() => navigator?.Show(LobbyScreen.DirectJoin));
            joinSteamButton.onClick.AddListener(() => SessionServices.Session?.JoinSteamRoom(roomCodeInput.text, joinPasswordInput.text));
            joinDirectButton.onClick.AddListener(() => SessionServices.Session?.JoinDirectRoom(addressInput.text));

            // 입력한 뒤 마우스로 버튼을 찾지 않아도 되도록 Enter로 바로 입장한다.
            LobbyInputSubmit.Connect(joinSteamButton, roomCodeInput, joinPasswordInput);
            LobbyInputSubmit.Connect(joinDirectButton, addressInput);

            joinPasswordInput.contentType = InputField.ContentType.Password;

            if (SessionServices.Session != null)
                SessionServices.Session.PasswordRequired += OnPasswordRequired;
        }

        /// <summary>
        /// 세션 서비스는 씬보다 오래 살아서, 끊지 않으면 파괴된 메뉴를 부른다.
        /// </summary>
        private void OnDestroy()
        {
            if (SessionServices.Session != null)
                SessionServices.Session.PasswordRequired -= OnPasswordRequired;
        }

        /// <summary>
        /// Steam 상태와 접속 진행 여부는 알림 없이 바뀌므로 매 프레임 확인해, 실패할 버튼을 미리 막는다.
        /// </summary>
        private void Update()
        {
            ISessionService session = SessionServices.Session;
            if (session == null)
                return;

            steamJoinMenuButton.interactable = session.CanOpenSteamMenu();
            joinSteamButton.interactable = session.CanConnectSteam();
            joinDirectButton.interactable = session.CanConnectDirect();
        }

        /// <summary>
        /// 초대로 들어온 사람은 방 코드를 모르므로 미리 채워 두고, 바로 칠 수 있게 암호 입력칸에 초점을 둔다.
        /// </summary>
        private void OnPasswordRequired(string roomCode)
        {
            navigator?.Show(LobbyScreen.SteamJoin);
            roomCodeInput.text = roomCode;
            joinPasswordInput.text = string.Empty;
            joinPasswordInput.Select();
            joinPasswordInput.ActivateInputField();
        }
    }
}
