using GuardVsMannequin.Core;
using GuardVsMannequin.Network;
using NUnit.Framework;

namespace GuardVsMannequin.Tests
{
    /// <summary>
    /// 플레이어 키, 추방, 재접속 대기 자리 규칙을 확인한다.
    /// </summary>
    public class RoomMembershipTests
    {
        private const float GraceSeconds = 60f;
        private const float LoadLimitSeconds = 120f;

        /// <summary>각 테스트가 확인하려는 값만 드러나도록 나머지는 살아 있는 가드의 자리로 채운다.</summary>
        private static RejoinSlot CreateSlot(string key, PlayerRole role = PlayerRole.Guard, bool isAlive = true, float disconnectTime = 0f)
        {
            return new RejoinSlot { key = key, role = role, isAlive = isAlive, disconnectTime = disconnectTime };
        }

        /// <summary>재접속해도 같은 사람으로 알아보려면 바뀌지 않는 값이 키여야 하므로 SteamID → 토큰 → 연결 번호 순으로 고르는지, 너무 긴 토큰은 버리는지 확인한다.</summary>
        [Test]
        public void PlayerKey_UsesSteamIdThenTokenThenConnection()
        {
            Assert.AreEqual("steam:765", PlayerKey.Resolve("765", "ignored", 1));
            Assert.AreEqual("token:abc", PlayerKey.Resolve("127.0.0.1", "abc", 1));
            Assert.AreEqual("connection:7", PlayerKey.Resolve("127.0.0.1", string.Empty, 7));
            Assert.AreEqual("connection:7", PlayerKey.Resolve("127.0.0.1", new string('x', 65), 7));
        }

        /// <summary>추방한 사람이 재접속 자리로 돌아오지 못하도록 자리가 지워지고 새로 만들 수도 없는지 확인한다.</summary>
        [Test]
        public void Ban_RemovesSlotAndBlocksNewSlot()
        {
            var membership = new RoomMembership();
            membership.AddSlot(CreateSlot("a"));

            membership.Ban("a");

            Assert.IsTrue(membership.IsBanned("a"));
            Assert.IsFalse(membership.HasSlot("a"));
            Assert.IsFalse(membership.CanHoldSlot("a"));
        }

        /// <summary>추방은 방이 유지되는 동안 계속돼야 하고 나머지는 판 단위 정보이므로, 판이 끝나면 추방 목록만 남는지 확인한다.</summary>
        [Test]
        public void ClearMatchState_KeepsBansOnly()
        {
            var membership = new RoomMembership();
            membership.Ban("banned");
            membership.MarkLeaving("left");
            membership.AddSlot(CreateSlot("a"));

            membership.ClearMatchState();

            Assert.IsTrue(membership.IsBanned("banned"));
            Assert.IsTrue(membership.CanHoldSlot("left"));
            Assert.IsFalse(membership.HasSlot("a"));
        }

        /// <summary>끊긴 생존자가 있으면 바로 패배 처리하지 않아야 하므로, 살아 있는 사람만 역할별로 세는지 확인한다.</summary>
        [Test]
        public void CountPendingAlive_CountsAliveByRole()
        {
            var membership = new RoomMembership();
            membership.AddSlot(CreateSlot("a"));
            membership.AddSlot(CreateSlot("b", isAlive: false));
            membership.AddSlot(CreateSlot("c", PlayerRole.Mannequin));

            Assert.AreEqual(1, membership.CountPendingAlive(PlayerRole.Guard));
            Assert.AreEqual(1, membership.CountPendingAlive(PlayerRole.Mannequin));
        }

        /// <summary>돌아오지 않는 사람 때문에 판이 멈추지 않도록 유예 시간이 지나면 자리가 없어지는지 경계값으로 확인한다.</summary>
        [Test]
        public void RemoveExpired_RemovesAfterGrace()
        {
            var membership = new RoomMembership();
            membership.AddSlot(CreateSlot("a"));

            Assert.IsEmpty(membership.RemoveExpired(59f, GraceSeconds, LoadLimitSeconds));
            CollectionAssert.AreEqual(new[] { "a" }, membership.RemoveExpired(60f, GraceSeconds, LoadLimitSeconds));
        }

        /// <summary>느린 PC가 맵을 불러오다 자리를 잃지 않으면서도, 재접속을 반복해 유예 시간을 늘릴 수 없는지 확인한다.</summary>
        [Test]
        public void Claim_UsesLoadLimitAndReleaseKeepsOriginalTime()
        {
            var membership = new RoomMembership();
            membership.AddSlot(CreateSlot("a"));
            Assert.IsTrue(membership.TryClaim("a", 5, 50f));

            Assert.IsEmpty(membership.RemoveExpired(100f, GraceSeconds, LoadLimitSeconds));

            membership.ReleaseClaim("a");
            CollectionAssert.AreEqual(new[] { "a" }, membership.RemoveExpired(100f, GraceSeconds, LoadLimitSeconds));
        }

        /// <summary>다른 사람이 남의 자리를 가로채지 못하고, 돌아온 사람이 대기 인원에 두 번 세지지 않는지 확인한다.</summary>
        [Test]
        public void TryTakeClaimed_OnlyForClaimingClient()
        {
            var membership = new RoomMembership();
            membership.AddSlot(CreateSlot("a"));
            membership.Bind(5, "a");
            membership.Bind(6, "b");
            membership.TryClaim("a", 5, 1f);

            Assert.IsFalse(membership.TryTakeClaimed(6, out _));
            Assert.IsTrue(membership.TryTakeClaimed(5, out RejoinSlot slot));
            Assert.AreEqual("a", slot.key);
            Assert.AreEqual(0, membership.CountPendingAlive(PlayerRole.Guard));
        }

        /// <summary>FishNet이 연결 번호를 재사용하므로 옛 사람의 키가 새 연결을 가리키지 않는지 확인한다.</summary>
        [Test]
        public void Bind_ReusedClientIdDropsOldKey()
        {
            var membership = new RoomMembership();
            membership.Bind(5, "a");

            membership.Bind(5, "b");

            Assert.IsFalse(membership.TryGetClient("a", out _));
            Assert.IsTrue(membership.TryGetClient("b", out int clientId));
            Assert.AreEqual(5, clientId);
        }
    }
}
