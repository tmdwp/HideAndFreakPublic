using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Mods;
using GuardVsMannequin.Network;
using UnityEngine;

namespace GuardVsMannequin.Room
{
    /// <summary>
    /// 방 하나의 상태(참가자, 준비 여부, 게임 설정)를 서버 권한으로 관리하고 동기화한다.
    /// 전역(Global) 네트워크 오브젝트라 룸 ↔ 게임 씬을 오가도 유지되며, 서버가 멈추면 함께 사라진다.
    /// 게임 씬에는 <see cref="IRoomInfo"/>, <see cref="ISceneFlow"/>로만 노출된다.
    /// </summary>
    public class RoomState : NetworkBehaviour, IRoomInfo, ISceneFlow
    {
        private const int MaxDisplayNameLength = 24;

        // 로딩 중인 클라이언트가 "아직 불러오는 중"을 알리는 간격과, 마지막 알림 후 로딩 중으로 인정하는 시간(초).
        // 씬 활성화 순간에는 메인 스레드가 몇 초 멈출 수 있으므로 인정 시간을 간격보다 넉넉히 둔다.
        private const float LoadingReportInterval = 2f;
        private const float LoadingReportTimeout = 10f;

        [Tooltip("룸 씬 이름. (빌드 설정에 등록되어 있어야 함)")]
        [SerializeField] private string roomSceneName = "Room";

        [Tooltip("게임 시스템(상태 머신, 스폰, HUD, 씬 카메라) 씬 이름. 판을 시작하면 맵 씬과 함께 불러온다.")]
        [SerializeField] private string systemsSceneName = "Game_Systems";

        private readonly SyncVar<GameSettings> settings = new();
        private readonly SyncDictionary<int, RoomMember> members = new();
        private readonly SyncVar<bool> isInGame = new();
        // 판을 시작할 때 정한다. 서버는 맵을 다 불러온 뒤에야 게스트에게 로딩을 지시하므로, 게스트는 맵보다 이 값을 먼저 받는다.
        private readonly SyncVar<int> propLayoutSeed = new();

        // 서버: 테스트 방 여부 (방을 만들 때 세션에서 읽음, 게스트에게는 동기화하지 않음)
        private bool isTestRoom;

        // 서버: 클라이언트별 "로딩 중" 알림 기록
        private readonly LoadingReportTracker loadingReports = new(LoadingReportTimeout);
        // 클라이언트: 다음 "로딩 중" 알림 시각
        private float nextLoadingReportTime;
        private RoomSceneSet scenes;

        /// <summary>룸·게임 시스템·맵 씬 이름 처리. 맵 목록은 등록소(내장 맵 + 켜진 모드)에서 읽는다.</summary>
        private RoomSceneSet Scenes => scenes ??= new RoomSceneSet(roomSceneName, systemsSceneName, ContentRegistry.Instance);

        /// <summary>현재 방. 없으면 null. (서버와 클라이언트 모두)</summary>
        public static RoomState Instance { get; private set; }

        public GameSettings Settings => settings.Value;

        /// <summary>참가자 목록. (키: 클라이언트 ID)</summary>
        public SyncDictionary<int, RoomMember> Members => members;

        /// <summary>게임 씬에 있는지 여부. (룸 UI 표시용)</summary>
        public bool IsInGame => isInGame.Value;

        /// <inheritdoc />
        public int PropLayoutSeed => propLayoutSeed.Value;

        /// <inheritdoc />
        public bool IsTestRoom => isTestRoom;

        /// <summary>
        /// 이 클라이언트가 아직 목표 씬(게임 중이면 맵, 아니면 룸)에 도착하지 못했는지 여부. (로딩 화면 표시용)
        /// 게임 시작 직후에는 호스트가 맵을 다 불러올 때까지 게스트에게 로딩 지시가 오지 않으므로, 그 시간도 포함된다.
        /// </summary>
        public bool IsSceneTransitionPending => Scenes.IsTransitionPending(isInGame.Value);

        /// <summary>
        /// 서버에서 기본 설정을 적용하고, 이미 접속한 클라이언트를 참가자로 등록한다.
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();
            isTestRoom = SessionServices.Session?.IsTestRoom ?? false;
            // 방의 초기 설정은 Resources/Data/default_game_settings.json을 따른다.
            // 고른 맵(기본 맵)이 정한 기본값·범위도 적용한다. (map.json)
            ApplySettings(MapSettingRules.ApplyDefaults(DefaultGameSettings.Load()));

            // 접속 검증(RoomAuthenticator)을 통과한 연결만 참가자로 등록한다.
            foreach (NetworkConnection conn in ServerManager.Clients.Values)
            {
                if (conn.IsAuthenticated)
                    AddMember(conn);
            }

            ServerManager.OnAuthenticationResult += OnAuthenticationResult;
            ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            ApplySettings(settings.Value);
        }

        /// <summary>
        /// 접속 상태 구독을 해제한다.
        /// </summary>
        public override void OnStopServer()
        {
            base.OnStopServer();
            ServerManager.OnAuthenticationResult -= OnAuthenticationResult;
            ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        }

        /// <summary>
        /// 클라이언트에서 방을 등록하고, 서버에 내 표시 이름을 알린다.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            Instance = this;
            GameServices.RegisterRoom(this, this);
            SubmitNameServerRpc(LocalProfile.DisplayName);
        }

        /// <summary>
        /// 클라이언트에서 방 등록을 해제한다.
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();
            if (Instance == this)
                Instance = null;
            GameServices.UnregisterRoom(this);
        }

        /// <summary>
        /// 클라이언트의 표시 이름을 반환한다.
        /// </summary>
        public string GetDisplayName(int clientId)
        {
            return members.TryGetValue(clientId, out RoomMember member) && !string.IsNullOrEmpty(member.displayName)
                ? member.displayName
                : $"Player {clientId}";
        }

        /// <summary>
        /// 게임을 시작할 수 있는지와 그 이유를 반환한다. (호스트 제외 전원 준비 + 인원 조건, 테스트 방은 인원 조건 없음)
        /// </summary>
        public bool CanStartGame(out string reason)
        {
            if (!isTestRoom && !GameSettingsValidator.CanStart(members.Count))
            {
                reason = Loc.Format("room.reason.playerCount", GameConstants.MinPlayers, GameConstants.MaxPlayers, members.Count);
                return false;
            }
            if (!isTestRoom && !MapSettingRules.AllowsPlayerCount(settings.Value, members.Count, out int mapMinPlayers, out int mapMaxPlayers))
            {
                reason = Loc.Format("room.reason.mapPlayerCount", mapMinPlayers, mapMaxPlayers, members.Count);
                return false;
            }

            foreach (RoomMember member in members.Values)
            {
                if (!member.isHost && !member.isReady)
                {
                    reason = Loc.Get("room.reason.notAllReady");
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// 설정 항목을 한 단계 조정한다. (호스트 전용, 호스트는 서버이므로 직접 호출)
        /// </summary>
        [Server]
        public void AdjustSetting(GameSettingId id, int direction)
        {
            if (isInGame.Value || !GameSettingDefinitions.IsShownInRoom(id))
                return;

            GameSettings adjusted = GameSettingDefinitions.Adjust(settings.Value, id, direction);
            // 맵을 바꾸면 그 맵이 정한 기본값을 적용한다.
            if (id == GameSettingId.Map && adjusted.mapId != settings.Value.mapId)
                adjusted = MapSettingRules.ApplyDefaults(adjusted);
            ApplySettings(adjusted);
        }

        /// <summary>
        /// 설정 항목에 값을 직접 지정한다. 항목 범위와 인원 조건으로 보정된다. (호스트 전용, 키보드 입력)
        /// </summary>
        [Server]
        public void SetSettingValue(GameSettingId id, float value)
        {
            if (isInGame.Value || id == GameSettingId.Map || !GameSettingDefinitions.IsShownInRoom(id))
                return;
            ApplySettings(GameSettingDefinitions.SetClamped(settings.Value, id, value));
        }

        /// <summary>
        /// 조건을 확인하고 모든 플레이어를 게임 씬으로 보낸다. (호스트 전용)
        /// </summary>
        [Server]
        public bool TryStartGame(out string reason)
        {
            if (isInGame.Value)
            {
                reason = Loc.Get("room.reason.inGame");
                return false;
            }
            if (!CanStartGame(out reason))
                return false;

            // 게임 중에는 새 참가를 막는다.
            SessionServices.Session?.SetRoomJoinable(false);
            loadingReports.Clear();
            isInGame.Value = true;
            // 맵 씬을 불러오기 전에 정해, 모든 PC가 맵을 불러오자마자 같은 배치로 섞게 한다. (0은 "섞지 않음"이라 피함)
            propLayoutSeed.Value = Random.Range(1, int.MaxValue);
            LoadGameScenes(Scenes.ResolveGameScene(settings.Value.mapId));
            return true;
        }

        /// <summary>
        /// 게임이 끝나면 준비 상태를 초기화하고 모든 플레이어를 룸 씬으로 돌려보낸다. (서버 전용)
        /// </summary>
        [Server]
        public void ReturnToRoom()
        {
            if (!isInGame.Value)
                return;

            isInGame.Value = false;
            loadingReports.Clear();
            ResetReadyStates();
            SessionServices.Session?.SetRoomJoinable(true);
            LoadGlobalScene(Scenes.RoomSceneName);
        }

        /// <summary>
        /// 게스트가 준비/해제를 요청한다.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void SetReadyServerRpc(bool ready, NetworkConnection caller = null)
        {
            if (caller == null || !members.TryGetValue(caller.ClientId, out RoomMember member) || member.isHost)
                return;

            member.isReady = ready;
            members[caller.ClientId] = member;
        }

        /// <summary>
        /// 클라이언트가 아직 씬을 불러오는 중임을 알린다. 서버는 이 알림이 이어지는 동안 판 시작을 기다린다.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void ReportLoadingServerRpc(NetworkConnection caller = null)
        {
            if (caller == null || !members.ContainsKey(caller.ClientId))
                return;
            loadingReports.Record(caller.ClientId, Time.unscaledTime);
        }

        /// <summary>
        /// 클라이언트가 최근에 "로딩 중"을 알렸는지 확인한다. (서버 전용)
        /// </summary>
        public bool IsClientLoading(int clientId)
        {
            return loadingReports.IsLoading(clientId, Time.unscaledTime);
        }

        /// <summary>
        /// 게스트가 씬 전환 중이면 일정 간격으로 서버에 "로딩 중"을 알린다. (호스트는 서버가 직접 불러오므로 제외)
        /// </summary>
        private void ReportLoadingIfPending()
        {
            if (!IsClientStarted || IsServerStarted || !IsSceneTransitionPending)
                return;
            if (Time.unscaledTime < nextLoadingReportTime)
                return;

            nextLoadingReportTime = Time.unscaledTime + LoadingReportInterval;
            ReportLoadingServerRpc();
        }

        /// <summary>
        /// 클라이언트가 자신의 표시 이름을 알린다. 비어 있으면 "Player N"을 쓴다.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void SubmitNameServerRpc(string displayName, NetworkConnection caller = null)
        {
            if (caller == null || !members.TryGetValue(caller.ClientId, out RoomMember member))
                return;

            string trimmed = SanitizeDisplayName(displayName);
            member.displayName = trimmed.Length > MaxDisplayNameLength ? trimmed.Substring(0, MaxDisplayNameLength) : trimmed;
            members[caller.ClientId] = member;
        }

        /// <summary>
        /// 표시 이름에서 UI 리치 텍스트 태그로 쓰일 수 있는 꺾쇠와 제어 문자를 제거한다.
        /// (이름에 &lt;size=200&gt; 같은 태그를 넣어 다른 사람의 화면을 깨뜨리는 것을 막는다)
        /// </summary>
        private static string SanitizeDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return string.Empty;

            var builder = new System.Text.StringBuilder(displayName.Length);
            foreach (char c in displayName)
            {
                if (c == '<' || c == '>' || char.IsControl(c))
                    continue;
                builder.Append(c);
            }
            return builder.ToString().Trim();
        }

        /// <summary>
        /// 접속 검증을 통과하면 참가자로 등록하고, 인원에 맞게 설정을 다시 보정한다.
        /// </summary>
        private void OnAuthenticationResult(NetworkConnection conn, bool authenticated)
        {
            if (!authenticated)
                return;

            AddMember(conn);
            ApplySettings(settings.Value);
        }

        /// <summary>
        /// 접속이 끊기면 참가자에서 제거하고, 인원에 맞게 설정을 다시 보정한다.
        /// </summary>
        private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Stopped)
                return;

            loadingReports.Remove(conn.ClientId);
            if (members.Remove(conn.ClientId))
                ApplySettings(settings.Value);
        }

        /// <summary>
        /// 참가자를 등록한다. 호스트 자신의 연결은 호스트로 표시한다.
        /// </summary>
        private void AddMember(NetworkConnection conn)
        {
            if (members.ContainsKey(conn.ClientId))
                return;

            members[conn.ClientId] = new RoomMember
            {
                displayName = string.Empty,
                isReady = false,
                isHost = conn.IsLocalClient
            };
        }

        /// <summary>
        /// 매 프레임 호스트 표시를 확정하고, 게스트라면 로딩 상태를 알린다.
        /// </summary>
        private void Update()
        {
            ConfirmHostMember();
            ReportLoadingIfPending();
        }

        /// <summary>
        /// 서버에서 호스트 표시를 확정한다.
        /// 호스트의 로컬 연결은 인증이 끝난 뒤에야 IsLocalClient로 식별되므로, 참가자 등록 시점에 놓칠 수 있다.
        /// </summary>
        private void ConfirmHostMember()
        {
            if (!this.IsServerReady() || !ClientManager.Started)
                return;

            NetworkConnection hostConnection = ClientManager.Connection;
            if (hostConnection == null || !hostConnection.IsValid)
                return;

            if (members.TryGetValue(hostConnection.ClientId, out RoomMember host) && !host.isHost)
            {
                host.isHost = true;
                host.isReady = false;
                members[hostConnection.ClientId] = host;
            }
        }

        /// <summary>
        /// 현재 인원 기준으로 설정을 보정해 적용한다. (인원이 줄면 가드 수 등이 자동으로 줄어듦)
        /// </summary>
        private void ApplySettings(GameSettings newSettings)
        {
            // 맵 범위로 먼저 맞추고, 인원에 따른 관계 규칙(가드 수, 목표 < 전원 장치 등)을 마지막에 적용한다.
            settings.Value = GameSettingsValidator.Validate(MapSettingRules.Clamp(newSettings), Mathf.Max(GameConstants.MinPlayers, members.Count));
        }

        /// <summary>
        /// 모든 게스트의 준비 상태를 해제한다.
        /// </summary>
        private void ResetReadyStates()
        {
            foreach (int clientId in new System.Collections.Generic.List<int>(members.Keys))
            {
                RoomMember member = members[clientId];
                member.isReady = false;
                members[clientId] = member;
            }
        }

        /// <summary>
        /// 모든 플레이어에게 씬을 전역으로 불러오고 이전 씬을 교체한다.
        /// </summary>
        private void LoadGlobalScene(string sceneName)
        {
            var loadData = new SceneLoadData(sceneName) { ReplaceScenes = ReplaceOption.All };
            SceneManager.LoadGlobalScenes(loadData);
        }

        /// <summary>
        /// 맵 씬과 게임 시스템 씬을 함께 불러와 룸 씬을 교체한다. 맵 씬을 먼저 불러오고 활성 씬으로 정해
        /// 맵의 하늘·환경광·안개 설정과 오클루전 데이터가 쓰이게 한다.
        /// </summary>
        private void LoadGameScenes(string mapSceneName)
        {
            var loadData = new SceneLoadData(new[] { mapSceneName, systemsSceneName })
            {
                ReplaceScenes = ReplaceOption.All,
                PreferredActiveScene = new PreferredScene(new SceneLookupData(mapSceneName))
            };
            SceneManager.LoadGlobalScenes(loadData);
        }
    }
}
