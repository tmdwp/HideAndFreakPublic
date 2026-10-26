namespace GuardVsMannequin.Core
{
    /// <summary>
    /// 테스트 방에서 호스트가 게임 중에 쓰는 조작. (호스트는 서버이므로 직접 호출한다)
    /// 테스트 방 게임 씬에서만 <see cref="GameServices.TestRoom"/>에 등록된다.
    /// </summary>
    public interface ITestRoomActions
    {
        /// <summary>지금 판의 상태.</summary>
        GameState CurrentState { get; }

        /// <summary>지금 판의 설정.</summary>
        GameSettings Settings { get; }

        /// <summary>역할·사냥 시간 전환을 쓸 수 있는 단계인지 여부. (탐색 단계 또는 사냥 시간)</summary>
        bool CanSwitch { get; }

        /// <summary>
        /// 내 아바타를 가드 ↔ 마네킹으로 바꾼다. 지금 자리에서 다시 스폰한다. (아바타가 없으면 가드로 스폰)
        /// </summary>
        void SwapLocalRole();

        /// <summary>
        /// 마네킹 ↔ 구울: 사냥 시간을 켜고 끈다. 끌 때는 마네킹을 같은 자리에 다시 스폰해 원래 모습으로 되돌린다.
        /// </summary>
        void ToggleHunt();

        /// <summary>
        /// 진행 중인 판의 설정을 바꾼다.
        /// </summary>
        void ApplySettings(GameSettings settings);

        /// <summary>
        /// 판을 끝내고 모두 룸으로 돌아간다. (테스트 방은 승패가 나지 않으므로 맵을 바꿀 때 사용)
        /// </summary>
        void ReturnToRoom();
    }
}
