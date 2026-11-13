using GuardVsMannequin.Core;

namespace GuardVsMannequin.Rules
{
    /// <summary>
    /// 승패 판정 결과: 다음 상태(없으면 계속), 판이 끝났다면 승리 진영과 사유.
    /// </summary>
    public readonly struct MatchVerdict
    {
        /// <summary>현재 단계를 계속하는 판정.</summary>
        public static readonly MatchVerdict Continue = new(null, MatchWinner.None, WinReason.None);

        /// <summary>바꿀 다음 상태(null이면 그대로).</summary>
        public readonly GameState? NextState;

        /// <summary>판이 끝났을 때의 승리 진영.</summary>
        public readonly MatchWinner Winner;

        /// <summary>판이 끝났을 때의 사유.</summary>
        public readonly WinReason Reason;

        /// <summary>이 판정으로 판이 끝나는지 여부.</summary>
        public bool EndsMatch => NextState == GameState.Ended;

        /// <summary>승자가 있는데 판이 계속되는 것처럼 앞뒤가 맞지 않는 판정이 생기지 않도록 생성자를 숨기고 정적 팩토리로만 만든다.</summary>
        private MatchVerdict(GameState? nextState, MatchWinner winner, WinReason reason)
        {
            NextState = nextState;
            Winner = winner;
            Reason = reason;
        }

        /// <summary>판이 끝날 때는 결과 화면에 승자와 사유가 꼭 필요하므로 둘을 함께 받는다.</summary>
        public static MatchVerdict End(MatchWinner winner, WinReason reason) => new(GameState.Ended, winner, reason);

        /// <summary>단계만 바뀌고 판은 이어지므로 승자·사유 없이 다음 상태만 담는다.</summary>
        public static MatchVerdict MoveTo(GameState state) => new(state, MatchWinner.None, WinReason.None);
    }

    /// <summary>서버와 테스트가 같은 규칙을 쓰도록 승패와 사냥 시간 진입 조건을 값만으로 판정한다.</summary>
    public static class MatchJudge
    {
        /// <summary>같은 틱에 목표 달성과 겹쳐도 가드 승리가 우선이도록 마네킹 전멸부터 판정한다.</summary>
        /// <param name="aliveGuards">살아 있는 가드 수.</param>
        /// <param name="aliveMannequins">살아 있는 마네킹 수.</param>
        /// <param name="huntReady">사냥 시간에 들어갈 조건을 채웠는지.</param>
        /// <param name="remainingTime">탐색 단계 남은 시간(초).</param>
        public static MatchVerdict JudgePlaying(int aliveGuards, int aliveMannequins, bool huntReady, float remainingTime)
        {
            if (aliveMannequins == 0)
                return MatchVerdict.End(MatchWinner.Guard, WinReason.AllMannequinsEliminated);

            // 가드가 모두 나간 경우(접속 종료)에는 마네킹 승리로 처리한다.
            if (aliveGuards == 0)
                return MatchVerdict.End(MatchWinner.Mannequin, WinReason.AllGuardsEliminated);

            if (huntReady)
                return MatchVerdict.MoveTo(GameState.Hunting);

            if (remainingTime <= 0f)
                return MatchVerdict.End(MatchWinner.Guard, WinReason.ObjectiveFailed);

            return MatchVerdict.Continue;
        }

        /// <summary>사냥 시간은 마네킹이 공격하는 단계라 가드 전멸부터 판정한다.</summary>
        /// <param name="aliveGuards">살아 있는 가드 수.</param>
        /// <param name="aliveMannequins">살아 있는 마네킹 수.</param>
        /// <param name="remainingTime">사냥 시간 남은 시간(초).</param>
        public static MatchVerdict JudgeHunting(int aliveGuards, int aliveMannequins, float remainingTime)
        {
            if (aliveGuards == 0)
                return MatchVerdict.End(MatchWinner.Mannequin, WinReason.AllGuardsEliminated);

            if (aliveMannequins == 0)
                return MatchVerdict.End(MatchWinner.Guard, WinReason.AllMannequinsEliminated);

            if (remainingTime <= 0f)
                return MatchVerdict.End(MatchWinner.Guard, WinReason.GuardsSurvived);

            return MatchVerdict.Continue;
        }

        /// <summary>목표가 0회인 방은 목표로 사냥 시간에 들어갈 수 없으므로 보호 시간 종료를 대신 조건으로 쓴다.</summary>
        public static bool IsHuntReady(GameSettings settings, float remainingTime, bool objectiveComplete)
        {
            if (!HuntTiming.IsTimedHunt(settings))
                return objectiveComplete;

            return HuntTiming.GetProtectionRemaining(settings, remainingTime) <= 0f;
        }
    }
}
