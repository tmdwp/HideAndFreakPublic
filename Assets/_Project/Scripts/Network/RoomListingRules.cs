using System.Collections.Generic;

namespace GuardVsMannequin.Network
{
    /// <summary>
    /// 공개방 목록의 표시 규칙(거르기, 정렬, 빠른 참가 고르기). 값만 판정한다.
    /// </summary>
    public static class RoomListingRules
    {
        /// <summary>
        /// 표시 필터를 적용하고 정렬한 새 목록을 만든다.
        /// </summary>
        /// <param name="rooms">검색 결과.</param>
        /// <param name="hidePassword">암호 방을 숨길지.</param>
        /// <param name="hideMissingMap">맵이 없는 방을 숨길지.</param>
        public static List<RoomListing> Filter(IReadOnlyList<RoomListing> rooms, bool hidePassword, bool hideMissingMap)
        {
            var result = new List<RoomListing>();
            foreach (RoomListing room in rooms)
            {
                if ((hidePassword && room.hasPassword) || (hideMissingMap && !room.hasMap))
                    continue;
                result.Add(room);
            }
            result.Sort(Compare);
            return result;
        }

        /// <summary>
        /// 빠른 참가할 방을 고른다. 암호가 없고, 맵이 있고, 자리가 있는 방 중 정렬 1순위.
        /// </summary>
        /// <returns>들어갈 수 있는 방이 있는지 여부.</returns>
        public static bool TryPickQuickJoin(IReadOnlyList<RoomListing> rooms, out RoomListing picked)
        {
            picked = default;
            bool found = false;
            foreach (RoomListing room in rooms)
            {
                if (room.hasPassword || !room.hasMap || room.playerCount >= room.maxPlayers)
                    continue;
                if (!found || Compare(room, picked) < 0)
                {
                    picked = room;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// 정렬 순서: 맵이 있는 방 → 암호 없는 방 → 인원이 많은 방(곧 시작할 방) → 핑이 낮은 방(모르면 뒤로).
        /// </summary>
        public static int Compare(RoomListing a, RoomListing b)
        {
            if (a.hasMap != b.hasMap)
                return a.hasMap ? -1 : 1;
            if (a.hasPassword != b.hasPassword)
                return a.hasPassword ? 1 : -1;
            if (a.playerCount != b.playerCount)
                return b.playerCount.CompareTo(a.playerCount);

            int pingOrder = PingRank(a).CompareTo(PingRank(b));
            return pingOrder != 0 ? pingOrder : a.lobbyId.CompareTo(b.lobbyId);
        }

        /// <summary>
        /// 핑 정렬 값. 모르는 핑은 가장 뒤로 보낸다.
        /// </summary>
        private static int PingRank(RoomListing room)
        {
            return room.pingMs < 0 ? int.MaxValue : room.pingMs;
        }
    }
}
