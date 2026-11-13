using System;
using System.Collections.Generic;
using GuardVsMannequin.Core;
using Steamworks;
using UnityEngine;

namespace GuardVsMannequin.Network
{
    /// <summary>로비 데이터가 조작될 수 있어 값을 검사하며 Steam 로비 검색으로 공개방을 찾는다.</summary>
    public sealed class SteamRoomBrowser : IRoomBrowser
    {
        // Steam 요청을 남발하지 않게 하는 새로고침 간격(초)
        private const float RefreshInterval = 3f;
        private const int MaxResults = 50;
        private const int MaxMapIdLength = 96;

        private readonly Func<ulong> getCurrentLobbyId;
        private readonly Func<string, bool> hasMap;
        private readonly List<RoomListing> rooms = new();
        private readonly CallResult<LobbyMatchList_t> matchListResult;
        private float nextRefreshTime;

        /// <inheritdoc />
        public bool IsRefreshing { get; private set; }

        /// <inheritdoc />
        public IReadOnlyList<RoomListing> Rooms => rooms;

        /// <inheritdoc />
        public event Action ListUpdated;

        /// <inheritdoc />
        public event Action<string> Failed;

        /// <summary>로비 서비스와 모드 서비스에 직접 의존하지 않도록 필요한 조회만 함수로 받는다.</summary>
        /// <param name="getCurrentLobbyId">내 방이 목록에 보이지 않도록 지금 로비 ID를 구한다.</param>
        /// <param name="hasMap">맵 ID로 내 PC에 그 맵이 있는지 확인한다.</param>
        public SteamRoomBrowser(Func<ulong> getCurrentLobbyId, Func<string, bool> hasMap)
        {
            this.getCurrentLobbyId = getCurrentLobbyId;
            this.hasMap = hasMap;
            matchListResult = CallResult<LobbyMatchList_t>.Create(OnMatchList);
        }

        /// <inheritdoc />
        public bool Refresh()
        {
            if (!SteamBootstrap.IsInitialized)
            {
                Failed?.Invoke("notice.steamNotRunning");
                return false;
            }
            if (IsRefreshing || Time.unscaledTime < nextRefreshTime)
                return false;

            IsRefreshing = true;
            nextRefreshTime = Time.unscaledTime + RefreshInterval;

            // 다른 게임·다른 버전·판 중인 방·가득 찬 방은 들어가도 거부되므로 Steam 쪽에서 먼저 거른다.
            SteamMatchmaking.AddRequestLobbyListStringFilter(SteamLobbyKeys.Game, SteamLobbyKeys.GameValue, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(SteamLobbyKeys.Version, GameVersion.Current, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(SteamLobbyKeys.State, SteamLobbyKeys.StateRoom, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);
            // 초기 사용자 수가 적으므로 전 세계에서 찾는다.
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(MaxResults);
            matchListResult.Set(SteamMatchmaking.RequestLobbyList());
            return true;
        }

        /// <summary>검색 결과는 콜백으로 오므로 여기서 목록을 만들고 내가 있는 방은 뺀다.</summary>
        private void OnMatchList(LobbyMatchList_t result, bool ioFailure)
        {
            IsRefreshing = false;
            if (ioFailure)
            {
                Failed?.Invoke("notice.roomListFailed");
                return;
            }

            rooms.Clear();
            ulong currentLobbyId = getCurrentLobbyId();
            int count = Mathf.Min((int)result.m_nLobbiesMatching, MaxResults);
            for (int i = 0; i < count; i++)
            {
                CSteamID lobby = SteamMatchmaking.GetLobbyByIndex(i);
                if (lobby.m_SteamID != currentLobbyId && TryReadListing(lobby, out RoomListing listing))
                    rooms.Add(listing);
            }
            ListUpdated?.Invoke();
        }

        /// <summary>다른 게임 로비가 섞일 수 있어 게임 표시를 한 번 더 보고, 조작될 수 있는 값은 검사해 넣는다.</summary>
        private bool TryReadListing(CSteamID lobby, out RoomListing listing)
        {
            listing = default;
            if (SteamMatchmaking.GetLobbyData(lobby, SteamLobbyKeys.Game) != SteamLobbyKeys.GameValue)
                return false;

            string mapId = DisplayTextSanitizer.Clean(SteamMatchmaking.GetLobbyData(lobby, SteamLobbyKeys.Map), MaxMapIdLength);
            int memberLimit = SteamMatchmaking.GetLobbyMemberLimit(lobby);
            listing = new RoomListing
            {
                lobbyId = lobby.m_SteamID,
                name = DisplayTextSanitizer.Clean(SteamMatchmaking.GetLobbyData(lobby, SteamLobbyKeys.Name), SteamRoomOptions.MaxNameLength),
                mapId = mapId,
                playerCount = SteamMatchmaking.GetNumLobbyMembers(lobby),
                maxPlayers = ReadMaxPlayers(lobby, memberLimit),
                hasPassword = SteamMatchmaking.GetLobbyData(lobby, SteamLobbyKeys.HasPassword) == "1",
                // 호스트가 맵을 아직 적지 않았으면(방을 막 만든 순간) 없는 맵으로 보지 않는다.
                hasMap = mapId.Length == 0 || hasMap(mapId),
                pingMs = EstimatePing(lobby)
            };
            return true;
        }

        /// <summary>
        /// 적힌 값은 조작될 수 있으므로 게임 범위를 벗어나면 Steam이 관리하는 로비 인원 상한을 대신 쓴다.
        /// </summary>
        private static int ReadMaxPlayers(CSteamID lobby, int memberLimit)
        {
            bool isValid = int.TryParse(SteamMatchmaking.GetLobbyData(lobby, SteamLobbyKeys.MaxPlayers), out int maxPlayers)
                && maxPlayers >= GameConstants.MinPlayers && maxPlayers <= GameConstants.MaxPlayers;
            return isValid ? maxPlayers : Mathf.Clamp(memberLimit, GameConstants.MinPlayers, GameConstants.MaxPlayers);
        }

        /// <summary>접속하지 않고도 핑을 보여 주도록 호스트의 핑 위치로 예상 핑을 구한다.</summary>
        private static int EstimatePing(CSteamID lobby)
        {
            string location = SteamMatchmaking.GetLobbyData(lobby, SteamLobbyKeys.PingLocation);
            if (string.IsNullOrEmpty(location) || !SteamNetworkingUtils.ParsePingLocationString(location, out SteamNetworkPingLocation_t remoteLocation))
                return RoomListing.UnknownPing;

            int pingMs = SteamNetworkingUtils.EstimatePingTimeFromLocalHost(ref remoteLocation);
            return pingMs >= 0 ? pingMs : RoomListing.UnknownPing;
        }
    }
}
