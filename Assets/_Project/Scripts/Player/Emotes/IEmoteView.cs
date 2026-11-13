namespace GuardVsMannequin.Player.Emotes
{
    /// <summary>여러 구현 중 처음 성공한 것만 재생하도록 감정 표현 재생을 인터페이스로 둔다.</summary>
    public interface IEmoteView
    {
        /// <summary>
        /// 재생할 수 없는 외형도 있으므로 false를 돌려 다음 구현에 넘긴다.
        /// </summary>
        /// <param name="emoteId">1부터 시작하는 감정 표현 번호.</param>
        bool TryPlay(int emoteId);
    }
}
