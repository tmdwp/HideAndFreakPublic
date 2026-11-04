using System.Collections.Generic;
using System.Text;
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
        // 로딩 중인 클라이언트가 "아직 불러오는 중"을 알리는 간격과, 마지막 알림 후 로딩 중으로 인정하는 시간(초).
        // 씬 활성화 순간에는 메인 스레드가 몇 초 멈출 수 있으므로 인정 시간을 간격보다 넉넉히 둔다.
        private const float LoadingReportInterval = 2f;
        private const float LoadingReportTimeout = 10f;

        // 시작을 누른 뒤 게스트들이 모드 확인 결과를 보낼 때까지 기다리는 시간(초). 모드 폴더를 다시 읽는 시간을 포함한다.
        private const float StartModCheckTimeout = 5f;
        private const int MaxMapNameLength = 48;

        // 게스트가 맵 변경으로 모드 폴더를 다시 읽는 최소 간격(초). 호스트가 맵을 빠르게 넘겨도 디스크를 계속 읽지 않게 한다.
        private const float LocalModCheckInterval = 0.25f;

        [Tooltip("룸 씬 이름. (빌드 설정에 등록되어 있어야 함)")]
        [SerializeField] private string roomSceneName = "Room";

        [Tooltip("게임 시스템(상태 머신, 스폰, HUD, 씬 카메라) 씬 이름. 판을 시작하면 맵 씬과 함께 불러온다.")]
        [SerializeField] private string systemsSceneName = "Game_Systems";

        private readonly SyncVar<GameSettings> settings = new();
        private readonly SyncDictionary<int, RoomMember> members = new();
        private readonly SyncVar<bool> isInGame = new();
        // 판을 시작할 때 정한다. 서버는 맵을 다 불러온 뒤에야 게스트에게 로딩을 지시하므로, 게스트는 맵보다 이 값을 먼저 받는다.
        private readonly SyncVar<int> propLayoutSeed = new();
        // 고른 맵의 ID·이름·필요한 모드. 게스트마다 맵 목록이 달라 GameSettings.mapId(호스트 목록의 번호)로는 알 수 없다.
        private readonly SyncVar<RoomMapInfo> selectedMap = new();

        // 서버: 시작 전 모드 확인
        private readonly StartModCheck startModCheck = new();
        // 서버: selectedMap을 만든 맵 번호와 마지막 확인 차수
        private int selectedMapIndex = -1;
        private int lastMapRevision;
        // 서버: 호스트 화면에 한 번 보여 줄 안내
        private string hostStartNotice;

        // 클라이언트: 마지막으로 확인한 차수와 이 PC에서 쓸 수 없는 모드
        private int checkedMapRevision;
        private float nextLocalModCheckTime;
        private List<MissingMod> localMissingMods = new();
        // 클라이언트: 모드가 없어 시작이 중단됐다는 알림을 아직 화면에 보여 주지 않음
        private bool startBlockedPending;

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

        /// <summary>이 PC에서 쓸 수 없는, 고른 맵의 모드. 비어 있으면 플레이할 수 있다. (게스트)</summary>
        public IReadOnlyList<MissingMod> LocalMissingMods => localMissingMods;

        /// <summary>고른 맵의 모드가 없는 게스트가 있는지 여부.</summary>
        public bool HasMembersMissingMods
        {
            get
            {
                foreach (RoomMember member in members.Values)
                {
                    if (!member.isHost && member.modState == MemberModState.Missing)
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 고른 맵의 표시 이름. 내 PC에 있는 맵이면 내 언어로, 없으면 호스트가 보낸 이름을 정리해 보여 준다.
        /// </summary>
        public string SelectedMapDisplayName
        {
            get
            {
                RoomMapInfo map = selectedMap.Value;
                ContentRegistry registry = ContentRegistry.Instance;
                if (registry.TryGetMap(registry.IndexOf(map.mapId), out MapEntry entry))
                    return entry.DisplayName;

                string hostName = DisplayTextSanitizer.Clean(map.displayName, MaxMapNameLength);
                return hostName.Length > 0 ? hostName : "?";
            }
        }

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
            // 서버가 정리해 보내지만, 호스트가 바꿔 보낼 수 있어 받는 쪽에서도 정리한다.
            string name = members.TryGetValue(clientId, out RoomMember member)
                ? DisplayTextSanitizer.Clean(member.displayName, DisplayTextSanitizer.MaxPlayerNameLength)
                : string.Empty;
            return name.Length > 0 ? name : $"Player {clientId}";
        }

        /// <summary>
        /// 게임을 시작할 수 있는지와 그 이유를 반환한다. (호스트 제외 전원 준비 + 인원 조건, 테스트 방은 인원 조건 없음)
        /// </summary>
        public bool CanStartGame(out string reason)
        {
            if (startModCheck.IsRunning)
            {
                reason = Loc.Get("room.reason.checkingMods");
                return false;
            }
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
        /// 모드 맵이면 바로 시작하지 않고 게스트들에게 모드를 다시 확인시킨 뒤, 전원이 가지고 있을 때 시작한다.
        /// </summary>
        /// <param name="reason">바로 시작하지 않았을 때 보여 줄 문구.</param>
        /// <returns>바로 시작했는지 여부.</returns>
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

            RoomMapInfo map = selectedMap.Value;
            if (map.requiredMods == null || map.requiredMods.Length == 0 || !HasGuests())
            {
                BeginGame();
                return true;
            }

            // 맵을 고른 뒤 게스트가 모드를 지웠거나 새로 설치했을 수 있으므로 지금 다시 확인한다.
            PublishSelectedMap();
            startModCheck.Begin(Time.unscaledTime, StartModCheckTimeout);
            reason = Loc.Get("room.reason.checkingMods");
            return false;
        }

        /// <summary>
        /// 호스트 화면에 한 번 보여 줄 안내를 꺼낸다. 꺼내면 지워진다.
        /// </summary>
        public bool TryTakeHostNotice(out string notice)
        {
            notice = hostStartNotice;
            hostStartNotice = null;
            return !string.IsNullOrEmpty(notice);
        }

        /// <summary>
        /// 모드가 없어 시작이 중단됐다는 알림을 꺼낸다. 꺼내면 지워진다. (게스트의 설치 안내 창)
        /// </summary>
        public bool TryTakeStartBlocked()
        {
            bool blocked = startBlockedPending;
            startBlockedPending = false;
            return blocked;
        }

        /// <summary>
        /// 모드 폴더를 다시 읽어 고른 맵의 모드를 확인하고 결과를 서버에 알린다. (게스트, 맵이 바뀌었을 때와 "다시 확인"을 눌렀을 때)
        /// </summary>
        public void RecheckLocalMods()
        {
            if (!IsClientStarted || IsServerStarted)
                return;

            RoomMapInfo map = selectedMap.Value;
            checkedMapRevision = map.revision;
            localMissingMods = ModService.Instance.UseRequiredMods(map.requiredMods);
            bool hasAll = localMissingMods.Count == 0 && (map.requiredMods == null || map.requiredMods.Length <= RequiredModComparer.MaxRequiredMods);
            ReportMapModsServerRpc(map.revision, hasAll);
        }

        /// <summary>
        /// 모든 플레이어를 게임 씬으로 보낸다.
        /// </summary>
        [Server]
        private void BeginGame()
        {
            // 게임 중에는 새 참가를 막는다.
            SessionServices.Session?.SetRoomJoinable(false);
            loadingReports.Clear();
            isInGame.Value = true;
            // 맵 씬을 불러오기 전에 정해, 모든 PC가 맵을 불러오자마자 같은 배치로 섞게 한다. (0은 "섞지 않음"이라 피함)
            propLayoutSeed.Value = Random.Range(1, int.MaxValue);
            LoadGameScenes(Scenes.ResolveGameScene(settings.Value.mapId));
        }

        /// <summary>
        /// 게스트가 고른 맵의 모드를 가지고 있는지 알린다. 지난 확인 차수의 답은 무시한다.
        /// 게스트가 거짓으로 답할 수 있지만, 그래도 그 게스트만 맵을 불러오지 못한다.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void ReportMapModsServerRpc(int revision, bool hasAll, NetworkConnection caller = null)
        {
            if (caller == null || revision != selectedMap.Value.revision)
                return;
            if (members.TryGetValue(caller.ClientId, out RoomMember member) && !member.isHost)
                SetMemberModState(caller.ClientId, hasAll ? MemberModState.Ready : MemberModState.Missing);
        }

        /// <summary>
        /// 시작이 중단됐음을 모드가 없는 게스트에게 알린다. 게스트는 설치 안내 창을 연다.
        /// </summary>
        [TargetRpc]
        private void NotifyStartBlockedTargetRpc(NetworkConnection target)
        {
            RecheckLocalMods();
            startBlockedPending = true;
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

            member.displayName = DisplayTextSanitizer.Clean(displayName, DisplayTextSanitizer.MaxPlayerNameLength);
            members[caller.ClientId] = member;
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
            // 시작 확인 중 나간 사람은 판정에서 자연스럽게 빠진다.
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
        /// 매 프레임 호스트 표시를 확정하고 시작 전 모드 확인을 진행한다. 게스트라면 로딩 상태를 알리고 맵의 모드를 확인한다.
        /// </summary>
        private void Update()
        {
            ConfirmHostMember();
            UpdateStartModCheck();
            ReportLoadingIfPending();
            CheckLocalModsIfMapChanged();
        }

        /// <summary>
        /// 게스트: 확인 차수가 바뀌었으면 맵의 모드를 확인한다. (접속 직후 처음 받은 맵도 여기서 확인)
        /// </summary>
        private void CheckLocalModsIfMapChanged()
        {
            if (!IsClientStarted || IsServerStarted || isInGame.Value)
                return;

            int revision = selectedMap.Value.revision;
            if (revision == 0 || revision == checkedMapRevision || Time.unscaledTime < nextLocalModCheckTime)
                return;

            nextLocalModCheckTime = Time.unscaledTime + LocalModCheckInterval;
            RecheckLocalMods();
        }

        /// <summary>
        /// 서버: 시작 전 모드 확인 결과를 판정해 게임을 시작하거나 중단한다.
        /// </summary>
        private void UpdateStartModCheck()
        {
            if (!startModCheck.IsRunning || !this.IsServerReady())
                return;

            switch (startModCheck.Evaluate(members.Values, Time.unscaledTime))
            {
                case StartModCheckResult.Passed:
                    // 기다리는 동안 준비를 풀었거나 인원이 바뀌었을 수 있다.
                    if (CanStartGame(out string reason))
                        BeginGame();
                    else
                        hostStartNotice = reason;
                    break;
                case StartModCheckResult.Failed:
                    BlockStart();
                    break;
            }
        }

        /// <summary>
        /// 서버: 모드가 없거나 답하지 않은 게스트에게 알리고, 호스트에게 누구 때문인지 안내한다.
        /// </summary>
        private void BlockStart()
        {
            var names = new StringBuilder();
            foreach (int clientId in new List<int>(members.Keys))
            {
                RoomMember member = members[clientId];
                if (member.isHost || member.modState == MemberModState.Ready)
                    continue;

                SetMemberModState(clientId, MemberModState.Missing);
                if (names.Length > 0)
                    names.Append(", ");
                names.Append(GetDisplayName(clientId));
                if (ServerManager.Clients.TryGetValue(clientId, out NetworkConnection connection))
                    NotifyStartBlockedTargetRpc(connection);
            }
            hostStartNotice = Loc.Format("room.reason.modsMissing", names.ToString());
        }

        /// <summary>
        /// 서버: 지금 맵이 바뀌었으면 맵 정보를 새로 보낸다.
        /// </summary>
        private void SyncSelectedMap()
        {
            if (settings.Value.mapId != selectedMapIndex)
                PublishSelectedMap();
        }

        /// <summary>
        /// 서버: 고른 맵의 정보를 새 확인 차수로 보내고 게스트들의 답을 초기화한다. 진행 중인 시작 확인은 취소된다.
        /// </summary>
        private void PublishSelectedMap()
        {
            selectedMapIndex = settings.Value.mapId;
            ContentRegistry.Instance.TryGetMap(selectedMapIndex, out MapEntry map);
            selectedMap.Value = new RoomMapInfo
            {
                mapId = map?.Id ?? string.Empty,
                displayName = map?.DisplayName ?? string.Empty,
                requiredMods = ModService.Instance.BuildRequirements(map).ToArray(),
                revision = ++lastMapRevision
            };

            startModCheck.Cancel();
            foreach (int clientId in new List<int>(members.Keys))
            {
                if (!members[clientId].isHost)
                    SetMemberModState(clientId, MemberModState.Unknown);
            }

            MapSettingRules.AllowsPlayerCount(settings.Value, 0, out _, out int maxPlayers);
            SessionServices.Session?.UpdateRoomListing(selectedMap.Value.mapId, maxPlayers);
        }

        /// <summary>
        /// 서버: 참가자의 모드 상태를 바꾼다. 같은 값이면 보내지 않는다.
        /// </summary>
        private void SetMemberModState(int clientId, MemberModState state)
        {
            RoomMember member = members[clientId];
            if (member.modState == state)
                return;
            member.modState = state;
            members[clientId] = member;
        }

        /// <summary>
        /// 호스트 말고 다른 참가자가 있는지 확인한다.
        /// </summary>
        private bool HasGuests()
        {
            foreach (RoomMember member in members.Values)
            {
                if (!member.isHost)
                    return true;
            }
            return false;
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
            SyncSelectedMap();
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
