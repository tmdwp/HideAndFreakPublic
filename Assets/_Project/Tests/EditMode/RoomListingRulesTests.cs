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
        /// 각 테스트가 확인하려는 값만 드러나도록 나머지는 평범한 방(맵 있음, 암호 없음, 2/10명, 핑 모름)으로 채운다.
        /// </summary>
        private static RoomListing CreateRoom(ulong id, bool hasMap = true, bool hasPassword = false, int players = 2, int maxPlayers = 10, int ping = RoomListing.UnknownPing)
        {
            return new RoomListing { lobbyId = id, name = "r", mapId = "m", hasMap = hasMap, hasPassword = hasPassword, playerCount = players, maxPlayers = maxPlayers, pingMs = ping };
        }

        /// <summary>바로 들어갈 수 있고 사람이 많은 방이 위에 와야 하므로 맵 → 암호 → 인원 → 핑 순 정렬을 지키는지 확인한다.</summary>
        [Test]
        public void Filter_SortsByMapThenPasswordThenPlayersThenPing()
        {
            var rooms = new List<RoomListing>
            {
                CreateRoom(1, hasMap: false, players: 9, ping: 1),
                CreateRoom(2, hasPassword: true, players: 9, ping: 1),
                CreateRoom(3, players: 2, ping: 1),
                CreateRoom(4, players: 5, ping: 200),
                CreateRoom(5, players: 5, ping: 30),
                CreateRoom(6, players: 5)
            };

            List<RoomListing> sorted = RoomListingRules.Filter(rooms, false, false);

            CollectionAssert.AreEqual(new ulong[] { 5, 4, 6, 3, 2, 1 }, sorted.ConvertAll(room => room.lobbyId));
        }

        /// <summary>들어갈 수 없는 방을 숨기는 필터가 각각 따로 동작하는지 확인한다.</summary>
        [Test]
        public void Filter_HidesPasswordAndMissingMapRooms()
        {
            var rooms = new List<RoomListing> { CreateRoom(1, hasMap: false), CreateRoom(2, hasPassword: true), CreateRoom(3) };

            Assert.AreEqual(2, RoomListingRules.Filter(rooms, true, false).Count);
            Assert.AreEqual(2, RoomListingRules.Filter(rooms, false, true).Count);
            Assert.AreEqual(3UL, RoomListingRules.Filter(rooms, true, true)[0].lobbyId);
        }

        /// <summary>빠른 참가가 들어가자마자 실패하지 않도록 암호 없음 + 맵 있음 + 자리 있음 중 1순위를 고르는지 확인한다.</summary>
        [Test]
        public void TryPickQuickJoin_PicksBestJoinableRoom()
        {
            var rooms = new List<RoomListing>
            {
                CreateRoom(1, hasPassword: true, players: 9),
                CreateRoom(2, hasMap: false, players: 9),
                CreateRoom(3, players: 10, maxPlayers: 10),
                CreateRoom(4, players: 3),
                CreateRoom(5, players: 6)
            };

            Assert.IsTrue(RoomListingRules.TryPickQuickJoin(rooms, out RoomListing picked));
            Assert.AreEqual(5UL, picked.lobbyId);
        }

        /// <summary>들어갈 방이 없을 때 아무 방이나 골라 참가 오류가 나지 않도록 false를 돌려주는지 확인한다.</summary>
        [Test]
        public void TryPickQuickJoin_ReturnsFalseWhenNoRoomFits()
        {
            var rooms = new List<RoomListing> { CreateRoom(1, hasPassword: true), CreateRoom(2, hasMap: false) };

            Assert.IsFalse(RoomListingRules.TryPickQuickJoin(rooms, out _));
        }
    }
}
