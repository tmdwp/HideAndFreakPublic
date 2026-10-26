using System;
using GuardVsMannequin.Core;
using GuardVsMannequin.Mods;
using Steamworks;
using UnityEngine;

namespace GuardVsMannequin.Network
{
    /// <summary>
    /// Steam 로비(방 코드, 친구 초대)를 관리한다. 네트워크 연결 자체는 하지 않는다(SRP).
    /// - 방 코드 = Steam 로비 ID
    /// - 로비 데이터 "HostAddress"에 호스트의 SteamID를 저장해, 참가자가 그 주소로 접속한다.
    /// </summary>
    public class SteamLobbyService : MonoBehaviour
    {
        private const string HostAddressKey = "HostAddress";
        private const string VersionKey = "Version";
        private const string ModsKey = "Mods";
        private const string ModListKey = "ModList";
        private const string HasPasswordKey = "HasPassword";

        [SerializeField] private int maxMembers = 10;

        [Tooltip("로비 공개 범위. FriendsOnly = Steam 친구와 초대받은 사람만 참가(방 코드도 친구만 통함), " +
                 "Public = 방 코드를 아는 누구나 참가(테스트용 AppID 480에서는 다른 게임의 로비 검색에 보일 수 있음), " +
                 "Private = 초대로만 참가.")]
        [SerializeField] private ELobbyType lobbyType = ELobbyType.k_ELobbyTypeFriendsOnly;

        private CallResult<LobbyCreated_t> lobbyCreatedResult;
        private Callback<LobbyEnter_t> lobbyEnterCallback;
        private Callback<GameLobbyJoinRequested_t> joinRequestedCallback;
        private bool isCreatingLobby;
        private bool creatingWithPassword;

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

            CheckLaunchInvite();
        }

        /// <summary>
        /// 인스펙터에서 정한 공개 범위로 로비를 만든다.
        /// 암호 자체는 로비 데이터에 적지 않는다(누구나 읽을 수 있으므로). 암호가 있다는 표시만 적고, 확인은 호스트가 접속 때 한다.
        /// </summary>
        public void CreateLobby(bool hasPassword)
        {
            if (!SteamBootstrap.IsInitialized)
            {
                Failed?.Invoke("notice.steamNotRunning", string.Empty);
                return;
            }

            creatingWithPassword = hasPassword;
            isCreatingLobby = true;
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
            SteamMatchmaking.SetLobbyData(lobby, HostAddressKey, SteamUser.GetSteamID().m_SteamID.ToString());
            // 참가자가 호스트에 접속하기 전에 버전을 비교할 수 있게 적어 둔다.
            SteamMatchmaking.SetLobbyData(lobby, VersionKey, GameVersion.Current);
            // 켜진 모드 지문과 모드 ID 목록 (참가자가 접속 전에 호스트 구성에 맞춰 둘 수 있게 한다)
            // 로비 값은 크기 제한이 있으므로 ID만 넣고, 넘치면 비운다. (버전·파일 비교와 안내는 접속 후 서버가 한다)
            SteamMatchmaking.SetLobbyData(lobby, ModsKey, ModService.Instance.ModSetHash);
            SteamMatchmaking.SetLobbyData(lobby, ModListKey, FitLobbyValue(ModService.Instance.ModSetIds));
            SteamMatchmaking.SetLobbyData(lobby, HasPasswordKey, creatingWithPassword ? "1" : "0");
            LobbyCreated?.Invoke(CurrentLobbyId);
        }

        // Steam 로비 데이터 값 하나의 최대 크기(바이트, Steamworks k_cubChatMetadataMax 8192)보다 조금 작게 둔다.
        private const int MaxLobbyValueBytes = 8000;

        /// <summary>
        /// 로비 값 크기 제한을 넘으면 빈 문자열로 바꾼다. (잘린 목록은 잘못된 모드 구성으로 맞추게 하므로 넣지 않는다)
        /// </summary>
        private static string FitLobbyValue(string value)
        {
            if (string.IsNullOrEmpty(value) || System.Text.Encoding.UTF8.GetByteCount(value) <= MaxLobbyValueBytes)
                return value ?? string.Empty;

            Debug.LogWarning($"[SteamLobby] 켜진 모드 목록이 로비 데이터 크기 제한을 넘어 넣지 않습니다. 참가자는 접속 후 호스트와 모드를 맞춥니다.");
            return string.Empty;
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
            string hostVersion = SteamMatchmaking.GetLobbyData(lobby, VersionKey);
            if (hostVersion != GameVersion.Current)
            {
                string shownVersion = string.IsNullOrEmpty(hostVersion) ? "?" : hostVersion;
                Failed?.Invoke("notice.rejected.version", $"{shownVersion} ≠ {GameVersion.Current}");
                LeaveLobby();
                return;
            }

            // 켜진 모드가 다르면 접속 전에 호스트 구성(모드 ID)에 맞춰 둔다.
            // 맞추지 못해도 여기서 거절하지 않고 접속한다. 서버가 전체 모드 정보로 다시 맞추게 하고,
            // 그래도 다르면 부족한 모드(버전·파일 차이 포함)와 함께 거절한다. (RoomAuthenticator)
            string hostModIds = SteamMatchmaking.GetLobbyData(lobby, ModListKey);
            if (!ModService.Instance.TryMatchHost(hostModIds, SteamMatchmaking.GetLobbyData(lobby, ModsKey)))
                Debug.Log("[SteamLobby] 접속 전 모드 맞추기에 실패했습니다. 접속 후 호스트와 다시 확인합니다.");

            string hostAddress = SteamMatchmaking.GetLobbyData(lobby, HostAddressKey);
            if (!ulong.TryParse(hostAddress, out ulong hostSteamId))
            {
                Failed?.Invoke("notice.lobbyDataInvalid", string.Empty);
                LeaveLobby();
                return;
            }

            bool hasPassword = SteamMatchmaking.GetLobbyData(lobby, HasPasswordKey) == "1";
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
