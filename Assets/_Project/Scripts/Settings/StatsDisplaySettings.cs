using UnityEngine;

namespace GuardVsMannequin.Settings
{
    /// <summary>화면 구석의 FPS·핑 표시가 방해되는 사람도 있어서 무엇을 보일지 고를 수 있게 한다.</summary>
    public static class StatsDisplaySettings
    {
        /// <summary>저장값이 이 배열의 번호라 순서를 바꾸면 안 되고, 값이 없던 예전 설정을 위해 0번은 둘 다 표시다.</summary>
        public static readonly (bool showFps, bool showPing, string labelKey)[] Modes =
        {
            (true, true, "options.statsDisplay.all"),
            (true, false, "options.statsDisplay.fpsOnly"),
            (false, false, "options.statsDisplay.off")
        };

        /// <summary>
        /// 저장값이 손상되거나 항목 수가 줄었을 때 배열 범위를 벗어나지 않게 한다.
        /// </summary>
        public static int ClampMode(int modeIndex)
        {
            return Mathf.Clamp(modeIndex, 0, Modes.Length - 1);
        }

        /// <summary>표시 쪽이 번호를 몰라도 되도록 보일 항목으로 바꿔 준다.</summary>
        public static (bool showFps, bool showPing) Get(GameOptions options)
        {
            (bool showFps, bool showPing, string _) = Modes[ClampMode(options.statsDisplay)];
            return (showFps, showPing);
        }
    }
}
