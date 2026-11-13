using System.Collections.Generic;

namespace GuardVsMannequin.Network
{
    /// <summary>네트워크 없이 시험하도록 공개방 목록의 거르기·정렬·빠른 참가 규칙을 값으로 판정한다.</summary>
    public static class RoomListingRules
    {
        /// <summary>
        /// 검색 결과를 덮어쓰면 필터를 풀 때 되돌릴 수 없으므로 원본은 두고 거르고 정렬한 새 목록을 만든다.
        /// </summary>
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
        /// 빠른 참가는 누르자마자 들어가야 하므로 암호가 없고, 맵이 있고, 자리가 있는 방만 고르고 그중 정렬 1순위를 쓴다.
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

        /// <summary>바로 들어가 곧 시작할 수 있는 방이 위에 오도록 정렬하고, 순서가 흔들리지 않게 로비 ID로 마무리한다.</summary>
        public static int Compare(RoomListing first, RoomListing second)
        {
            if (first.hasMap != second.hasMap)
                return first.hasMap ? -1 : 1;
            if (first.hasPassword != second.hasPassword)
                return first.hasPassword ? 1 : -1;
            if (first.playerCount != second.playerCount)
                return second.playerCount.CompareTo(first.playerCount);

            int pingOrder = PingRank(first).CompareTo(PingRank(second));
            return pingOrder != 0 ? pingOrder : first.lobbyId.CompareTo(second.lobbyId);
        }

        /// <summary>
        /// 핑을 모르는 방(-1)이 가장 빠른 방처럼 앞에 오지 않도록 가장 뒤로 보낸다.
        /// </summary>
        private static int PingRank(RoomListing room)
        {
            return room.pingMs < 0 ? int.MaxValue : room.pingMs;
        }
    }
}
