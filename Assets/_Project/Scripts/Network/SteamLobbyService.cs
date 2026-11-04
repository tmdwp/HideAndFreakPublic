using System;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using Steamworks;
using UnityEngine;

namespace GuardVsMannequin.Network
{
    /// <summary>
    /// Steam 로비(방 코드, 친구 초대)를 관리한다. 네트워크 연결 자체는 하지 않는다(SRP).
    /// - 방 코드 = Steam 로비 ID
    /// - 참가자는 로비 주인(Steam이 정하므로 위조할 수 없음)의 SteamID로 접속한다.
    /// - 공개방 목록에 필요한 값을 로비 데이터에 적는다. (<see cref="SteamLobbyKeys"/>, 검색은 <see cref="SteamRoomBrowser"/>)
    /// </summary>
    public class SteamLobbyService : MonoBehaviour
    {
        [SerializeField] private int maxMembers = 10;

        private CallResult<LobbyCreated_t> lobbyCreatedResult;
        private Callback<LobbyEnter_t> lobbyEnterCallback;
        private Callback<GameLobbyJoinRequested_t> joinRequestedCallback;
        private bool isCreatingLobby;
        private SteamRoomOptions creatingOptions;
        private bool pingLocationWritten;

        /// <summary>로비 생성이 끝났을 때. (로비 ID)</summary>
        public event Action<ulong> LobbyCreated;

        /// <summary>다른 사람의 로비에 들어갔을 때. (로비 ID, 호스트 SteamID, 암호가 있는 방인지)</summary>
        public event Action<ulong, ulong, bool> JoinedLobby;

        /// <summary>친구 초대를 수락하거나 친구 목록에서 참가를 눌렀을 때. (로비 ID)</summary>
        public event Action<ulong> JoinRequested;

        /// <summary>로비 작업이 실패했을 때. (사유 문구 key, 문구에 끼워 넣을 세부 정보 — 없으면 빈 문자열)</summary>
        public event Action<string, string> Failed;

        /// <summary>현재 들어가 있는 로비 ID. 없으면 0.</summary>
        public ulong CurrentLobbyId { get; private set; }

        /// <summary>
        /// Steam 콜백을 등록한다. Steam이 초기화되지 않았으면 아무것도 하지 않는다.
        /// </summary>
        private void Start()
        {
            if (!SteamBootstrap.IsInitialized)
                return;

            lobbyCreatedResult = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
            lobbyEnterCallback = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            joinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
            // 핑 위치를 구하려면 Steam 중계망 정보가 필요하다. 미리 받아 둔다.
            SteamNetworkingUtils.InitRelayNetworkAccess();

            CheckLaunchInvite();
        }

        /// <summary>
        /// 공개방 또는 친구 전용 로비를 만든다. 친구 전용은 Steam 친구와 초대받은 사람만 들어올 수 있고 목록에 나오지 않는다.
        /// 암호 자체는 로비 데이터에 적지 않는다(누구나 읽을 수 있으므로). 암호가 있다는 표시만 적고, 확인은 호스트가 접속 때 한다.
        /// </summary>
        public void CreateLobby(SteamRoomOptions options)
        {
            if (!SteamBootstrap.IsInitialized)
            {
                Failed?.Invoke("notice.steamNotRunning", string.Empty);
                return;
            }

            creatingOptions = options;
            isCreatingLobby = true;
            ELobbyType lobbyType = options.isPublic ? ELobbyType.k_ELobbyTypePublic : ELobbyType.k_ELobbyTypeFriendsOnly;
            SteamAPICall_t call = SteamMatchmaking.CreateLobby(lobbyType, maxMembers);
            lobbyCreatedResult.Set(call);
        }

        /// <summary>
        /// 방 코드(로비 ID)로 로비에 들어간다.
        /// </summary>
        public void JoinLobby(ulong lobbyId)
        {
            if (!SteamBootstrap.IsInitialized)
            {
                Failed?.Invoke("notice.steamNotRunning", string.Empty);
                return;
            }

            SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
        }

        /// <summary>
        /// 현재 로비에서 나간다.
        /// </summary>
        public void LeaveLobby()
        {
            if (CurrentLobbyId != 0 && SteamBootstrap.IsInitialized)
                SteamMatchmaking.LeaveLobby(new CSteamID(CurrentLobbyId));
            CurrentLobbyId = 0;
        }

        /// <summary>
        /// 새 참가자를 받을지 정한다. (게임 진행 중에는 막는다)
        /// </summary>
        public void SetJoinable(bool joinable)
        {
            if (CurrentLobbyId != 0 && SteamBootstrap.IsInitialized)
                SteamMatchmaking.SetLobbyJoinable(new CSteamID(CurrentLobbyId), joinable);
        }

        /// <summary>
        /// 고른 맵과 최대 인원을 로비 데이터와 인원 상한에 맞춘다. 지금 인원보다 낮게는 줄이지 않는다. (호스트)
        /// </summary>
        public void UpdateListing(string mapId, int maxPlayers)
        {
            if (CurrentLobbyId == 0 || !SteamBootstrap.IsInitialized)
                return;

            CSteamID lobby = new CSteamID(CurrentLobbyId);
            SteamMatchmaking.SetLobbyData(lobby, SteamLobbyKeys.Map, mapId ?? string.Empty);
            SteamMatchmaking.SetLobbyData(lobby, SteamLobbyKeys.MaxPlayers, maxPlayers.ToString());
            SteamMatchmaking.SetLobbyMemberLimit(lobby, Mathf.Max(maxPlayers, SteamMatchmaking.GetNumLobbyMembers(lobby)));
            WritePingLocation(lobby);
        }

        /// <summary>현재 로비가 공개방인지 여부. (호스트와 게스트 모두)</summary>
        public bool IsCurrentLobbyPublic => CurrentLobbyId != 0 && SteamBootstrap.IsInitialized
            && SteamMatchmaking.GetLobbyData(new CSteamID(CurrentLobbyId), SteamLobbyKeys.IsPublic) == "1";

        /// <summary>
        /// 해당 SteamID가 현재 로비의 멤버인지 확인한다. (호스트가 접속자를 검증할 때 사용)
        /// </summary>
        public bool IsLobbyMember(ulong steamId)
        {
            if (CurrentLobbyId == 0 || !SteamBootstrap.IsInitialized)
                return false;

            CSteamID lobby = new CSteamID(CurrentLobbyId);
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobby);
            for (int i = 0; i < memberCount; i++)
            {
                if (SteamMatchmaking.GetLobbyMemberByIndex(lobby, i).m_SteamID == steamId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Steam 오버레이의 친구 초대 창을 연다.
        /// </summary>
        public void OpenInviteDialog()
        {
            if (CurrentLobbyId != 0 && SteamBootstrap.IsInitialized)
                SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(CurrentLobbyId));
        }

        /// <summary>
        /// 로비 생성 결과를 받아 호스트 주소를 기록한다.
        /// </summary>
        private void OnLobbyCreated(LobbyCreated_t result, bool ioFailure)
        {
            isCreatingLobby = false;
            if (ioFailure || result.m_eResult != EResult.k_EResultOK)
            {
                Failed?.Invoke("notice.lobbyCreateFailed", result.m_eResult.ToString());
                return;
            }

            CurrentLobbyId = result.m_ulSteamIDLobby;
            CSteamID lobby = new CSteamID(CurrentLobbyId);
            SteamMatchmaking.SetLobbyData(lobby, SteamLobbyKeys.Game, SteamLobbyKeys.GameValue);
            // 참가자가 호스트에 접속하기 전에 버전을 비교할 수 있게 적어 둔다.
            SteamMatchmaking.SetLobbyData(lobby, SteamLobbyKeys.Version, GameVersion.Current);
            SteamMatchmaking.SetLobbyData(lobby, SteamLobbyKeys.HasPassword, string.IsNullOrEmpty(creatingOptions.password) ? "0" : "1");
            SteamMatchmaking.SetLobbyData(lobby, SteamLobbyKeys.IsPublic, creatingOptions.isPublic ? "1" : "0");
            SteamMatchmaking.SetLobbyData(lobby, SteamLobbyKeys.Name, ResolveRoomName(creatingOptions.roomName));
            pingLocationWritten = false;
            WritePingLocation(lobby);
            LobbyCreated?.Invoke(CurrentLobbyId);
        }

        /// <summary>
        /// 방 이름을 정리한다. 비어 있으면 "&lt;Steam 이름&gt;의 방".
        /// </summary>
        private static string ResolveRoomName(string roomName)
        {
            string cleaned = DisplayTextSanitizer.Clean(roomName, SteamRoomOptions.MaxNameLength);
            if (cleaned.Length > 0)
                return cleaned;

            string steamName = DisplayTextSanitizer.Clean(SteamFriends.GetPersonaName(), DisplayTextSanitizer.MaxPlayerNameLength);
            return DisplayTextSanitizer.Clean(Loc.Format("lobby.roomName.default", steamName), SteamRoomOptions.MaxNameLength);
        }

        /// <summary>
        /// 호스트의 핑 위치를 적는다. Steam 중계망 정보가 아직 없으면 다음 갱신 때 다시 시도한다.
        /// </summary>
        private void WritePingLocation(CSteamID lobby)
        {
            if (pingLocationWritten || SteamNetworkingUtils.GetLocalPingLocation(out SteamNetworkPingLocation_t location) < 0f)
                return;

            SteamNetworkingUtils.ConvertPingLocationToString(ref location, out string text, Constants.k_cchMaxSteamNetworkingPingLocationString);
            SteamMatchmaking.SetLobbyData(lobby, SteamLobbyKeys.PingLocation, text);
            pingLocationWritten = true;
        }

        /// <summary>
        /// 로비 입장 결과를 받는다. 내가 만든 로비가 아니면 호스트 주소로 접속하도록 알린다.
        /// </summary>
        private void OnLobbyEntered(LobbyEnter_t result)
        {
            CSteamID lobby = new CSteamID(result.m_ulSteamIDLobby);
            if (result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Failed?.Invoke("notice.lobbyEnterFailed", string.Empty);
                return;
            }

            CurrentLobbyId = result.m_ulSteamIDLobby;

            // 방을 만든 호스트도 입장 콜백을 받는다. 호스트는 이미 서버를 시작했으므로 무시한다.
            if (isCreatingLobby || SteamMatchmaking.GetLobbyOwner(lobby) == SteamUser.GetSteamID())
                return;

            // 버전이 다르면 접속하지 않고 나온다. (버전을 적지 않은 옛 빌드의 방도 다른 버전으로 본다)
            string hostVersion = SteamMatchmaking.GetLobbyData(lobby, SteamLobbyKeys.Version);
            if (hostVersion != GameVersion.Current)
            {
                string shownVersion = string.IsNullOrEmpty(hostVersion) ? "?" : hostVersion;
                Failed?.Invoke("notice.rejected.version", $"{shownVersion} ≠ {GameVersion.Current}");
                LeaveLobby();
                return;
            }

            // 로비 데이터에 적힌 주소가 아니라 Steam이 알려 주는 로비 주인에게 접속한다. (로비 데이터는 주인이 마음대로 적을 수 있다)
            ulong hostSteamId = SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID;
            if (hostSteamId == 0)
            {
                Failed?.Invoke("notice.lobbyDataInvalid", string.Empty);
                LeaveLobby();
                return;
            }

            bool hasPassword = SteamMatchmaking.GetLobbyData(lobby, SteamLobbyKeys.HasPassword) == "1";
            JoinedLobby?.Invoke(CurrentLobbyId, hostSteamId, hasPassword);
        }

        /// <summary>
        /// 게임 실행 중 친구 초대를 수락했을 때 호출된다.
        /// </summary>
        private void OnJoinRequested(GameLobbyJoinRequested_t request)
        {
            JoinRequested?.Invoke(request.m_steamIDLobby.m_SteamID);
        }

        /// <summary>
        /// 게임이 꺼진 상태에서 초대를 수락해 실행된 경우(+connect_lobby 인자) 그 로비로 들어간다.
        /// </summary>
        private void CheckLaunchInvite()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out ulong lobbyId))
                {
                    JoinRequested?.Invoke(lobbyId);
                    return;
                }
            }
        }
    }
}
