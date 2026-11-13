using System;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using Steamworks;
using UnityEngine;

namespace GuardVsMannequin.Network
{
    /// <summary>네트워크 연결과 나누려고 Steam 로비(방 코드, 친구 초대)만 관리한다.</summary>
    public class SteamLobbyService : MonoBehaviour
    {
        [SerializeField] private int maxMembers = 10;

        private CallResult<LobbyCreated_t> lobbyCreatedResult;
        private Callback<LobbyEnter_t> lobbyEnterCallback;
        private Callback<GameLobbyJoinRequested_t> joinRequestedCallback;
        private bool isCreatingLobby;
        private SteamRoomOptions creatingOptions;
        private bool isPingLocationWritten;

        /// <summary>로비 ID가 생겨야 서버를 열 수 있어 생성 완료를 알린다.</summary>
        public event Action<ulong> LobbyCreated;

        /// <summary>호스트에 접속할 수 있도록 접속 정보를 함께 알린다.</summary>
        public event Action<ulong, ulong, bool> JoinedLobby;

        /// <summary>친구 초대를 수락하거나 친구 목록에서 참가를 눌렀음을 알린다.</summary>
        public event Action<ulong> JoinRequested;

        /// <summary>로비에 실패 이유를 보여 주도록 문구 key와 세부 정보를 알린다.</summary>
        public event Action<string, string> Failed;

        /// <summary>없으면 0.</summary>
        public ulong CurrentLobbyId { get; private set; }

        /// <summary>Steam이 없으면 콜백을 만들 수 없어 초기화됐을 때만 등록하고 핑 계산용 중계망 정보를 미리 받는다.</summary>
        private void Start()
        {
            if (!SteamBootstrap.IsInitialized)
                return;

            lobbyCreatedResult = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
            lobbyEnterCallback = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            joinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
            SteamNetworkingUtils.InitRelayNetworkAccess();

            CheckLaunchInvite();
        }

        /// <summary>로비 데이터는 누구나 읽을 수 있으므로 암호 자체는 적지 않고 공개방 또는 친구 전용 로비를 만든다.</summary>
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
        /// 입장 결과는 콜백으로 오므로 여기서는 요청만 보낸다.
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
        /// 나간 뒤에도 친구 목록에 그 방에 있는 것으로 보이지 않도록 Steam 로비에서도 나간다.
        /// </summary>
        public void LeaveLobby()
        {
            if (CurrentLobbyId != 0 && SteamBootstrap.IsInitialized)
                SteamMatchmaking.LeaveLobby(new CSteamID(CurrentLobbyId));
            CurrentLobbyId = 0;
        }

        /// <summary>판 중인 방이 목록에 나오지 않게 적되, 끊긴 사람이 돌아오도록 입장은 막지 않는다.</summary>
        public void SetInGame(bool inGame)
        {
            if (CurrentLobbyId != 0 && SteamBootstrap.IsInitialized)
                SteamMatchmaking.SetLobbyData(new CSteamID(CurrentLobbyId), SteamLobbyKeys.State, inGame ? SteamLobbyKeys.StateInGame : SteamLobbyKeys.StateRoom);
        }

        /// <summary>목록에 맵과 인원 상한이 보이도록 맞추되, 이미 들어온 사람이 쫓겨나지 않게 지금 인원보다 낮게는 줄이지 않는다.</summary>
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

        /// <summary>게스트는 로비 종류를 직접 알 수 없어 로비 데이터의 값을 읽는다.</summary>
        public bool IsCurrentLobbyPublic => CurrentLobbyId != 0 && SteamBootstrap.IsInitialized
            && SteamMatchmaking.GetLobbyData(new CSteamID(CurrentLobbyId), SteamLobbyKeys.IsPublic) == "1";

        /// <summary>
        /// 방 코드 없이 SteamID로 바로 접속하는 사람을 막으려고, 호스트가 접속자를 검증할 때 로비 멤버인지 확인한다.
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
        /// 게임 안에 친구 목록 UI를 따로 만들지 않으려고 Steam 오버레이의 초대 창을 쓴다.
        /// </summary>
        public void OpenInviteDialog()
        {
            if (CurrentLobbyId != 0 && SteamBootstrap.IsInitialized)
                SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(CurrentLobbyId));
        }

        /// <summary>목록과 참가자가 접속 전에 판단할 수 있도록 필요한 값을 로비 데이터에 적는다.</summary>
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
            SteamMatchmaking.SetLobbyData(lobby, SteamLobbyKeys.State, SteamLobbyKeys.StateRoom);
            SteamMatchmaking.SetLobbyData(lobby, SteamLobbyKeys.Version, GameVersion.Current);
            SteamMatchmaking.SetLobbyData(lobby, SteamLobbyKeys.HasPassword, string.IsNullOrEmpty(creatingOptions.password) ? "0" : "1");
            SteamMatchmaking.SetLobbyData(lobby, SteamLobbyKeys.IsPublic, creatingOptions.isPublic ? "1" : "0");
            SteamMatchmaking.SetLobbyData(lobby, SteamLobbyKeys.Name, ResolveRoomName(creatingOptions.roomName));
            isPingLocationWritten = false;
            WritePingLocation(lobby);
            LobbyCreated?.Invoke(CurrentLobbyId);
        }

        /// <summary>
        /// 방 이름은 목록에서 다른 사람에게 보이므로 정리하고, 비어 있으면 누구 방인지 알 수 있게 "&lt;Steam 이름&gt;의 방"으로 채운다.
        /// </summary>
        private static string ResolveRoomName(string roomName)
        {
            string cleanedName = DisplayTextSanitizer.Clean(roomName, SteamRoomOptions.MaxNameLength);
            if (cleanedName.Length > 0)
                return cleanedName;

            string steamName = DisplayTextSanitizer.Clean(SteamFriends.GetPersonaName(), DisplayTextSanitizer.MaxPlayerNameLength);
            return DisplayTextSanitizer.Clean(Loc.Format("lobby.roomName.default", steamName), SteamRoomOptions.MaxNameLength);
        }

        /// <summary>목록에서 핑을 보여 주도록 핑 위치를 적고, 중계망 정보가 없으면 다음 갱신 때 다시 시도한다.</summary>
        private void WritePingLocation(CSteamID lobby)
        {
            if (isPingLocationWritten || SteamNetworkingUtils.GetLocalPingLocation(out SteamNetworkPingLocation_t location) < 0f)
                return;

            SteamNetworkingUtils.ConvertPingLocationToString(ref location, out string text, Constants.k_cchMaxSteamNetworkingPingLocationString);
            SteamMatchmaking.SetLobbyData(lobby, SteamLobbyKeys.PingLocation, text);
            isPingLocationWritten = true;
        }

        /// <summary>
        /// 방을 만든 호스트도 입장 콜백을 받는데 이미 서버를 열었으므로 무시하고, 남의 로비면 버전을 먼저 비교한 뒤 접속을 알린다.
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

            if (isCreatingLobby || SteamMatchmaking.GetLobbyOwner(lobby) == SteamUser.GetSteamID())
                return;

            // 버전이 다르면 접속해도 거부되므로 바로 나온다.
            string hostVersion = SteamMatchmaking.GetLobbyData(lobby, SteamLobbyKeys.Version);
            if (hostVersion != GameVersion.Current)
            {
                string shownVersion = string.IsNullOrEmpty(hostVersion) ? "?" : hostVersion;
                Failed?.Invoke("notice.rejected.version", $"{shownVersion} ≠ {GameVersion.Current}");
                LeaveLobby();
                return;
            }

            // 로비 데이터는 주인이 마음대로 적을 수 있으므로, 적힌 주소가 아니라 Steam이 알려 주는 로비 주인에게 접속한다.
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
        /// 방에 있는 중인지 등 참가 여부는 세션 쪽이 판단하므로 로비 ID만 넘긴다.
        /// </summary>
        private void OnJoinRequested(GameLobbyJoinRequested_t request)
        {
            JoinRequested?.Invoke(request.m_steamIDLobby.m_SteamID);
        }

        /// <summary>게임이 꺼진 상태에서 초대를 수락하면 실행 인자로 오므로 시작할 때 확인한다.</summary>
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
