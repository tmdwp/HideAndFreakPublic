using GuardVsMannequin.Core;
using GuardVsMannequin.Core.States;
using GuardVsMannequin.Objectives;
using GuardVsMannequin.Player;
using UnityEngine;

namespace GuardVsMannequin.Rules
{
    /// <summary>상태 머신이 판정 규칙을 몰라도 되도록 씬 값을 모아 MatchJudge에 판정을 맡긴다.</summary>
    public class MatchRules : MonoBehaviour, IMatchRules
    {
        [SerializeField] private PlayerRegistry playerRegistry;
        [SerializeField] private ObjectiveTracker objectiveTracker;
        [SerializeField] private MatchResult matchResult;

        /// <summary>
        /// 판정 로직은 씬을 모르는 MatchJudge에 두고, 여기서는 씬에서 값만 모아 넘긴다.
        /// </summary>
        public GameState? EvaluatePlaying(IGameStateContext context)
        {
            bool huntReady = MatchJudge.IsHuntReady(context.Settings, context.RemainingTime, objectiveTracker.IsComplete);
            return Apply(MatchJudge.JudgePlaying(playerRegistry.AliveGuardCount, playerRegistry.AliveMannequinCount, huntReady, context.RemainingTime));
        }

        /// <summary>
        /// 판정 로직은 씬을 모르는 MatchJudge에 두고, 여기서는 씬에서 값만 모아 넘긴다.
        /// </summary>
        public GameState? EvaluateHunting(IGameStateContext context)
        {
            return Apply(MatchJudge.JudgeHunting(playerRegistry.AliveGuardCount, playerRegistry.AliveMannequinCount, context.RemainingTime));
        }

        /// <summary>MatchJudge는 값만 돌려주므로 결과 기록은 여기서 하고, 테스트 방은 승패로 단계를 넘기지 않는다.</summary>
        private GameState? Apply(MatchVerdict verdict)
        {
            if (GameServices.Room?.IsTestRoom ?? false)
                return null;

            if (verdict.EndsMatch)
                matchResult.SetResult(verdict.Winner, verdict.Reason);
            return verdict.NextState;
        }
    }
}
