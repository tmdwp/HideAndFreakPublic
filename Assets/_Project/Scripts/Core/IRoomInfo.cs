namespace GuardVsMannequin.Core
{
    /// <summary>
    /// 게임 씬이 룸에서 필요로 하는 정보. (룸 구현에 직접 의존하지 않도록 분리, DIP)
    /// </summary>
    public interface IRoomInfo
    {
        /// <summary>호스트가 룸에서 정한 게임 설정.</summary>
        GameSettings Settings { get; }

        /// <summary>게임 씬에서 판이 진행 중인지 여부. (진행 중에는 새 참가를 받지 않음)</summary>
        bool IsInGame { get; }

        /// <summary>이번 판의 변신 사물 배치 시드. 모든 PC가 이 값으로 같은 배치를 만든다. 0이면 섞지 않는다.</summary>
        int PropLayoutSeed { get; }

        /// <summary>테스트 방인지 여부. (서버에서만 정확함: 1인 시작, 승패 판정 없음, 테스트 메뉴)</summary>
        bool IsTestRoom { get; }

        /// <summary>
        /// 클라이언트의 표시 이름을 반환한다. 모르면 "Player N".
        /// </summary>
        string GetDisplayName(int clientId);
    }

    /// <summary>
    /// 룸 ↔ 게임 씬 전환 흐름. (서버 전용)
    /// </summary>
    public interface ISceneFlow
    {
        /// <summary>
        /// 모든 플레이어를 룸 씬으로 돌려보낸다.
        /// </summary>
        void ReturnToRoom();

        /// <summary>
        /// 클라이언트가 최근에 "아직 씬을 불러오는 중"이라고 알렸는지 여부. (느리지만 살아 있는 플레이어를 기다리기 위함)
        /// </summary>
        bool IsClientLoading(int clientId);
    }
}
