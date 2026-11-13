namespace GuardVsMannequin.Player
{
    /// <summary>HUD·외곽선이 구현에 의존하지 않도록 일시 효과 상태를 인터페이스로 둔다.</summary>
    public interface IStatusEffects
    {
        /// <summary>상대 진영 화면에 벽 너머 외곽선을 그릴지 정하는 데 쓴다.</summary>
        bool IsRevealed { get; }

        bool IsSlowed { get; }
    }
}
