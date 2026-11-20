using System.Collections.Generic;
using System.Text;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Map;
using GuardVsMannequin.Mods;
using GuardVsMannequin.Network;
using UnityEngine;

namespace GuardVsMannequin.Room
{
    /// <summary>룸 ↔ 게임 씬을 오가도 유지되도록 방 상태를 전역 네트워크 오브젝트로 서버 권한 관리·동기화한다.</summary>
    public class RoomState : NetworkBehaviour, IRoomInfo, ISceneFlow
    {
        // 씬 활성화 때 메인 스레드가 몇 초 멈출 수 있어 인정 시간을 알림 간격보다 넉넉히 둔 로딩 알림 간격·인정 시간(초).
        private const float LoadingReportInterval = 2f;
        private const float LoadingReportTimeout = 10f;

        // 모드 폴더를 다시 읽는 시간까지 포함한 게스트 모드 확인 대기 시간(초).
        private const float StartModCheckTimeout = 5f;
        private const int MaxMapNameLength = 48;

        // 호스트가 맵을 빠르게 넘겨도 디스크를 계속 읽지 않도록 둔 모드 재확인 최소 간격(초).
        private const float LocalModCheckInterval = 0.25f;

        [Tooltip("룸 씬 이름. (빌드 설정에 등록되어 있어야 함)")]
        [SerializeField] private string roomSceneName = "Room";

        [Tooltip("게임 시스템(상태 머신, 스폰, HUD, 씬 카메라) 씬 이름. 판을 시작하면 맵 씬과 함께 불러온다.")]
        [SerializeField] private string systemsSceneName = "Game_Systems";

        private readonly SyncVar<GameSettings> settings = new();
        private readonly SyncDictionary<int, RoomMember> members = new();
        private readonly SyncVar<bool> isInGame = new();
        // 게스트가 맵보다 먼저 받도록 판 시작 때 정하는 배치 시드.
        private readonly SyncVar<int> propLayoutSeed = new();
        // 게스트마다 맵 목록이 달라 번호 대신 쓰는 고른 맵의 ID·이름·필요한 모드.
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

        // 서버만 아는 테스트 방 여부.
        private bool isTestRoom;

        // 서버: 클라이언트별 "로딩 중" 알림 기록
        private readonly LoadingReportTracker loadingReports = new(LoadingReportTimeout);
        // 클라이언트: 다음 "로딩 중" 알림 시각
        private float nextLoadingReportTime;
        private RoomSceneSet scenes;

        /// <summary>맵 목록이 모드에 따라 바뀌므로 처음 쓸 때 등록소로 만든다.</summary>
        private RoomSceneSet Scenes => scenes ??= new RoomSceneSet(roomSceneName, systemsSceneName, ContentRegistry.Instance);

        /// <summary>룸 UI가 씬 참조 없이 방을 찾을 수 있도록 연다.</summary>
        public static RoomState Instance { get; private set; }

        public GameSettings Settings => settings.Value;

        /// <summary>키: 클라이언트 ID.</summary>
        public SyncDictionary<int, RoomMember> Members => members;

        /// <summary>룸 UI가 게임 중에는 조작을 막도록 노출한다.</summary>
        public bool IsInGame => isInGame.Value;

        /// <inheritdoc />
        public int PropLayoutSeed => propLayoutSeed.Value;

        /// <inheritdoc />
        public bool IsTestRoom => isTestRoom;

        /// <summary>게스트에게 무엇을 설치해야 하는지 보여 주려고 노출한다.</summary>
        public IReadOnlyList<MissingMod> LocalMissingMods => localMissingMods;

        /// <summary>호스트가 시작 버튼을 누르기 전에 경고를 보여 줄 수 있도록, 모드가 없는 게스트가 있는지 알려 준다.</summary>
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

        /// <summary>내 PC에 있는 맵이면 내 언어로, 없으면 정리한 호스트의 이름으로 보여 준다.</summary>
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

        /// <summary>로딩 화면을 언제 보여 줄지 정하려고 호스트가 맵을 불러오는 시간까지 포함해 알린다.</summary>
        public bool IsSceneTransitionPending => Scenes.IsTransitionPending(isInGame.Value);

        /// <summary>
        /// 방이 처음 만들어질 때 기본 설정과 고른 맵의 기본값을 넣고, 이 오브젝트보다 먼저 접속한 사람도 참가자로 넣는다.
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();
            isTestRoom = SessionServices.Session?.IsTestRoom ?? false;
            ApplySettings(MapSettingRules.ApplyDefaults(DefaultGameSettings.Load()));

            // 인증을 통과한 연결만 참가자로 넣는다.
            foreach (NetworkConnection connection in ServerManager.Clients.Values)
            {
                if (connection.IsAuthenticated)
                    AddMember(connection);
            }

            ServerManager.OnAuthenticationResult += OnAuthenticationResult;
            ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            ApplySettings(settings.Value);
        }

        /// <summary>
        /// 서버가 멈춘 뒤 콜백이 남지 않도록 구독을 끊는다.
        /// </summary>
        public override void OnStopServer()
        {
            base.OnStopServer();
            ServerManager.OnAuthenticationResult -= OnAuthenticationResult;
            ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        }

        /// <summary>게임 씬이 방을 찾도록 등록하고, 서버가 내 이름을 모르므로 알리며, 재접속이면 미뤄 둔 사물 준비를 한다.</summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            Instance = this;
            GameServices.RegisterRoom(this, this);
            MapPropSetup.RunPending();
            SubmitNameServerRpc(LocalProfile.DisplayName);
        }

        /// <summary>
        /// 연결이 끊긴 뒤 없어진 방을 다른 코드가 계속 참조하지 않도록 등록을 해제한다.
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();
            if (Instance == this)
                Instance = null;
            GameServices.UnregisterRoom(this);
        }

        /// <summary>호스트가 바꿔 보낼 수 있어 받는 쪽에서도 이름을 정리한다.</summary>
        public string GetDisplayName(int clientId)
        {
            string displayName = members.TryGetValue(clientId, out RoomMember member)
                ? DisplayTextSanitizer.Clean(member.displayName, DisplayTextSanitizer.MaxPlayerNameLength)
                : string.Empty;
            return displayName.Length > 0 ? displayName : $"Player {clientId}";
        }

        /// <summary>시작 버튼을 막을 때 이유를 보여 주도록 조건과 문구를 함께 돌려준다.</summary>
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

        /// <summary>호스트는 서버라 RPC 없이 직접 부르고, 맵을 바꾸면 그 맵의 기본값을 함께 적용한다.</summary>
        [Server]
        public void AdjustSetting(GameSettingId id, int direction)
        {
            if (isInGame.Value || !GameSettingDefinitions.IsShownInRoom(id))
                return;

            GameSettings adjusted = GameSettingDefinitions.Adjust(settings.Value, id, direction);
            if (id == GameSettingId.Map && adjusted.mapId != settings.Value.mapId)
                adjusted = MapSettingRules.ApplyDefaults(adjusted);
            ApplySettings(adjusted);
        }

        /// <summary>맵은 번호로 고르면 헷갈리므로 키보드 직접 입력을 막는다.</summary>
        [Server]
        public void SetSettingValue(GameSettingId id, float value)
        {
            if (isInGame.Value || id == GameSettingId.Map || !GameSettingDefinitions.IsShownInRoom(id))
                return;
            ApplySettings(GameSettingDefinitions.SetClamped(settings.Value, id, value));
        }

        /// <summary>게스트가 모드를 지웠으면 로딩 중에 실패하므로 다시 확인시킨 뒤 시작한다.</summary>
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

            PublishSelectedMap();
            startModCheck.Begin(Time.unscaledTime, StartModCheckTimeout);
            reason = Loc.Get("room.reason.checkingMods");
            return false;
        }

        /// <summary>
        /// 같은 안내가 매 프레임 다시 뜨지 않도록 꺼내면 지운다.
        /// </summary>
        public bool TryTakeHostNotice(out string notice)
        {
            notice = hostStartNotice;
            hostStartNotice = null;
            return !string.IsNullOrEmpty(notice);
        }

        /// <summary>설치 안내 창이 한 번만 열리도록 꺼내면 지운다.</summary>
        public bool TryTakeStartBlocked()
        {
            bool isBlocked = startBlockedPending;
            startBlockedPending = false;
            return isBlocked;
        }

        /// <summary>게스트가 모드를 새로 설치했을 수 있어 모드 폴더를 다시 읽어 서버에 알린다.</summary>
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

        /// <summary>판 도중 참가로 상태가 꼬이지 않도록 새 참가를 막고, 모든 PC가 같은 배치로 섞도록 시드를 정한 뒤 게임 씬으로 보낸다.</summary>
        [Server]
        private void BeginGame()
        {
            SessionServices.Session?.SetRoomInGame(true);
            loadingReports.Clear();
            isInGame.Value = true;
            propLayoutSeed.Value = Random.Range(1, int.MaxValue);
            LoadGameScenes(Scenes.ResolveGameScene(settings.Value.mapId));
        }

        /// <summary>맵을 빠르게 바꾸면 지난 맵의 답이 늦게 올 수 있어 차수가 다른 답은 무시한다.</summary>
        [ServerRpc(RequireOwnership = false)]
        private void ReportMapModsServerRpc(int revision, bool hasAll, NetworkConnection caller = null)
        {
            if (caller == null || revision != selectedMap.Value.revision)
                return;
            if (members.TryGetValue(caller.ClientId, out RoomMember member) && !member.isHost)
                SetMemberModState(caller.ClientId, hasAll ? MemberModState.Ready : MemberModState.Missing);
        }

        /// <summary>
        /// 모드가 없는 게스트가 무엇을 설치해야 하는지 바로 볼 수 있도록 알려 설치 안내 창을 열게 한다.
        /// </summary>
        [TargetRpc]
        private void NotifyStartBlockedTargetRpc(NetworkConnection target)
        {
            RecheckLocalMods();
            startBlockedPending = true;
        }

        /// <summary>지난 판의 준비 상태로 다음 판이 바로 시작되지 않도록 준비를 풀고 룸 씬으로 돌려보낸다.</summary>
        [Server]
        public void ReturnToRoom()
        {
            if (!isInGame.Value)
                return;

            isInGame.Value = false;
            loadingReports.Clear();
            ResetReadyStates();
            SessionServices.Session?.SetRoomInGame(false);
            LoadGlobalScene(Scenes.RoomSceneName);
        }

        /// <summary>내보낸 사람이 곧바로 다시 들어오지 못하도록 방이 닫힐 때까지 막는다.</summary>
        [Server]
        public void Kick(int clientId)
        {
            if (!members.TryGetValue(clientId, out RoomMember member) || member.isHost)
                return;
            if (!ServerManager.Clients.TryGetValue(clientId, out NetworkConnection connection))
                return;

            RoomMembership membership = SessionServices.Session?.Membership;
            if (membership != null && membership.TryGetKey(clientId, out string key))
                membership.Ban(key);

            ServerManager.Broadcast(connection, new ConnectionRejectedBroadcast { reason = ConnectionRejectReason.Kicked, hostVersion = GameVersion.Current }, false);
            connection.Disconnect(false);
        }

        /// <summary>
        /// 스스로 나간 사람에게 재접속 대기 자리를 주면 판이 끝나지 않고 기다리므로, 나가기 전에 알려 바로 탈락하게 한다.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void DeclareLeavingServerRpc(NetworkConnection caller = null)
        {
            RoomMembership membership = SessionServices.Session?.Membership;
            if (caller != null && membership != null && membership.TryGetKey(caller.ClientId, out string key))
                membership.MarkLeaving(key);
        }

        /// <summary>
        /// 호스트는 준비할 필요가 없으므로 게스트만 준비 상태를 바꿀 수 있다.
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
        /// 판 도중에 바꾸면 다음 판 배정과 헷갈리므로 룸에 있을 때만 받고, 같은 값이면 다시 보내지 않는다.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void SetTeamPreferenceServerRpc(TeamPreference preference, NetworkConnection caller = null)
        {
            if (caller == null || isInGame.Value || !members.TryGetValue(caller.ClientId, out RoomMember member))
                return;
            if (preference is not (TeamPreference.Random or TeamPreference.Guard or TeamPreference.Mannequin) || member.teamPreference == preference)
                return;

            member.teamPreference = preference;
            members[caller.ClientId] = member;
        }

        /// <inheritdoc />
        public TeamPreference GetTeamPreference(int clientId)
        {
            return members.TryGetValue(clientId, out RoomMember member) ? member.teamPreference : TeamPreference.Random;
        }

        /// <summary>
        /// 느린 PC를 끊긴 것으로 오인하지 않도록, 아직 씬을 불러오는 중이라는 알림을 받는 동안 서버가 판 시작을 기다린다.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void ReportLoadingServerRpc(NetworkConnection caller = null)
        {
            if (caller == null || !members.ContainsKey(caller.ClientId))
                return;
            loadingReports.Record(caller.ClientId, Time.unscaledTime);
        }

        /// <summary>판 시작 대기에서 느리지만 살아 있는 사람을 구분하려고 쓴다.</summary>
        public bool IsClientLoading(int clientId)
        {
            return loadingReports.IsLoading(clientId, Time.unscaledTime);
        }

        /// <summary>대역폭을 아끼려고 일정 간격으로만 알린다.</summary>
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
        /// 리치 텍스트 태그 등으로 다른 사람 화면을 깨뜨리지 못하도록 서버에서 정리한 뒤 저장한다.
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
        /// 인원이 늘면 가드 수 등의 허용 범위가 바뀌므로, 참가자로 넣은 뒤 설정을 다시 보정한다.
        /// </summary>
        private void OnAuthenticationResult(NetworkConnection connection, bool authenticated)
        {
            if (!authenticated)
                return;

            AddMember(connection);
            ApplySettings(settings.Value);
        }

        /// <summary>인원이 줄면 허용 범위가 바뀌므로 참가자에서 뺀 뒤 설정을 다시 보정한다.</summary>
        private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Stopped)
                return;

            loadingReports.Remove(connection.ClientId);
            if (members.Remove(connection.ClientId))
                ApplySettings(settings.Value);
        }

        /// <summary>
        /// 이미 있는 참가자를 다시 넣으면 준비 상태가 풀리므로 없을 때만 넣는다.
        /// </summary>
        private void AddMember(NetworkConnection connection)
        {
            if (members.ContainsKey(connection.ClientId))
                return;

            members[connection.ClientId] = new RoomMember
            {
                displayName = string.Empty,
                isReady = false,
                isHost = connection.IsLocalClient
            };
        }

        /// <summary>
        /// 서버·게스트가 할 일이 한 오브젝트에 모여 있어서, 각 작업이 스스로 조건을 확인하도록 매 프레임 차례로 부른다.
        /// </summary>
        private void Update()
        {
            ConfirmHostMember();
            UpdateStartModCheck();
            ReportLoadingIfPending();
            CheckLocalModsIfMapChanged();
        }

        /// <summary>접속 직후 처음 받은 맵도 같은 경로로 확인하도록 콜백 대신 차수를 비교한다.</summary>
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

        /// <summary>기다리는 동안 준비나 인원이 바뀌었을 수 있어 시작 조건을 한 번 더 본다.</summary>
        private void UpdateStartModCheck()
        {
            if (!startModCheck.IsRunning || !this.IsServerReady())
                return;

            switch (startModCheck.Evaluate(members.Values, Time.unscaledTime))
            {
                case StartModCheckResult.Passed:
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

        /// <summary>호스트가 누구를 기다려야 하는지 알도록 문제 있는 게스트 이름을 모아 안내한다.</summary>
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

        /// <summary>게스트가 불필요하게 다시 확인하지 않도록 맵이 바뀐 경우에만 맵 정보를 보낸다.</summary>
        private void SyncSelectedMap()
        {
            if (settings.Value.mapId != selectedMapIndex)
                PublishSelectedMap();
        }

        /// <summary>이전 맵 기준 답이 새 맵에 쓰이지 않도록 새 차수로 보내며 답과 시작 확인을 초기화한다.</summary>
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

        /// <summary>SyncDictionary는 다시 넣을 때마다 전송되므로 같은 값이면 보내지 않는다.</summary>
        private void SetMemberModState(int clientId, MemberModState state)
        {
            RoomMember member = members[clientId];
            if (member.modState == state)
                return;
            member.modState = state;
            members[clientId] = member;
        }

        /// <summary>
        /// 혼자 있을 때 모드 확인을 기다리면 시작이 늦어지기만 하므로, 게스트가 있는지 먼저 본다.
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

        /// <summary>호스트 로컬 연결은 인증 뒤에야 식별되어 등록 때 놓칠 수 있으므로 매 프레임 바로잡는다.</summary>
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

        /// <summary>인원이 줄어도 설정이 맞도록 맵 범위로 먼저 맞추고 관계 규칙을 마지막에 적용한다.</summary>
        private void ApplySettings(GameSettings newSettings)
        {
            settings.Value = GameSettingsValidator.Validate(MapSettingRules.Clamp(newSettings), Mathf.Max(GameConstants.MinPlayers, members.Count));
            SyncSelectedMap();
        }

        /// <summary>
        /// 판이 끝난 뒤 지난 준비 상태로 다음 판이 바로 시작되지 않도록 모두 해제한다.
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
        /// 이전 씬이 남아 겹치지 않도록 모든 플레이어에게 전역으로 불러오면서 이전 씬을 교체한다.
        /// </summary>
        private void LoadGlobalScene(string sceneName)
        {
            var loadData = new SceneLoadData(sceneName) { ReplaceScenes = ReplaceOption.All };
            SceneManager.LoadGlobalScenes(loadData);
        }

        /// <summary>맵의 하늘·안개·오클루전이 쓰이도록 맵 씬을 먼저 불러와 활성 씬으로 정한다.</summary>
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
