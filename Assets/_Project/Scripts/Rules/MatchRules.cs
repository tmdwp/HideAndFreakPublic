using GuardVsMannequin.Core;
using GuardVsMannequin.Core.States;
using GuardVsMannequin.Objectives;
using GuardVsMannequin.Player;
using UnityEngine;

namespace GuardVsMannequin.Rules
{
    /// <summary>
    /// 상태 머신과 승패 판정을 잇는다. (서버에서만 호출됨)
    /// 씬의 현재 값(생존자 수, 목표 달성)을 모아 <see cref="MatchJudge"/>에 판정을 맡기고, 판이 끝나면 결과를 기록한다.
    /// </summary>
    public class MatchRules : MonoBehaviour, IMatchRules
    {
        [SerializeField] private PlayerRegistry playerRegistry;
        [SerializeField] private ObjectiveTracker objectiveTracker;
        [SerializeField] private MatchResult matchResult;

        /// <summary>
        /// 탐색 단계의 전환 조건을 평가한다.
        /// </summary>
        public GameState? EvaluatePlaying(IGameStateContext context)
        {
            bool huntReady = MatchJudge.IsHuntReady(context.Settings, context.RemainingTime, objectiveTracker.IsComplete);
            return Apply(MatchJudge.JudgePlaying(playerRegistry.AliveGuardCount, playerRegistry.AliveMannequinCount, huntReady, context.RemainingTime));
        }

        /// <summary>
        /// 사냥 시간의 전환 조건을 평가한다.
        /// </summary>
        public GameState? EvaluateHunting(IGameStateContext context)
        {
            return Apply(MatchJudge.JudgeHunting(playerRegistry.AliveGuardCount, playerRegistry.AliveMannequinCount, context.RemainingTime));
        }

        /// <summary>
        /// 판이 끝나는 판정이면 결과를 기록하고, 다음 상태를 반환한다.
        /// 테스트 방은 승패·시간으로 단계를 넘기지 않는다. (테스트 메뉴로만 사냥 시간을 켜고 끔)
        /// </summary>
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
