namespace GuardVsMannequin.Core
{
    /// <summary>호스트만 실행할 수 있는 판 진행 명령.</summary>
    public interface IMatchControl
    {
        /// <summary>UI가 호스트 전용 버튼을 숨길지 정하려고 쓴다.</summary>
        bool CanControlMatch { get; }

        /// <summary>클라이언트가 판을 끝낼 수 없도록 호스트가 아니면 무시하고 모두를 룸으로 돌려보낸다.</summary>
        void ReturnToRoom();
    }
}
