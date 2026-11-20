using GuardVsMannequin.Core;
using NUnit.Framework;
using UnityEngine;

namespace GuardVsMannequin.Tests
{
    /// <summary>구울 속도·도약·은신 소리 규칙을 조정 파일 값이 아닌 계산 규칙으로 확인한다.</summary>
    public class GhoulRulesTests
    {
        private const float Tolerance = 0.0001f;

        /// <summary>사냥 시간 마네킹(구울) 속도가 룸 설정이 아니라 조정 파일의 구울 속도를 그대로 쓰는지 확인한다.</summary>
        [Test]
        public void HuntMoveSpeed_UsesTuningGhoulMoveSpeed()
        {
            GameSettings settings = GameSettings.CreateDefault();
            settings.mannequinMoveSpeed = 9f;

            float ghoulSpeed = RoleDefinitions.Get(PlayerRole.Mannequin).GetMoveSpeed(settings, GameState.Hunting);

            Assert.Greater(ghoulSpeed, 0f);
            Assert.AreEqual(GameTuning.GhoulMoveSpeed, ghoulSpeed, Tolerance);
        }

        /// <summary>사냥 시간 속도 규칙은 마네킹에만 적용되고 가드·탐색 단계는 평소 속도인지 확인한다.</summary>
        [Test]
        public void MoveSpeed_OutsideGhoul_UsesBaseSpeed()
        {
            GameSettings settings = GameSettings.CreateDefault();

            Assert.AreEqual(settings.guardMoveSpeed, RoleDefinitions.Get(PlayerRole.Guard).GetMoveSpeed(settings, GameState.Hunting), Tolerance);
            Assert.AreEqual(settings.mannequinMoveSpeed, RoleDefinitions.Get(PlayerRole.Mannequin).GetMoveSpeed(settings, GameState.Playing), Tolerance);
        }

        /// <summary>도약 속도로 도약 시간 동안 가면 정한 거리가 되는지 확인한다.</summary>
        [Test]
        public void LungeSpeed_TimesDuration_IsDistance()
        {
            float speed = GhoulRules.GetLungeSpeed(2f, 0.25f);

            Assert.AreEqual(2f, speed * 0.25f, Tolerance);
        }

        /// <summary>거리·시간이 0이면 도약하지 않는지 확인한다.</summary>
        [TestCase(0f, 0.25f)]
        [TestCase(2f, 0f)]
        public void LungeSpeed_ZeroInput_IsZero(float distance, float duration)
        {
            Assert.AreEqual(0f, GhoulRules.GetLungeSpeed(distance, duration));
        }

        /// <summary>도약 포물선이 정한 체공 시간에 정한 최고 높이에 닿고, 끝날 때 처음 높이로 돌아오는지 확인한다.</summary>
        [Test]
        public void LungeArc_ReachesHeightAndLandsAtDuration()
        {
            const float height = 0.6f;
            const float duration = 0.45f;
            GhoulRules.GetLungeArc(height, duration, out float launchSpeed, out float arcGravity);

            float half = duration * 0.5f;
            float peakHeight = launchSpeed * half - 0.5f * arcGravity * half * half;
            float endHeight = launchSpeed * duration - 0.5f * arcGravity * duration * duration;

            Assert.AreEqual(height, peakHeight, Tolerance);
            Assert.AreEqual(0f, endHeight, Tolerance);
        }

        /// <summary>높이가 0이면 위로 뛰지 않고 곧게 도약하는지 확인한다.</summary>
        [Test]
        public void LungeArc_ZeroHeight_IsFlat()
        {
            GhoulRules.GetLungeArc(0f, 0.45f, out float launchSpeed, out float arcGravity);

            Assert.AreEqual(0f, launchSpeed);
            Assert.AreEqual(0f, arcGravity);
        }

        /// <summary>가득 찬 스태미나로 정한 시간(20초)만큼 은신하면 딱 바닥나는지 확인한다.</summary>
        [Test]
        public void StealthDrain_FullStaminaLastsConfiguredSeconds()
        {
            const float step = 0.05f;
            var stamina = new GuardVsMannequin.Player.StaminaModel();
            float drain = GhoulRules.GetStealthDrainPerSecond(20f);

            int justBeforeSteps = Mathf.RoundToInt(19.9f / step);
            for (int i = 0; i < justBeforeSteps; i++)
                stamina.Tick(true, drain, step);
            Assert.IsFalse(stamina.IsExhausted, "20초가 되기 전에는 남아 있어야 한다");

            for (int i = 0; i < 4; i++)
                stamina.Tick(true, drain, step);
            Assert.IsTrue(stamina.IsExhausted, "20초가 지나면 바닥나야 한다");
            Assert.IsFalse(stamina.Tick(true, drain, step), "바닥나면 더 쓰지 못해 은신이 풀려야 한다");
        }

        /// <summary>도약으로 스태미나를 바닥내면 바로 0·소진 상태가 되어 은신(스태미나 사용)을 다시 시작하지 못하는지 확인한다.</summary>
        [Test]
        public void Deplete_EmptiesStaminaAndBlocksDrain()
        {
            var stamina = new GuardVsMannequin.Player.StaminaModel();

            stamina.Deplete();

            Assert.AreEqual(0f, stamina.Current);
            Assert.IsTrue(stamina.IsExhausted);
            Assert.IsFalse(stamina.Tick(true, GhoulRules.GetStealthDrainPerSecond(20f), 0.05f));
        }

        /// <summary>서버 허용 거리가 도약 거리보다 넉넉해 정상 도약이 되돌려지지 않는지 확인한다.</summary>
        [Test]
        public void LungeAllowance_CoversLungeDistance()
        {
            Assert.Greater(GhoulRules.GetLungeAllowance(2f), 2f);
            Assert.AreEqual(0f, GhoulRules.GetLungeAllowance(0f));
        }

        /// <summary>은신 중에만 소리가 줄고, 배율은 0~1로 잘리는지 확인한다.</summary>
        [Test]
        public void BodySound_OnlyReducedWhileStealthed()
        {
            Assert.AreEqual(1f, GhoulRules.GetBodySoundMultiplier(false, 0.15f));
            Assert.AreEqual(0.15f, GhoulRules.GetBodySoundMultiplier(true, 0.15f), Tolerance);
            Assert.AreEqual(0f, GhoulRules.GetBodySoundMultiplier(true, -1f));
            Assert.AreEqual(1f, GhoulRules.GetBodySoundMultiplier(true, 3f));
        }
    }
}
