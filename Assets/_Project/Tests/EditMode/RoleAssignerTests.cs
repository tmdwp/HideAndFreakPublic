using System.Collections.Generic;
using GuardVsMannequin.Core;
using GuardVsMannequin.Player;
using NUnit.Framework;

namespace GuardVsMannequin.Tests
{
    /// <summary>
    /// 희망 팀을 반영한 역할 배정 규칙을 확인한다.
    /// </summary>
    public class RoleAssignerTests
    {
        /// <summary>가드 희망자가 자리 수 이하이면 모두 가드가 되고, 마네킹 희망자는 가드가 되지 않는지 확인한다.</summary>
        [Test]
        public void Assign_GivesGuardSlotsToVolunteersFirst()
        {
            var prefs = new Dictionary<int, TeamPreference>
            {
                [1] = TeamPreference.Mannequin, [2] = TeamPreference.Guard, [3] = TeamPreference.Random, [4] = TeamPreference.Mannequin
            };
            var assigner = new RoleAssigner(new System.Random(7));

            Dictionary<int, PlayerRole> roles = assigner.Assign(new List<int> { 1, 2, 3, 4 }, 2, id => prefs[id]);

            Assert.AreEqual(PlayerRole.Guard, roles[2]);
            Assert.AreEqual(PlayerRole.Guard, roles[3]);
            Assert.AreEqual(PlayerRole.Mannequin, roles[1]);
            Assert.AreEqual(PlayerRole.Mannequin, roles[4]);
        }

        /// <summary>가드 희망자가 자리보다 많아도 가드 수는 설정대로이고, 가드는 희망자 중에서만 나오는지 확인한다.</summary>
        [Test]
        public void Assign_KeepsGuardCountWhenTooManyVolunteers()
        {
            var prefs = new Dictionary<int, TeamPreference>
            {
                [1] = TeamPreference.Guard, [2] = TeamPreference.Guard, [3] = TeamPreference.Guard, [4] = TeamPreference.Random
            };
            var assigner = new RoleAssigner(new System.Random(3));

            Dictionary<int, PlayerRole> roles = assigner.Assign(new List<int> { 1, 2, 3, 4 }, 1, id => prefs[id]);

            int guards = 0;
            foreach (KeyValuePair<int, PlayerRole> pair in roles)
            {
                if (pair.Value == PlayerRole.Guard)
                {
                    guards++;
                    Assert.AreNotEqual(4, pair.Key);
                }
            }
            Assert.AreEqual(1, guards);
        }

        /// <summary>모두 마네킹을 원해도 판이 성립하도록 가드는 최소 1명 나오는지 확인한다.</summary>
        [Test]
        public void Assign_StillHasOneGuardWhenNobodyWantsIt()
        {
            var assigner = new RoleAssigner(new System.Random(1));

            Dictionary<int, PlayerRole> roles = assigner.Assign(new List<int> { 1, 2, 3 }, 1, _ => TeamPreference.Mannequin);

            Assert.AreEqual(1, new List<PlayerRole>(roles.Values).FindAll(role => role == PlayerRole.Guard).Count);
        }
    }
}
