namespace GuardVsMannequin.Core.States
{
    /// <summary>호스트가 시작을 요청할 때까지 아무것도 하지 않는 접속 대기 상태.</summary>
    public sealed class WaitingStateHandler : IGameStateHandler
    {
        public GameState State => GameState.Waiting;

        /// <summary>
        /// 대기에는 시간 제한이 없으므로 이전 판의 타이머가 보이지 않게 멈춘다.
        /// </summary>
        public void Enter(IGameStateContext context)
        {
            context.StopTimer();
        }

        /// <summary>
        /// 호스트의 시작 요청으로만 넘어가야 하므로 스스로는 전환하지 않는다.
        /// </summary>
        public GameState? Tick(IGameStateContext context, float deltaTime)
        {
            return null;
        }

        /// <summary>
        /// 인터페이스를 채우기 위한 것으로, 정리할 것이 없다.
        /// </summary>
        public void Exit(IGameStateContext context) { }
    }

    /// <summary>스폰이 끝난 다음 틱에 바로 탐색으로 넘어가는 준비 상태.</summary>
    public sealed class PreparationStateHandler : IGameStateHandler
    {
        public GameState State => GameState.Preparation;

        /// <summary>
        /// 이전 판의 타이머가 남아 보이지 않도록 멈춘다.
        /// </summary>
        public void Enter(IGameStateContext context) => context.StopTimer();

        /// <summary>
        /// 시작 직후 행동을 막지 않기로 해서 기다리지 않고, 스폰을 끝낼 한 틱만 머문다.
        /// </summary>
        public GameState? Tick(IGameStateContext context, float deltaTime) => GameState.Playing;

        /// <summary>
        /// 인터페이스를 채우기 위한 것으로, 정리할 것이 없다.
        /// </summary>
        public void Exit(IGameStateContext context) { }
    }

    /// <summary>타이머를 시작하고 전환 여부는 규칙에 맡기는 탐색 단계.</summary>
    public sealed class PlayingStateHandler : IGameStateHandler
    {
        private readonly IMatchRules rules;

        public GameState State => GameState.Playing;

        /// <summary>
        /// 테스트 방처럼 승패 규칙을 바꿔 끼울 수 있도록 밖에서 받는다.
        /// </summary>
        public PlayingStateHandler(IMatchRules rules)
        {
            this.rules = rules;
        }

        /// <summary>규칙이 남은 시간으로 판정하므로 진입할 때 제한 시간을 건다.</summary>
        public void Enter(IGameStateContext context) => context.StartTimer(context.Settings.playTime);

        /// <summary>전환 조건이 규칙마다 달라서 규칙에 맡긴다.</summary>
        public GameState? Tick(IGameStateContext context, float deltaTime) => rules.EvaluatePlaying(context);

        /// <summary>승패로 일찍 끝났을 때 남은 시간이 다음 단계에 보이지 않도록 멈춘다.</summary>
        public void Exit(IGameStateContext context) => context.StopTimer();
    }

    /// <summary>타이머를 시작하고 전환 여부는 규칙에 맡기는 사냥 시간.</summary>
    public sealed class HuntingStateHandler : IGameStateHandler
    {
        private readonly IMatchRules rules;

        public GameState State => GameState.Hunting;

        /// <summary>
        /// 테스트 방처럼 승패 규칙을 바꿔 끼울 수 있도록 밖에서 받는다.
        /// </summary>
        public HuntingStateHandler(IMatchRules rules)
        {
            this.rules = rules;
        }

        /// <summary>규칙이 남은 시간으로 판정하므로 진입할 때 사냥 시간을 건다.</summary>
        public void Enter(IGameStateContext context) => context.StartTimer(context.Settings.huntTime);

        /// <summary>전환 조건이 규칙마다 달라서 규칙에 맡긴다.</summary>
        public GameState? Tick(IGameStateContext context, float deltaTime) => rules.EvaluateHunting(context);

        /// <summary>승패로 일찍 끝났을 때 남은 시간이 다음 단계에 보이지 않도록 멈춘다.</summary>
        public void Exit(IGameStateContext context) => context.StopTimer();
    }

    /// <summary>일정 시간 결과를 보여 준 뒤 대기로 돌아가는 종료 단계.</summary>
    public sealed class EndedStateHandler : TimedStateHandler
    {
        public override GameState State => GameState.Ended;
        protected override GameState NextState => GameState.Waiting;

        /// <summary>
        /// 결과 화면 길이는 호스트가 바꿀 값이 아니어서 조정 파일 값을 쓴다.
        /// </summary>
        protected override float GetDuration(GameSettings settings) => GameTuning.ResultDisplayDuration;
    }
}
