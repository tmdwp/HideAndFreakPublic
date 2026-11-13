namespace GuardVsMannequin.Core.States
{
    /// <summary>
    /// 정해진 시간이 지나면 다음 상태로 넘어가는 상태의 공통 구현.
    /// </summary>
    public abstract class TimedStateHandler : IGameStateHandler
    {
        public abstract GameState State { get; }

        protected abstract GameState NextState { get; }

        /// <summary>
        /// 단계 길이가 호스트 설정에서 오기도 하고 조정 파일에서 오기도 해서 하위 클래스가 정한다.
        /// </summary>
        protected abstract float GetDuration(GameSettings settings);

        /// <summary>
        /// 진입할 때 정한 길이만큼만 진행되도록 바로 타이머를 건다.
        /// </summary>
        public virtual void Enter(IGameStateContext context)
        {
            context.StartTimer(GetDuration(context.Settings));
        }

        /// <summary>
        /// 시간 말고 다른 전환 조건이 없으므로 타이머만 본다.
        /// </summary>
        public virtual GameState? Tick(IGameStateContext context, float deltaTime)
        {
            return context.RemainingTime <= 0f ? NextState : null;
        }

        /// <summary>
        /// 외부에서 단계를 일찍 끝냈을 때 남은 타이머가 다음 단계에 보이지 않도록 멈춘다.
        /// </summary>
        public virtual void Exit(IGameStateContext context)
        {
            context.StopTimer();
        }
    }
}
