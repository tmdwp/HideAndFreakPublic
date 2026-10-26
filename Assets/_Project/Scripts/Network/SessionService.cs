using System;
using FishNet.Transporting;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Mods;
using UnityEngine;

namespace GuardVsMannequin.Network
{
    /// <summary>
    /// Steam 로비와 트랜스포트 연결을 묶어 "방"을 만든다/참가한다/나간다.
    /// - Steam 방 만들기: 로비 생성 → 서버(Tugboat + Steam) 시작 → 로컬 클라이언트 접속
    /// - Steam 방 참가: 로비 입장 → 호스트 SteamID로 Steam 클라이언트 접속
    /// - 직접 방: Tugboat만 사용 (한 PC에서 여러 개를 띄워 테스트할 때)
    /// 서버가 시작되면 FishNet DefaultScene이 룸 씬을, 연결이 끊기면 로비 씬을 불러온다.
    /// </summary>
    public class SessionService : MonoBehaviour, ISessionService
    {
        /// <summary>모드가 달라 입장할 수 없을 때의 안내 key.</summary>
        public const string ModRejectedNoticeKey = "notice.rejected.mods";

        [SerializeField] private TransportConnector connector;
        [SerializeField] private SteamLobbyService steamLobby;

        private bool isLeaving;
        private bool isRegistered;

        // 호스트가 알려 준 접속 거부 이유 (연결이 끊길 때 안내에 사용)
        private string rejectNoticeKey;
        private string rejectNoticeDetail;

        // 모드가 달라 거부됐을 때 호스트 모드 목록 (거부 직후: 연결이 끊길 때 확정, 확정 후: 로비 팝업이 꺼내 감)
        private string pendingModRejection;
        private string modRejection;

        // 안내 문구는 key와 끼워 넣을 값으로 보관하고, 읽을 때 현재 언어로 만든다. (언어를 바꾸면 바로 반영)
        private string noticeKey = string.Empty;
        private string noticeDetail = string.Empty;

        public bool IsSteamAvailable => SteamBootstrap.IsInitialized && connector.HasSteamTransport;
        public bool IsBusy { get; private set; }
        public bool IsSteamRoom { get; private set; }
        public bool IsTestRoom { get; private set; }
        public string Notice => string.IsNullOrEmpty(noticeKey) ? string.Empty : Loc.Format(noticeKey, noticeDetail);

        public string RoomCode => IsSteamRoom && steamLobby.CurrentLobbyId != 0
            ? steamLobby.CurrentLobbyId.ToString()
            : Loc.Get(IsTestRoom ? "session.testRoomCode" : "session.directRoomCode");

        public string RoomPassword { get; private set; } = string.Empty;
        public string JoinPassword { get; private set; } = string.Empty;

        public event Action<string> PasswordRequired;

        /// <summary>
        /// 첫 인스턴스만 서비스로 등록하고 이벤트를 연결한다. (로비 씬 재로딩 시 생기는 중복은 무시)
        /// </summary>
        private void Awake()
        {
            isRegistered = SessionServices.TryRegister(this);
            if (!isRegistered)
                return;

            steamLobby.LobbyCreated += OnLobbyCreated;
            steamLobby.JoinedLobby += OnJoinedLobby;
            steamLobby.JoinRequested += OnJoinRequested;
            steamLobby.Failed += OnLobbyFailed;
        }

        /// <summary>
        /// 클라이언트 연결 상태를 구독하고 접속 검증기를 서버에 연결한다.
        /// (NetworkManager의 하위 매니저는 NetworkManager.Awake 이후에 준비됨)
        /// </summary>
        private void Start()
        {
            if (!isRegistered)
                return;

            connector.NetworkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            connector.NetworkManager.ClientManager.RegisterBroadcast<ConnectionRejectedBroadcast>(OnConnectionRejected);
            SetupAuthenticator();
            // 모드 맵 씬을 불러오기 전에 번들을 불러오는 씬 처리기 (모든 PC)
            ModSceneProcessor.Install(connector.NetworkManager);
        }

        /// <summary>
        /// 새 접속을 검증하는 <see cref="RoomAuthenticator"/>를 준비해 서버에 등록한다.
        /// </summary>
        private void SetupAuthenticator()
        {
            if (!TryGetComponent(out RoomAuthenticator authenticator))
                authenticator = gameObject.AddComponent<RoomAuthenticator>();

            authenticator.Configure(this, steamLobby);
            connector.NetworkManager.ServerManager.SetAuthenticator(authenticator);
        }

        /// <summary>
        /// 등록된 인스턴스라면 이벤트 연결을 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            if (!isRegistered)
                return;

            SessionServices.Unregister(this);
            steamLobby.LobbyCreated -= OnLobbyCreated;
            steamLobby.JoinedLobby -= OnJoinedLobby;
            steamLobby.JoinRequested -= OnJoinRequested;
            steamLobby.Failed -= OnLobbyFailed;
            if (connector != null && connector.NetworkManager != null)
            {
                connector.NetworkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
                connector.NetworkManager.ClientManager.UnregisterBroadcast<ConnectionRejectedBroadcast>(OnConnectionRejected);
            }
        }

        /// <summary>
        /// 호스트가 접속을 거부한 이유를 기억한다. 곧 연결이 끊기면 이 이유로 안내한다.
        /// </summary>
        private void OnConnectionRejected(ConnectionRejectedBroadcast message, Channel channel)
        {
            (rejectNoticeKey, rejectNoticeDetail) = message.reason switch
            {
                ConnectionRejectReason.VersionMismatch => ("notice.rejected.version", $"{message.hostVersion} ≠ {GameVersion.Current}"),
                ConnectionRejectReason.RoomFull => ("notice.rejected.full", string.Empty),
                ConnectionRejectReason.InGame => ("notice.rejected.inGame", string.Empty),
                ConnectionRejectReason.NotLobbyMember => ("notice.rejected.notMember", string.Empty),
                ConnectionRejectReason.WrongPassword => ("notice.rejected.password", string.Empty),
                ConnectionRejectReason.ModMismatch => (ModRejectedNoticeKey, DescribeMods(message.hostMods)),
                _ => ("notice.rejected.version", $"{message.hostVersion} ≠ {GameVersion.Current}")
            };
            pendingModRejection = message.reason == ConnectionRejectReason.ModMismatch ? message.hostMods ?? string.Empty : null;
        }

        /// <inheritdoc />
        public bool TryTakeModRejection(out string hostMods)
        {
            hostMods = modRejection;
            modRejection = null;
            return hostMods != null;
        }

        /// <summary>
        /// 모드 목록 안내 문구. (없으면 "없음")
        /// </summary>
        private static string DescribeMods(string mods)
        {
            return string.IsNullOrEmpty(mods) ? Loc.Get("notice.mods.none") : mods;
        }

        /// <summary>
        /// Steam 로비를 먼저 만들고, 생성되면 서버를 시작한다. 암호를 정하면 참가자는 같은 암호를 입력해야 한다.
        /// </summary>
        public void HostSteamRoom(string password)
        {
            if (!CanStartNewSession())
                return;

            if (!IsSteamAvailable)
            {
                SetNotice("notice.steamUnavailable");
                return;
            }

            // 호스트 자신의 접속도 암호 확인을 거치므로 참가 암호도 같게 둔다.
            RoomPassword = NormalizePassword(password);
            JoinPassword = RoomPassword;
            IsBusy = true;
            SetNotice("notice.creatingRoom");
            steamLobby.CreateLobby(hasPassword: RoomPassword.Length > 0);
        }

        /// <summary>
        /// 방 코드(로비 ID)로 로비에 들어가고, 입장하면 입력한 암호와 함께 호스트에 접속한다.
        /// </summary>
        public void JoinSteamRoom(string roomCode, string password)
        {
            if (!CanStartNewSession())
                return;

            RoomPassword = string.Empty;
            JoinPassword = NormalizePassword(password);

            if (!IsSteamAvailable)
            {
                SetNotice("notice.steamUnavailable");
                return;
            }

            if (!ulong.TryParse(roomCode?.Trim(), out ulong lobbyId))
            {
                SetNotice("notice.invalidCode");
                return;
            }

            IsBusy = true;
            SetNotice("notice.joiningRoom");
            steamLobby.JoinLobby(lobbyId);
        }

        /// <summary>
        /// Tugboat만으로 방을 만든다.
        /// </summary>
        public void HostDirectRoom()
        {
            StartDirectRoom(isTestRoom: false);
        }

        /// <inheritdoc />
        public void HostTestRoom()
        {
            StartDirectRoom(isTestRoom: true);
        }

        /// <summary>
        /// Tugboat만으로 방을 만든다. 테스트 방이면 서버가 시작되기 전에 표시해 방(RoomState)이 읽을 수 있게 한다.
        /// </summary>
        private void StartDirectRoom(bool isTestRoom)
        {
            if (!CanStartNewSession())
                return;

            // 직접 방은 개발/LAN용이라 암호가 없다.
            RoomPassword = string.Empty;
            JoinPassword = string.Empty;
            IsSteamRoom = false;
            IsTestRoom = isTestRoom;
            if (connector.StartServer(withSteam: false))
                connector.StartLocalClient();
            else
            {
                IsTestRoom = false;
                SetNotice("notice.serverStartFailedPort");
            }
        }

        /// <summary>
        /// IP 주소로 직접 접속 방에 참가한다.
        /// </summary>
        public void JoinDirectRoom(string address)
        {
            if (!CanStartNewSession())
                return;

            RoomPassword = string.Empty;
            JoinPassword = string.Empty;
            IsSteamRoom = false;
            IsBusy = true;
            SetNotice("notice.joiningRoom");
            connector.StartDirectClient(address);
        }

        /// <summary>
        /// Steam 친구 초대 창을 연다.
        /// </summary>
        public void OpenInviteDialog()
        {
            if (IsSteamRoom)
                steamLobby.OpenInviteDialog();
        }

        /// <summary>
        /// Steam 로비의 참가 가능 여부를 바꾼다.
        /// </summary>
        public void SetRoomJoinable(bool joinable)
        {
            if (IsSteamRoom)
                steamLobby.SetJoinable(joinable);
        }

        /// <summary>
        /// 연결과 로비를 모두 정리한다. 로비 씬으로의 이동은 DefaultScene이 처리한다.
        /// </summary>
        public void Leave()
        {
            isLeaving = true;
            connector.StopAll();
            steamLobby.LeaveLobby();
            IsSteamRoom = false;
            IsTestRoom = false;
            IsBusy = false;
            RoomPassword = string.Empty;
            JoinPassword = string.Empty;
        }

        /// <summary>
        /// 앞뒤 공백을 지운 암호. 없으면 빈 문자열.
        /// </summary>
        private static string NormalizePassword(string password)
        {
            return string.IsNullOrWhiteSpace(password) ? string.Empty : password.Trim();
        }

        /// <summary>
        /// 안내 메시지를 지운다.
        /// </summary>
        public void ClearNotice()
        {
            SetNotice(string.Empty);
        }

        /// <summary>
        /// 안내 문구를 key(strings.csv)와 끼워 넣을 세부 정보로 정한다. 빈 key는 문구 없음.
        /// </summary>
        private void SetNotice(string key, string detail = "")
        {
            noticeKey = key ?? string.Empty;
            noticeDetail = detail ?? string.Empty;
        }

        /// <summary>
        /// 이미 방에 있거나 연결 중이면 새 작업을 막는다.
        /// </summary>
        private bool CanStartNewSession()
        {
            if (IsBusy || connector.IsServerRunning || connector.IsClientRunning)
            {
                SetNotice("notice.alreadyInRoom");
                return false;
            }

            isLeaving = false;
            IsTestRoom = false;
            rejectNoticeKey = null;
            rejectNoticeDetail = null;
            pendingModRejection = null;
            modRejection = null;
            SetNotice(string.Empty);
            // 지난 접속에서 따르던 호스트 모드 구성이 남아 있으면 내 설정으로 돌린다.
            ModService.Instance.ClearSessionMods();
            return true;
        }

        /// <summary>
        /// 로비가 만들어지면 서버와 로컬 클라이언트를 시작한다.
        /// </summary>
        private void OnLobbyCreated(ulong lobbyId)
        {
            IsSteamRoom = true;
            if (connector.StartServer(withSteam: true))
            {
                connector.StartLocalClient();
                return;
            }

            IsBusy = false;
            SetNotice("notice.serverStartFailed");
            steamLobby.LeaveLobby();
        }

        /// <summary>
        /// 다른 사람의 로비에 들어가면 호스트에 Steam으로 접속한다.
        /// 암호가 있는 방인데 암호를 입력하지 않았으면(초대로 들어온 경우 등) 접속하지 않고 암호를 요청한다.
        /// </summary>
        private void OnJoinedLobby(ulong lobbyId, ulong hostSteamId, bool hasPassword)
        {
            if (hasPassword && JoinPassword.Length == 0)
            {
                IsBusy = false;
                ModService.Instance.ClearSessionMods();
                steamLobby.LeaveLobby();
                SetNotice("notice.passwordRequired");
                PasswordRequired?.Invoke(lobbyId.ToString());
                return;
            }

            IsSteamRoom = true;
            if (!connector.StartSteamClient(hostSteamId))
            {
                IsBusy = false;
                ModService.Instance.ClearSessionMods();
                SetNotice("notice.hostConnectFailed");
                steamLobby.LeaveLobby();
            }
        }

        /// <summary>
        /// 친구 초대 수락 등으로 참가 요청이 오면, 다른 방에 있지 않을 때만 참가한다.
        /// </summary>
        private void OnJoinRequested(ulong lobbyId)
        {
            if (connector.IsClientRunning || connector.IsServerRunning)
            {
                SetNotice("notice.inviteWhileInRoom");
                return;
            }

            // 초대로 들어올 때는 암호를 모른다. 암호가 있는 방이면 로비 입장 후 암호를 요청한다.
            JoinSteamRoom(lobbyId.ToString(), string.Empty);
        }

        /// <summary>
        /// 로비 작업 실패를 안내한다.
        /// </summary>
        private void OnLobbyFailed(string reasonKey, string detail)
        {
            IsBusy = false;
            ModService.Instance.ClearSessionMods();
            SetNotice(reasonKey, detail);
        }

        /// <summary>
        /// 접속 성공 시 작업을 끝내고, 스스로 나가지 않았는데 끊기면 안내 메시지를 남긴다.
        /// (호스트가 방을 닫으면 게스트는 이 경로로 로비에 돌아간다)
        /// </summary>
        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                IsBusy = false;
                SetNotice(string.Empty);
                return;
            }

            if (args.ConnectionState != LocalConnectionState.Stopped)
                return;

            bool wasConnecting = IsBusy;
            IsBusy = false;
            steamLobby.LeaveLobby();
            // 접속 동안 따르던 호스트 모드 구성을 버리고 내 설정으로 돌아간다.
            ModService.Instance.ClearSessionMods();

            // 호스트가 이유를 알려 줬으면(버전 불일치 등) 그 이유로 안내한다.
            if (!string.IsNullOrEmpty(rejectNoticeKey))
                SetNotice(rejectNoticeKey, rejectNoticeDetail);
            else if (!isLeaving)
                SetNotice(wasConnecting ? "notice.joinFailed" : "notice.hostDisconnected");
            rejectNoticeKey = null;
            rejectNoticeDetail = null;
            // 모드가 달라 거부됐으면 로비 팝업이 부족한 모드를 보여주도록 남긴다.
            modRejection = pendingModRejection;
            pendingModRejection = null;

            // 호스트가 아닌 경우에만 여기서 정리한다. (호스트는 Leave에서 서버까지 정리)
            if (!connector.IsServerRunning)
            {
                IsSteamRoom = false;
                IsTestRoom = false;
            }
            isLeaving = false;
        }
    }
}
