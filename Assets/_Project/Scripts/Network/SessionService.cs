using System;
using FishNet.Transporting;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Mods;
using UnityEngine;

namespace GuardVsMannequin.Network
{
    /// <summary>UI가 연결 순서를 몰라도 되도록 Steam 로비와 트랜스포트 연결을 묶어 방을 만들고 참가하고 나간다.</summary>
    public class SessionService : MonoBehaviour, ISessionService
    {
        [SerializeField] private TransportConnector connector;
        [SerializeField] private SteamLobbyService steamLobby;

        private bool isLeaving;
        private bool isRegistered;
        private SteamRoomBrowser roomBrowser;

        // 끊겼을 때 돌아갈 곳
        private sealed class RejoinTarget
        {
            public ulong lobbyId;
            public string address;
            public float deadline;
            public float autoAttemptTime;
        }

        // 로비 씬으로 돌아갈 시간을 주는 자동 재시도 대기 시간(초).
        private const float AutoRejoinDelay = 1.5f;

        private RejoinTarget rejoinTarget;
        private bool wasInMatch;
        private string directAddress = string.Empty;

        // 연결이 끊길 때 안내에 쓸 호스트의 거부 이유.
        private string rejectNoticeKey;
        private string rejectNoticeDetail;

        // 호스트가 방을 닫는다는 알림이 게스트에게 도착할 시간(초). 바로 끊으면 알림이 전송되지 못하고 사라진다.
        private const float RoomClosingNoticeSeconds = 0.3f;

        // 게스트: 호스트가 방을 닫는다고 알렸는지. 이 뒤의 끊김은 재참가 대상이 아니다.
        private bool isRoomClosedByHost;

        // 호스트: 닫는다는 알림을 이미 보냈는지. 종료 요청을 한 번 미룬 뒤 다시 받으면 그대로 종료한다.
        private bool isClosingNoticeSent;

        // 게스트: 지금 접속 시도가 자동 재참가인지. 자동 시도마저 실패하면 호스트가 사라진 것으로 본다.
        private bool isAutoRejoinAttempt;

        // 언어를 바꾸면 바로 반영되도록 안내 문구는 key와 값으로 보관한다.
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

        public bool IsPublicRoom => IsSteamRoom && steamLobby.IsCurrentLobbyPublic;

        /// <inheritdoc />
        public IRoomBrowser RoomBrowser => IsSteamAvailable
            ? roomBrowser ??= new SteamRoomBrowser(() => steamLobby.CurrentLobbyId, ModService.Instance.HasMap)
            : null;

        /// <inheritdoc />
        public RoomMembership Membership { get; private set; } = new();

        /// <inheritdoc />
        public string ClientToken { get; } = Guid.NewGuid().ToString("N");

        /// <inheritdoc />
        public float RejoinSecondsLeft => rejoinTarget == null ? 0f : Mathf.Max(0f, rejoinTarget.deadline - Time.unscaledTime);

        public string RoomPassword { get; private set; } = string.Empty;
        public string JoinPassword { get; private set; } = string.Empty;

        public event Action<string> PasswordRequired;

        /// <summary>
        /// 로비 씬을 다시 불러올 때 생기는 중복이 연결 상태를 가진 기존 서비스와 섞이지 않도록 첫 인스턴스만 등록하고 이벤트를 건다.
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

        /// <summary>하위 매니저가 NetworkManager.Awake 이후에 준비되므로 연결 이벤트와 검증기는 Start에서 연결한다.</summary>
        private void Start()
        {
            if (!isRegistered)
                return;

            connector.NetworkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            connector.NetworkManager.ClientManager.RegisterBroadcast<ConnectionRejectedBroadcast>(OnConnectionRejected);
            connector.NetworkManager.ClientManager.RegisterBroadcast<RoomClosingBroadcast>(OnRoomClosing);
            Application.wantsToQuit += OnWantsToQuit;
            SetupAuthenticator();
            ModSceneProcessor.Install(connector.NetworkManager);
        }

        /// <summary>아무나 들어오지 못하도록 검증기를 서버에 붙이고, 프리팹에 없으면 직접 추가한다.</summary>
        private void SetupAuthenticator()
        {
            if (!TryGetComponent(out RoomAuthenticator authenticator))
                authenticator = gameObject.AddComponent<RoomAuthenticator>();

            authenticator.Configure(this, steamLobby);
            connector.NetworkManager.ServerManager.SetAuthenticator(authenticator);
        }

        /// <summary>
        /// 중복 인스턴스는 구독한 적이 없으므로 등록된 인스턴스일 때만 이벤트를 끊는다.
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
            Application.wantsToQuit -= OnWantsToQuit;
            if (connector != null && connector.NetworkManager != null)
            {
                connector.NetworkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
                connector.NetworkManager.ClientManager.UnregisterBroadcast<ConnectionRejectedBroadcast>(OnConnectionRejected);
                connector.NetworkManager.ClientManager.UnregisterBroadcast<RoomClosingBroadcast>(OnRoomClosing);
            }
        }

        /// <summary>
        /// 이 알림 뒤에 끊기면 돌아갈 방이 없으므로 기억해 두고 재참가를 만들지 않는다.
        /// </summary>
        private void OnRoomClosing(RoomClosingBroadcast message, Channel channel)
        {
            if (!connector.IsServerRunning)
                isRoomClosedByHost = true;
        }

        /// <summary>
        /// 호스트가 창을 닫으면 게스트가 끊긴 이유를 몰라 재참가를 기다리므로, 종료를 잠깐 미루고 방을 닫는다고 먼저 알린다.
        /// </summary>
        /// <returns>지금 바로 종료해도 되면 true.</returns>
        private bool OnWantsToQuit()
        {
            if (isClosingNoticeSent || !TrySendRoomClosing())
                return true;

            StartCoroutine(QuitAfterClosingNotice());
            return false;
        }

        /// <summary>
        /// 알림이 전송될 시간을 준 뒤 원래 하려던 종료를 마저 한다.
        /// </summary>
        private System.Collections.IEnumerator QuitAfterClosingNotice()
        {
            yield return new WaitForSecondsRealtime(RoomClosingNoticeSeconds);
            Application.Quit();
        }

        /// <summary>게스트가 있는 방의 호스트일 때만 방을 닫는다고 알린다.</summary>
        /// <returns>알림을 보냈으면 true.</returns>
        private bool TrySendRoomClosing()
        {
            if (!connector.IsServerRunning || connector.NetworkManager.ServerManager.Clients.Count <= 1)
                return false;

            connector.NetworkManager.ServerManager.Broadcast(new RoomClosingBroadcast(), false);
            isClosingNoticeSent = true;
            return true;
        }

        /// <summary>
        /// 거부 알림 직후 연결이 끊기는데, 끊김 처리에서 이유를 보여 줄 수 있도록 미리 기억해 둔다.
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
                ConnectionRejectReason.TooManyPasswordAttempts => ("notice.rejected.passwordLocked", string.Empty),
                ConnectionRejectReason.Kicked => ("notice.rejected.kicked", string.Empty),
                ConnectionRejectReason.Banned => ("notice.rejected.banned", string.Empty),
                // 새 이유가 생겼는데 옛 빌드가 모르면 버전 차이일 가능성이 높아서 버전 안내로 보여 준다.
                _ => ("notice.rejected.version", $"{message.hostVersion} ≠ {GameVersion.Current}")
            };
        }

        /// <summary>방 코드를 주려면 로비 ID가 필요해 Steam 로비를 먼저 만든 뒤 서버를 시작한다.</summary>
        public void HostSteamRoom(SteamRoomOptions options)
        {
            if (!CanStartNewSession())
                return;

            if (!IsSteamAvailable)
            {
                SetNotice("notice.steamUnavailable");
                return;
            }

            RoomPassword = NormalizePassword(options.password);
            JoinPassword = RoomPassword;
            IsBusy = true;
            SetNotice("notice.creatingRoom");
            options.password = RoomPassword;
            steamLobby.CreateLobby(options);
        }

        /// <inheritdoc />
        public void UpdateRoomListing(string mapId, int maxPlayers)
        {
            if (IsSteamRoom)
                steamLobby.UpdateListing(mapId, maxPlayers);
        }

        /// <summary>
        /// 호스트 SteamID는 로비에 들어가야 알 수 있으므로 로비부터 들어가고, 입장하면 입력한 암호와 함께 호스트에 접속한다.
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
        /// Steam 없이 개발·LAN에서 시험할 수 있도록 Tugboat만으로 방을 만든다.
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

        /// <summary>방이 서버 시작 직후 테스트 방 여부를 읽으므로 서버 시작 전에 표시해 둔다.</summary>
        private void StartDirectRoom(bool isTestRoom)
        {
            if (!CanStartNewSession())
                return;

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
        /// 끊겼을 때 같은 주소로 다시 들어갈 수 있도록 주소를 기억해 두고 접속한다.
        /// </summary>
        public void JoinDirectRoom(string address)
        {
            if (!CanStartNewSession())
                return;

            RoomPassword = string.Empty;
            JoinPassword = string.Empty;
            IsSteamRoom = false;
            IsBusy = true;
            directAddress = address ?? string.Empty;
            SetNotice("notice.joiningRoom");
            connector.StartDirectClient(address);
        }

        /// <summary>
        /// 직접 방은 Steam 로비가 없어 초대할 수 없으므로 Steam 방일 때만 연다.
        /// </summary>
        public void OpenInviteDialog()
        {
            if (IsSteamRoom)
                steamLobby.OpenInviteDialog();
        }

        /// <summary>공개 목록에서 판 중인 방을 숨기려고 Steam 로비에 적는다.</summary>
        public void SetRoomInGame(bool inGame)
        {
            if (IsSteamRoom)
                steamLobby.SetInGame(inGame);
        }

        /// <summary>끊긴 뒤에는 판 정보가 사라지므로 연결 중에 판 진행 여부를 기억하고 끊긴 뒤 재접속을 처리한다.</summary>
        private void Update()
        {
            if (!isRegistered)
                return;

            SimulateDisconnectIfRequested();

            if (connector.IsClientRunning && GameServices.Room != null)
            {
                wasInMatch = GameServices.Room.IsInGame && !connector.IsServerRunning;
                // 방 정보가 다시 왔으면 돌아온 것이다.
                rejoinTarget = null;
                return;
            }

            if (rejoinTarget == null || IsBusy || connector.IsClientRunning)
                return;

            if (Time.unscaledTime >= rejoinTarget.deadline)
            {
                DiscardRejoin();
                SetNotice("notice.rejoinExpired");
            }
            else if (rejoinTarget.autoAttemptTime > 0f && Time.unscaledTime >= rejoinTarget.autoAttemptTime)
            {
                rejoinTarget.autoAttemptTime = 0f;
                Rejoin();
                isAutoRejoinAttempt = IsBusy;
            }
        }

        /// <summary>PC 1대로 끊김을 시험하도록 개발 빌드에서 F9로 알림 없이 연결만 끊는다.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void SimulateDisconnectIfRequested()
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f9Key.wasPressedThisFrame && connector.IsClientRunning && !connector.IsServerRunning)
            {
                Debug.Log("[SessionService] 시험용 연결 끊기 (F9)");
                connector.NetworkManager.ClientManager.StopConnection();
            }
        }

        /// <inheritdoc />
        public void Rejoin()
        {
            if (rejoinTarget == null || IsBusy || connector.IsClientRunning || connector.IsServerRunning)
                return;

            rejoinTarget.autoAttemptTime = 0f;
            isLeaving = false;
            IsBusy = true;
            SetNotice("notice.joiningRoom");
            if (rejoinTarget.lobbyId != 0)
            {
                // 이미 로비에 있어도 입장 결과가 다시 오고, 그 결과로 로비 주인에게 접속한다.
                steamLobby.JoinLobby(rejoinTarget.lobbyId);
            }
            else
            {
                IsSteamRoom = false;
                connector.StartDirectClient(rejoinTarget.address);
            }
        }

        /// <inheritdoc />
        public void DiscardRejoin()
        {
            if (rejoinTarget == null)
                return;

            rejoinTarget = null;
            steamLobby.LeaveLobby();
            ModService.Instance.ClearSessionMods();
        }

        /// <inheritdoc />
        public void LeaveAfterDelay(float seconds)
        {
            isLeaving = true;
            StartCoroutine(LeaveAfter(seconds));
        }

        /// <summary>
        /// 일시정지 중에도 나갈 수 있도록 실제 시간으로 기다린다.
        /// </summary>
        private System.Collections.IEnumerator LeaveAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            Leave();
        }

        /// <summary>
        /// 다음 방을 깨끗하게 시작하도록 연결·로비·암호·재접속 대상을 모두 비운다.
        /// 게스트가 있는 호스트는 재참가를 기다리지 않게 방을 닫는다고 먼저 알리고 잠시 뒤 닫는다.
        /// </summary>
        public void Leave()
        {
            if (!isClosingNoticeSent && TrySendRoomClosing())
            {
                isLeaving = true;
                StartCoroutine(LeaveAfter(RoomClosingNoticeSeconds));
                return;
            }

            isClosingNoticeSent = false;
            isLeaving = true;
            rejoinTarget = null;
            wasInMatch = false;
            connector.StopAll();
            steamLobby.LeaveLobby();
            IsSteamRoom = false;
            IsTestRoom = false;
            IsBusy = false;
            RoomPassword = string.Empty;
            JoinPassword = string.Empty;
        }

        /// <inheritdoc />
        public void LeaveWithNotice(string noticeKey)
        {
            Leave();
            SetNotice(noticeKey);
        }

        /// <summary>복사한 암호의 앞뒤 공백으로 틀리지 않도록 지운다.</summary>
        private static string NormalizePassword(string password)
        {
            return string.IsNullOrWhiteSpace(password) ? string.Empty : password.Trim();
        }

        /// <summary>
        /// 읽은 안내가 다음에 로비를 열 때 또 뜨지 않도록 지운다.
        /// </summary>
        public void ClearNotice()
        {
            SetNotice(string.Empty);
        }

        /// <summary>언어를 바꾸면 바로 반영되도록 완성된 문장 대신 key와 값으로 보관한다.</summary>
        private void SetNotice(string key, string detail = "")
        {
            noticeKey = key ?? string.Empty;
            noticeDetail = detail ?? string.Empty;
        }

        /// <summary>트랜스포트가 꼬이지 않도록 연결 중 재시작을 막고, 지난 접속 기록을 비운다.</summary>
        private bool CanStartNewSession()
        {
            if (IsBusy || connector.IsServerRunning || connector.IsClientRunning)
            {
                SetNotice("notice.alreadyInRoom");
                return false;
            }

            DiscardRejoin();
            wasInMatch = false;
            Membership = new RoomMembership();
            isLeaving = false;
            IsTestRoom = false;
            rejectNoticeKey = null;
            rejectNoticeDetail = null;
            SetNotice(string.Empty);
            ModService.Instance.ClearSessionMods();
            return true;
        }

        /// <summary>로비가 생겨야 방 코드를 줄 수 있어 이때 서버를 열고, 실패하면 빈 로비가 남지 않게 나간다.</summary>
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

        /// <summary>초대로 들어오면 암호를 모르므로 암호 있는 방이면 로비에 암호 입력을 요청한다.</summary>
        private void OnJoinedLobby(ulong lobbyId, ulong hostSteamId, bool hasPassword)
        {
            if (hasPassword && JoinPassword.Length == 0)
            {
                IsBusy = false;
                rejoinTarget = null;
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
                SetNotice("notice.hostConnectFailed");
                if (AbandonFailedAutoRejoin())
                    return;
                if (rejoinTarget != null)
                    return;
                ModService.Instance.ClearSessionMods();
                steamLobby.LeaveLobby();
            }
        }

        /// <summary>지금 연결이 끊기지 않도록 다른 방에 있지 않을 때만 초대로 참가한다.</summary>
        private void OnJoinRequested(ulong lobbyId)
        {
            if (connector.IsClientRunning || connector.IsServerRunning)
            {
                SetNotice("notice.inviteWhileInRoom");
                return;
            }

            JoinSteamRoom(lobbyId.ToString(), string.Empty);
        }

        /// <summary>
        /// 재접속 중이 아니면 이번 시도를 위해 바꾼 맵 모드 구성을 되돌리고 이유를 안내한다.
        /// </summary>
        private void OnLobbyFailed(string reasonKey, string detail)
        {
            IsBusy = false;
            if (AbandonFailedAutoRejoin())
                return;
            if (rejoinTarget == null)
                ModService.Instance.ClearSessionMods();
            SetNotice(reasonKey, detail);
        }

        /// <summary>
        /// 끊긴 직후의 자동 재참가조차 호스트에 닿지 않으면 호스트가 게임을 끈 것으로 보고, 다시 참가 버튼을 남기지 않는다.
        /// </summary>
        /// <returns>자동 재참가 실패로 정리했으면 true.</returns>
        private bool AbandonFailedAutoRejoin()
        {
            if (!isAutoRejoinAttempt)
                return false;
            isAutoRejoinAttempt = false;
            DiscardRejoin();
            SetNotice("notice.hostClosedRoom");
            return true;
        }

        /// <summary>끊긴 이유에 따라 다른 안내를 보이고, 다시 참가할 수 있는 동안은 같은 맵을 위해 구성을 남긴다.</summary>
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
            bool isRejected = !string.IsNullOrEmpty(rejectNoticeKey);
            bool isRoomGone = isRoomClosedByHost || (wasConnecting && isAutoRejoinAttempt);
            isRoomClosedByHost = false;
            isAutoRejoinAttempt = false;
            // 호스트가 방을 닫았거나 자동 재참가조차 닿지 않으면 돌아갈 방이 없으므로 재참가를 띄우지 않는다.
            if (isRoomGone && !isRejected)
            {
                rejoinTarget = null;
                rejectNoticeKey = "notice.hostClosedRoom";
                rejectNoticeDetail = string.Empty;
                isRejected = true;
            }
            UpdateRejoinTarget(isRejected);
            wasInMatch = false;
            if (rejoinTarget == null)
            {
                steamLobby.LeaveLobby();
                ModService.Instance.ClearSessionMods();
            }

            if (isRejected)
                SetNotice(rejectNoticeKey, rejectNoticeDetail);
            else if (rejoinTarget != null)
                SetNotice(wasConnecting ? "notice.rejoinFailed" : "notice.rejoinAvailable");
            else if (!isLeaving)
                SetNotice(wasConnecting ? "notice.joinFailed" : "notice.hostDisconnected");
            rejectNoticeKey = null;
            rejectNoticeDetail = null;

            // 호스트는 Leave에서 서버까지 정리하므로 여기서는 게스트일 때만 방 표시를 지운다.
            if (!connector.IsServerRunning)
            {
                IsSteamRoom = false;
                IsTestRoom = false;
            }
            isLeaving = false;
        }

        /// <summary>게임 중 예상치 못하게 끊긴 게스트만 같은 판에 돌아갈 의미가 있어 그때만 돌아갈 곳을 만든다.</summary>
        private void UpdateRejoinTarget(bool isRejected)
        {
            if (isRejected || isLeaving || connector.IsServerRunning)
            {
                rejoinTarget = null;
                return;
            }
            if (rejoinTarget != null || !wasInMatch)
                return;

            rejoinTarget = new RejoinTarget
            {
                lobbyId = IsSteamRoom ? steamLobby.CurrentLobbyId : 0,
                address = directAddress,
                deadline = Time.unscaledTime + GameTuning.RejoinGraceSeconds,
                autoAttemptTime = Time.unscaledTime + AutoRejoinDelay
            };
        }
    }
}
