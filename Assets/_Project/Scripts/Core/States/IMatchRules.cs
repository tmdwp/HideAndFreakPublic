namespace GuardVsMannequin.Core.States
{
    /// <summary>상태 핸들러가 실제 규칙을 몰라도 되도록 승패와 사냥 진입 판정을 인터페이스로 둔다.</summary>
    public interface IMatchRules
    {
        /// <summary>탐색 단계는 사냥 진입과 종료 두 갈래가 있어 어느 쪽으로 갈지 규칙이 정한다.</summary>
        GameState? EvaluatePlaying(IGameStateContext context);

        /// <summary>사냥 시간에는 생존자 수로 승패가 갈리므로 규칙이 판단한다.</summary>
        GameState? EvaluateHunting(IGameStateContext context);
    }

    /// <summary>규칙 컴포넌트가 없을 때 쓰는 시간만으로 단계를 넘기는 기본 규칙.</summary>
    public sealed class TimeOnlyMatchRules : IMatchRules
    {
        /// <summary>규칙 컴포넌트 없이도 판이 끝없이 이어지지 않도록 시간이 다 되면 끝낸다.</summary>
        public GameState? EvaluatePlaying(IGameStateContext context)
        {
            return context.RemainingTime <= 0f ? GameState.Ended : null;
        }

        /// <summary>규칙 컴포넌트 없이도 판이 끝없이 이어지지 않도록 시간이 다 되면 끝낸다.</summary>
        public GameState? EvaluateHunting(IGameStateContext context)
        {
            return context.RemainingTime <= 0f ? GameState.Ended : null;
        }
    }
}
