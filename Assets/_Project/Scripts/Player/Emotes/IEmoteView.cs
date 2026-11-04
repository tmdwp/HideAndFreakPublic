namespace GuardVsMannequin.Player.Emotes
{
    /// <summary>
    /// 감정 표현을 화면에 재생한다.
    /// 오브젝트에 여러 구현이 있으면 컴포넌트 순서대로 시도해 처음 성공한 것만 재생한다. (기본은 외형 애니메이터 하나)
    /// </summary>
    public interface IEmoteView
    {
        /// <summary>
        /// 감정 표현을 재생한다. 재생할 수 없으면 false를 반환해 다음 구현에 넘긴다.
        /// </summary>
        /// <param name="emoteId">1부터 시작하는 감정 표현 번호.</param>
        bool TryPlay(int emoteId);
    }
}
