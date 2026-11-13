using System;
using System.Collections.Generic;
using GuardVsMannequin.Core;
using GuardVsMannequin.Player;
using NUnit.Framework;

namespace GuardVsMannequin.Tests
{
    /// <summary>
    /// 시작 인원에 따른 설정 보정과 역할 배정이 같은 가드 수 기준을 쓰는지 확인한다.
    /// </summary>
    public class GameSettingsRulesTests
    {
        /// <summary>가드가 마네킹보다 많아지지 않도록 인원 절반으로 묶이고 최소 1명인지 확인한다.</summary>
        [Test]
        public void MaxGuardCount_IsHalfOfPlayersAtLeastOne()
        {
            Assert.AreEqual(1, GameSettingsValidator.GetMaxGuardCount(1));
            Assert.AreEqual(1, GameSettingsValidator.GetMaxGuardCount(3));
            Assert.AreEqual(2, GameSettingsValidator.GetMaxGuardCount(5));
            Assert.AreEqual(5, GameSettingsValidator.GetMaxGuardCount(10));
        }

        /// <summary>룸 UI와 서버가 같은 인원 범위로 시작 가능 여부를 정하는지 확인한다.</summary>
        [Test]
        public void CanStart_RespectsPlayerLimits()
        {
            Assert.IsFalse(GameSettingsValidator.CanStart(GameConstants.MinPlayers - 1));
            Assert.IsTrue(GameSettingsValidator.CanStart(GameConstants.MinPlayers));
            Assert.IsTrue(GameSettingsValidator.CanStart(GameConstants.MaxPlayers));
            Assert.IsFalse(GameSettingsValidator.CanStart(GameConstants.MaxPlayers + 1));
        }

        /// <summary>인원보다 많은 가드·목표를 원해도 판이 성립하는 값으로 줄고, 전원 장치는 목표보다 하나 이상 많은지 확인한다.</summary>
        [Test]
        public void Validate_ClampsCountsToPlayers()
        {
            var settings = new GameSettings { guardCount = 4, objectiveCount = 10, generatorCount = 1, playTime = 300f, huntDelay = 30f, huntTime = 60f };

            GameSettings result = GameSettingsValidator.Validate(settings, 5);

            Assert.AreEqual(2, result.guardCount);
            Assert.AreEqual(3 + 2, result.objectiveCount, "마네킹 3명 + 2");
            Assert.GreaterOrEqual(result.generatorCount, result.objectiveCount + 1);
        }

        /// <summary>잘못된 파일 값이 들어와도 최후 안전선과 최소값으로 보정되는지 확인한다.</summary>
        [Test]
        public void Validate_ClampsTimesAndMinimums()
        {
            var settings = new GameSettings { playTime = 1f, huntDelay = 10000f, huntTime = 0f, gunMagazineSize = 0, guardMaxHealth = -5, guardMoveSpeed = 100f };

            GameSettings result = GameSettingsValidator.Validate(settings, 4);

            Assert.AreEqual(GameSettingsValidator.MinPlayTime, result.playTime);
            Assert.AreEqual(GameSettingsValidator.MaxHuntDelay, result.huntDelay);
            Assert.AreEqual(1f, result.huntTime);
            Assert.AreEqual(1, result.gunMagazineSize);
            Assert.AreEqual(1, result.guardMaxHealth);
            Assert.AreEqual(GameSettingsValidator.MaxMoveSpeed, result.guardMoveSpeed);
        }

        /// <summary>모든 사람이 역할을 하나씩 받고, 가드 수가 인원에 맞게 보정되는지 확인한다.</summary>
        [Test]
        public void RoleAssigner_AssignsEveryoneWithClampedGuards()
        {
            var assigner = new RoleAssigner(new Random(1));
            var clientIds = new List<int> { 10, 11, 12, 13, 14 };

            Dictionary<int, PlayerRole> roles = assigner.Assign(clientIds, 99);

            Assert.AreEqual(clientIds.Count, roles.Count);
            Assert.AreEqual(2, CountGuards(roles));
            Assert.AreEqual(1, CountGuards(assigner.Assign(clientIds, 0)), "0명을 원해도 최소 1명");
        }

        /// <summary>같은 시드면 같은 결과가 나와 테스트가 재현되고, 시드가 바뀌면 특정 사람이 늘 가드가 되지 않는지 확인한다.</summary>
        [Test]
        public void RoleAssigner_IsReproducibleAndSpreadsGuards()
        {
            var clientIds = new List<int> { 1, 2, 3, 4 };
            CollectionAssert.AreEquivalent(new RoleAssigner(new Random(7)).Assign(clientIds, 1), new RoleAssigner(new Random(7)).Assign(clientIds, 1));

            var guardIds = new HashSet<int>();
            for (int seed = 0; seed < 50; seed++)
            {
                foreach (KeyValuePair<int, PlayerRole> pair in new RoleAssigner(new Random(seed)).Assign(clientIds, 1))
                {
                    if (pair.Value == PlayerRole.Guard)
                        guardIds.Add(pair.Key);
                }
            }
            Assert.Greater(guardIds.Count, 1);
        }

        /// <summary>빈 목록에서 예외 없이 빈 결과를 내는지 확인한다.</summary>
        [Test]
        public void RoleAssigner_EmptyList_ReturnsEmpty()
        {
            Assert.IsEmpty(new RoleAssigner(new Random(0)).Assign(new List<int>(), 1));
        }

        /// <summary>가드 수를 세는 반복을 테스트마다 쓰지 않도록 모은다.</summary>
        private static int CountGuards(Dictionary<int, PlayerRole> roles)
        {
            int count = 0;
            foreach (PlayerRole role in roles.Values)
            {
                if (role == PlayerRole.Guard)
                    count++;
            }
            return count;
        }
    }
}
