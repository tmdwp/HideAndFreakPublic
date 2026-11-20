using GuardVsMannequin.Core;

namespace GuardVsMannequin.UI
{
    /// <summary>어떤 단계 전환을 화면 가운데에 크게 알릴지 정한다. 테스트할 수 있도록 값만 받는다.</summary>
    public static class PhaseAnnouncementRules
    {
        /// <summary>
        /// 판 시작과 헌트 타임 시작만 알린다. 헌트 타임은 도중에 들어온 사람에게 이미 지난 일이라 이전 단계가 진행 중일 때만 알린다.
        /// </summary>
        public static bool ShouldAnnounce(GameState previous, GameState current)
        {
            if (previous == current)
                return false;
            return current switch
            {
                GameState.Playing => previous is GameState.Waiting or GameState.Preparation or GameState.Ended,
                GameState.Hunting => previous == GameState.Playing,
                _ => false
            };
        }
    }
}
