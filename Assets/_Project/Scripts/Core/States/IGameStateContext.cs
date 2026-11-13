namespace GuardVsMannequin.Core.States
{
    /// <summary>핸들러가 구체적인 NetworkBehaviour에 의존하지 않도록 상태 머신의 최소 기능만 연다.</summary>
    public interface IGameStateContext
    {
        GameSettings Settings { get; }

        /// <summary>단위는 초.</summary>
        float RemainingTime { get; }

        /// <summary>
        /// 핸들러가 동기화 타이머를 직접 만지지 않고 단계 길이를 정할 수 있게 한다.
        /// </summary>
        void StartTimer(float seconds);

        /// <summary>
        /// 시간 제한이 없는 단계에서 이전 단계의 남은 시간이 보이지 않도록 멈춘다.
        /// </summary>
        void StopTimer();
    }
}
