namespace GuardVsMannequin.Core.States
{
    /// <summary>새 규칙을 핸들러 추가·교체로 확장하도록 한 상태의 서버 로직을 맡는다.</summary>
    public interface IGameStateHandler
    {
        GameState State { get; }

        /// <summary>
        /// 단계마다 타이머 등 준비할 것이 달라서 진입 처리를 핸들러에 맡긴다.
        /// </summary>
        void Enter(IGameStateContext context);

        /// <summary>
        /// 전환 시점을 상태 기계가 알 필요 없도록, 넘어갈 상태가 있으면 그 상태를, 아니면 null을 돌려준다.
        /// </summary>
        GameState? Tick(IGameStateContext context, float deltaTime);

        /// <summary>
        /// 다음 단계에 이 단계의 타이머 등이 남지 않도록 정리할 기회를 준다.
        /// </summary>
        void Exit(IGameStateContext context);
    }
}
