using GuardVsMannequin.Cameras;
using GuardVsMannequin.Core;
using GuardVsMannequin.Objectives;
using GuardVsMannequin.Player;

namespace GuardVsMannequin.UI
{
    /// <summary>HUD 영역들이 씬을 직접 찾지 않도록 함께 읽을 게임 정보를 묶는다.</summary>
    public sealed class HudContext
    {
        public IGameStateProvider State { get; }

        /// <summary>호스트만 가능하므로 호출하는 쪽이 권한을 확인해야 한다.</summary>
        public IMatchControl MatchControl { get; }

        public PlayerRegistry PlayerRegistry { get; }
        public ObjectiveTracker ObjectiveTracker { get; }
        public MatchResult MatchResult { get; }

        /// <summary>관전 카메라가 없는 씬도 있어 null일 수 있다.</summary>
        public SpectatorController Spectator { get; }

        public LocalPlayerTracker LocalPlayer { get; }

        /// <summary>
        /// 영역마다 씬에서 따로 찾지 않도록 한곳에서 모아 넘긴다.
        /// </summary>
        public HudContext(IGameStateProvider state, IMatchControl matchControl, PlayerRegistry playerRegistry,
            ObjectiveTracker objectiveTracker, MatchResult matchResult, SpectatorController spectator, LocalPlayerTracker localPlayer)
        {
            State = state;
            MatchControl = matchControl;
            PlayerRegistry = playerRegistry;
            ObjectiveTracker = objectiveTracker;
            MatchResult = matchResult;
            Spectator = spectator;
            LocalPlayer = localPlayer;
        }
    }
}
