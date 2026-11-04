using System;
using System.Collections.Generic;
using GuardVsMannequin.Core;
using Steamworks;
using UnityEngine;

namespace GuardVsMannequin.Network
{
    /// <summary>
    /// Steam 로비 검색으로 공개방을 찾는다. 내 로비 관리는 <see cref="SteamLobbyService"/>가 맡는다(SRP).
    /// 로비 데이터는 주인이 마음대로 적을 수 있으므로, 이름을 정리하고 숫자 범위를 확인한 뒤 목록에 넣는다.
    /// </summary>
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

        /// <param name="getCurrentLobbyId">내가 들어가 있는 로비 ID를 구하는 함수. (목록에서 뺀다)</param>
        /// <param name="hasMap">맵 ID로 내 PC에 그 맵이 있는지 확인하는 함수.</param>
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

            SteamMatchmaking.AddRequestLobbyListStringFilter(SteamLobbyKeys.Game, SteamLobbyKeys.GameValue, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(SteamLobbyKeys.Version, GameVersion.Current, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);
            // 초기 사용자 수가 적으므로 전 세계에서 찾는다. (사용자가 늘면 기본 거리로)
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(MaxResults);
            matchListResult.Set(SteamMatchmaking.RequestLobbyList());
            return true;
        }

        /// <summary>
        /// 검색 결과를 목록으로 만든다.
        /// </summary>
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

        /// <summary>
        /// 로비 데이터를 읽어 방 하나를 만든다. 우리 게임의 로비가 아니면 false.
        /// </summary>
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
        /// 표시할 최대 인원. 적힌 값이 게임 범위를 벗어나면 로비 인원 상한을 쓴다.
        /// </summary>
        private static int ReadMaxPlayers(CSteamID lobby, int memberLimit)
        {
            bool isValid = int.TryParse(SteamMatchmaking.GetLobbyData(lobby, SteamLobbyKeys.MaxPlayers), out int maxPlayers)
                && maxPlayers >= GameConstants.MinPlayers && maxPlayers <= GameConstants.MaxPlayers;
            return isValid ? maxPlayers : Mathf.Clamp(memberLimit, GameConstants.MinPlayers, GameConstants.MaxPlayers);
        }

        /// <summary>
        /// 호스트의 핑 위치로 예상 핑을 구한다. 모르면 <see cref="RoomListing.UnknownPing"/>.
        /// </summary>
        private static int EstimatePing(CSteamID lobby)
        {
            string location = SteamMatchmaking.GetLobbyData(lobby, SteamLobbyKeys.PingLocation);
            if (string.IsNullOrEmpty(location) || !SteamNetworkingUtils.ParsePingLocationString(location, out SteamNetworkPingLocation_t remote))
                return RoomListing.UnknownPing;

            int ping = SteamNetworkingUtils.EstimatePingTimeFromLocalHost(ref remote);
            return ping >= 0 ? ping : RoomListing.UnknownPing;
        }
    }
}
