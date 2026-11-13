using GuardVsMannequin.Core;
using GuardVsMannequin.Rules;
using NUnit.Framework;

namespace GuardVsMannequin.Tests
{
    /// <summary>
    /// 탐색·사냥 시간의 승패 판정 순서와 사냥 시간 진입 조건을 확인한다.
    /// </summary>
    public class MatchJudgeTests
    {
        /// <summary>같은 틱에 목표 달성과 마네킹 전멸이 겹치면 가드 승리가 먼저인지 확인한다.</summary>
        [Test]
        public void JudgePlaying_AllMannequinsDead_GuardWinsEvenIfHuntReady()
        {
            MatchVerdict verdict = MatchJudge.JudgePlaying(1, 0, true, 100f);

            Assert.IsTrue(verdict.EndsMatch);
            Assert.AreEqual(MatchWinner.Guard, verdict.Winner);
            Assert.AreEqual(WinReason.AllMannequinsEliminated, verdict.Reason);
        }

        /// <summary>가드가 모두 나가면 판을 이어갈 수 없어 마네킹 승리로 끝나는지 확인한다.</summary>
        [Test]
        public void JudgePlaying_NoGuards_MannequinWins()
        {
            MatchVerdict verdict = MatchJudge.JudgePlaying(0, 2, false, 100f);

            Assert.AreEqual(MatchWinner.Mannequin, verdict.Winner);
            Assert.AreEqual(WinReason.AllGuardsEliminated, verdict.Reason);
        }

        /// <summary>사냥 조건을 채우면 판이 끝나지 않고 사냥 시간으로 넘어가는지 확인한다.</summary>
        [Test]
        public void JudgePlaying_HuntReady_MovesToHunting()
        {
            MatchVerdict verdict = MatchJudge.JudgePlaying(1, 2, true, 0f);

            Assert.IsFalse(verdict.EndsMatch);
            Assert.AreEqual(GameState.Hunting, verdict.NextState);
            Assert.AreEqual(MatchWinner.None, verdict.Winner);
        }

        /// <summary>목표를 못 채우고 시간이 다 되면 가드 승리인지, 남은 시간이 있으면 계속하는지 확인한다.</summary>
        [Test]
        public void JudgePlaying_TimeOut_GuardWinsOtherwiseContinues()
        {
            MatchVerdict timedOut = MatchJudge.JudgePlaying(1, 2, false, 0f);
            Assert.AreEqual(MatchWinner.Guard, timedOut.Winner);
            Assert.AreEqual(WinReason.ObjectiveFailed, timedOut.Reason);

            MatchVerdict running = MatchJudge.JudgePlaying(1, 2, false, 10f);
            Assert.IsNull(running.NextState);
        }

        /// <summary>사냥 시간은 마네킹이 공격하는 단계라 양쪽이 동시에 0이면 마네킹 승리가 먼저인지 확인한다.</summary>
        [Test]
        public void JudgeHunting_BothZero_MannequinWins()
        {
            MatchVerdict verdict = MatchJudge.JudgeHunting(0, 0, 10f);

            Assert.AreEqual(MatchWinner.Mannequin, verdict.Winner);
        }

        /// <summary>사냥 시간을 버티면 가드 승리, 시간이 남았으면 계속하는지 확인한다.</summary>
        [Test]
        public void JudgeHunting_GuardsSurviveUntilTimeOut()
        {
            MatchVerdict timedOut = MatchJudge.JudgeHunting(1, 1, 0f);
            Assert.AreEqual(MatchWinner.Guard, timedOut.Winner);
            Assert.AreEqual(WinReason.GuardsSurvived, timedOut.Reason);

            Assert.IsNull(MatchJudge.JudgeHunting(1, 1, 5f).NextState);
        }

        /// <summary>목표가 있는 방은 목표 달성만, 목표 0회 방은 보호 시간이 지난 뒤에만 사냥 조건이 되는지 확인한다.</summary>
        [Test]
        public void IsHuntReady_UsesObjectiveOrProtectionTime()
        {
            var withObjectives = new GameSettings { objectiveCount = 2, playTime = 300f, huntDelay = 30f };
            Assert.IsFalse(MatchJudge.IsHuntReady(withObjectives, 0f, false));
            Assert.IsTrue(MatchJudge.IsHuntReady(withObjectives, 299f, true));

            var timed = new GameSettings { objectiveCount = 0, playTime = 300f, huntDelay = 30f };
            Assert.IsFalse(MatchJudge.IsHuntReady(timed, 280f, true), "보호 시간 20초 경과 — 아직");
            Assert.IsTrue(MatchJudge.IsHuntReady(timed, 270f, false), "보호 시간 30초 경과 — 진입");
        }
    }
}
