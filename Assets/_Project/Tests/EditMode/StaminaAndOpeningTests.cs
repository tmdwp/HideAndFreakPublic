using GuardVsMannequin.Core;
using GuardVsMannequin.Player;
using NUnit.Framework;

namespace GuardVsMannequin.Tests
{
    /// <summary>
    /// 조정 파일 값에 따라 달라지는 규칙(스태미나, 판 시작 구간)을 값 자체가 아닌 규칙으로 확인한다.
    /// </summary>
    public class StaminaAndOpeningTests
    {
        private const float Step = 0.05f;

        /// <summary>상태 기계 없이 시작 구간 판정을 시험하려고 필요한 값만 돌려준다.</summary>
        private sealed class FakeStateProvider : IGameStateProvider
        {
            public GameState CurrentState { get; set; }
            public GameSettings Settings { get; set; }
            public float RemainingTime { get; set; }
        }

        /// <summary>스태미나를 다 쓰면 소진 상태가 되어 키를 눌러도 달리지 못하는지 확인한다.</summary>
        [Test]
        public void Stamina_SprintUntilEmpty_BecomesExhausted()
        {
            var stamina = new StaminaModel();

            SprintUntilExhausted(stamina);

            Assert.IsTrue(stamina.IsExhausted);
            Assert.AreEqual(0f, stamina.Current);
            Assert.IsFalse(stamina.Tick(true, Step));
        }

        /// <summary>바닥난 뒤 다시 달릴 수 있는 비율까지 차야 달리기가 풀리는지 확인한다.</summary>
        [Test]
        public void Stamina_RecoversToRestartRatio_ThenSprintsAgain()
        {
            var stamina = new StaminaModel();
            SprintUntilExhausted(stamina);

            int guard = 0;
            while (stamina.Current < SprintRules.RestartRatio && guard++ < 100000)
                stamina.Tick(false, Step);

            Assert.IsTrue(stamina.Tick(true, Step));
            Assert.IsFalse(stamina.IsExhausted);
        }

        /// <summary>잠깐 멈췄다 다시 달리는 것으로 회복을 챙기지 못하도록 멈춘 직후에는 차지 않는지 확인한다.</summary>
        [Test]
        public void Stamina_WaitsRecoverDelayBeforeRefilling()
        {
            Assume.That(SprintRules.RecoverDelay > Step * 2f, "조정 파일의 회복 대기 시간이 0에 가까우면 확인할 수 없음");
            var stamina = new StaminaModel();
            stamina.Tick(true, 1f);
            float afterSprint = stamina.Current;

            stamina.Tick(false, Step);

            Assert.AreEqual(afterSprint, stamina.Current);
        }

        /// <summary>탐색 시작 직후에만 시작 구간이고, 다른 단계이거나 타이머를 아직 못 받았으면 아닌지 확인한다.</summary>
        [Test]
        public void MatchOpening_ActiveOnlyAtStartOfPlaying()
        {
            Assume.That(GameTuning.MatchOpeningTime > 0.5f, "시작 구간이 0이면 확인할 수 없음");
            var provider = new FakeStateProvider
            {
                CurrentState = GameState.Playing,
                Settings = new GameSettings { playTime = 300f },
                RemainingTime = 299.9f
            };
            Assert.IsTrue(MatchOpeningRules.IsActive(provider));

            provider.RemainingTime = 300f - GameTuning.MatchOpeningTime - 0.1f;
            Assert.IsFalse(MatchOpeningRules.IsActive(provider), "시작 구간 지남");

            provider.RemainingTime = 0f;
            Assert.IsFalse(MatchOpeningRules.IsActive(provider), "타이머 동기화 전");

            provider.RemainingTime = 299.9f;
            provider.CurrentState = GameState.Hunting;
            Assert.IsFalse(MatchOpeningRules.IsActive(provider), "사냥 시간");

            Assert.IsFalse(MatchOpeningRules.IsActive(null));
        }

        /// <summary>
        /// 더 달리면 소진 중 회복이 섞여 값이 흔들리므로, 소진되는 순간 멈춘다. 끝나지 않는 경우를 막으려고 반복 횟수를 묶는다.
        /// </summary>
        private static void SprintUntilExhausted(StaminaModel stamina)
        {
            int guard = 0;
            while (!stamina.IsExhausted && guard++ < 100000)
                stamina.Tick(true, Step);
        }
    }
}
