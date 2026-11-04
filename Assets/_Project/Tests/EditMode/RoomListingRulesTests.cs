using System.Collections.Generic;
using GuardVsMannequin.Network;
using NUnit.Framework;

namespace GuardVsMannequin.Tests
{
    /// <summary>
    /// 공개방 목록의 거르기·정렬·빠른 참가 규칙을 확인한다.
    /// </summary>
    public class RoomListingRulesTests
    {
        /// <summary>
        /// 시험용 방을 만든다. 기본은 맵 있음, 암호 없음, 2/10명, 핑 모름.
        /// </summary>
        private static RoomListing Room(ulong id, bool hasMap = true, bool hasPassword = false, int players = 2, int maxPlayers = 10, int ping = RoomListing.UnknownPing)
        {
            return new RoomListing { lobbyId = id, name = "r", mapId = "m", hasMap = hasMap, hasPassword = hasPassword, playerCount = players, maxPlayers = maxPlayers, pingMs = ping };
        }

        /// <summary>정렬은 맵 → 암호 → 인원 → 핑 순으로 앞에 둔다.</summary>
        [Test]
        public void Filter_SortsByMapThenPasswordThenPlayersThenPing()
        {
            var rooms = new List<RoomListing>
            {
                Room(1, hasMap: false, players: 9, ping: 1),
                Room(2, hasPassword: true, players: 9, ping: 1),
                Room(3, players: 2, ping: 1),
                Room(4, players: 5, ping: 200),
                Room(5, players: 5, ping: 30),
                Room(6, players: 5)
            };

            List<RoomListing> sorted = RoomListingRules.Filter(rooms, false, false);

            CollectionAssert.AreEqual(new ulong[] { 5, 4, 6, 3, 2, 1 }, sorted.ConvertAll(room => room.lobbyId));
        }

        /// <summary>필터를 켜면 암호 방과 맵이 없는 방을 뺀다.</summary>
        [Test]
        public void Filter_HidesPasswordAndMissingMapRooms()
        {
            var rooms = new List<RoomListing> { Room(1, hasMap: false), Room(2, hasPassword: true), Room(3) };

            Assert.AreEqual(2, RoomListingRules.Filter(rooms, true, false).Count);
            Assert.AreEqual(2, RoomListingRules.Filter(rooms, false, true).Count);
            Assert.AreEqual(3UL, RoomListingRules.Filter(rooms, true, true)[0].lobbyId);
        }

        /// <summary>빠른 참가는 암호 없음 + 맵 있음 + 자리 있음 중 1순위를 고른다.</summary>
        [Test]
        public void TryPickQuickJoin_PicksBestJoinableRoom()
        {
            var rooms = new List<RoomListing>
            {
                Room(1, hasPassword: true, players: 9),
                Room(2, hasMap: false, players: 9),
                Room(3, players: 10, maxPlayers: 10),
                Room(4, players: 3),
                Room(5, players: 6)
            };

            Assert.IsTrue(RoomListingRules.TryPickQuickJoin(rooms, out RoomListing picked));
            Assert.AreEqual(5UL, picked.lobbyId);
        }

        /// <summary>들어갈 수 있는 방이 없으면 고르지 못한다.</summary>
        [Test]
        public void TryPickQuickJoin_ReturnsFalseWhenNoRoomFits()
        {
            var rooms = new List<RoomListing> { Room(1, hasPassword: true), Room(2, hasMap: false) };

            Assert.IsFalse(RoomListingRules.TryPickQuickJoin(rooms, out _));
        }
    }
}
