namespace GuardVsMannequin.Core
{
    /// <summary>테스트 방 게임 씬에서만 등록되는 호스트 조작.</summary>
    public interface ITestRoomActions
    {
        GameState CurrentState { get; }

        GameSettings Settings { get; }

        /// <summary>아바타가 없는 단계에서 전환하면 스폰 흐름이 꼬이므로, 탐색 단계나 사냥 시간에만 허용한다.</summary>
        bool CanSwitch { get; }

        /// <summary>혼자 두 역할을 확인하도록 지금 자리에서 가드 ↔ 마네킹으로 다시 스폰한다.</summary>
        void SwapLocalRole();

        /// <summary>목표를 채우지 않고도 구울을 확인하도록 사냥 시간을 켜고 끈다.</summary>
        void ToggleHunt();

        /// <summary>
        /// 판을 다시 시작하지 않고 수치를 바로 바꿔 보기 위해 쓴다.
        /// </summary>
        void ApplySettings(GameSettings settings);

        /// <summary>
        /// 테스트 방은 승패가 나지 않으므로, 맵을 바꾸려면 직접 룸으로 돌아가야 한다.
        /// </summary>
        void ReturnToRoom();
    }
}
