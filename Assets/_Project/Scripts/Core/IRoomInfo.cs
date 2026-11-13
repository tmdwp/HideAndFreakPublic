namespace GuardVsMannequin.Core
{
    /// <summary>게임 씬이 룸 구현에 의존하지 않도록 필요한 룸 정보만 인터페이스로 둔다.</summary>
    public interface IRoomInfo
    {
        /// <summary>호스트가 룸에서 정한 값으로 판을 시작해야 해서 게임 씬에 넘긴다.</summary>
        GameSettings Settings { get; }

        /// <summary>판 도중에 들어온 사람이 상태가 꼬이지 않도록 새 참가를 막는 데 쓴다.</summary>
        bool IsInGame { get; }

        /// <summary>배치를 보내지 않고 모든 PC가 같은 배치를 만드는 시드(0이면 섞지 않음).</summary>
        int PropLayoutSeed { get; }

        /// <summary>1인 시작·승패 생략·테스트 메뉴를 켜는 테스트 방 여부(서버에서만 정확).</summary>
        bool IsTestRoom { get; }

        /// <summary>
        /// 이름을 아직 받지 못한 클라이언트도 화면에 표시할 수 있도록 "Player N"으로 대신한다.
        /// </summary>
        string GetDisplayName(int clientId);
    }

    /// <summary>서버 전용 룸 ↔ 게임 씬 전환 흐름.</summary>
    public interface ISceneFlow
    {
        /// <summary>
        /// 판이 끝나면 다음 판 설정을 다시 고를 수 있도록 모두를 룸 씬으로 돌려보낸다.
        /// </summary>
        void ReturnToRoom();

        /// <summary>
        /// 로딩이 느린 플레이어를 접속 끊김으로 오인해 내보내지 않도록, 최근에 로딩 중이라고 알렸는지 확인한다.
        /// </summary>
        bool IsClientLoading(int clientId);
    }
}
