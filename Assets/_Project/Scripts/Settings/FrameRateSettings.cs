using UnityEngine;

namespace GuardVsMannequin.Settings
{
    /// <summary>제한이 없으면 메뉴에서도 수백 fps로 돌아 노트북이 뜨거워지므로 프레임 제한을 고르고 적용한다.</summary>
    public static class FrameRateSettings
    {
        /// <summary>Unity의 targetFrameRate에서 제한 없음을 뜻하는 값.</summary>
        private const int Unlimited = -1;

        /// <summary>저장값이 이 배열의 번호라 순서를 바꾸면 안 되고, 값이 없던 예전 설정을 위해 0번은 수직 동기화다.</summary>
        public static readonly (int targetFps, string labelKey)[] Modes =
        {
            (0, "options.frameLimit.vsync"),
            (60, "options.frameLimit.60"),
            (120, "options.frameLimit.120"),
            (144, "options.frameLimit.144"),
            (Unlimited, "options.frameLimit.unlimited")
        };

        /// <summary>
        /// 저장값이 손상되거나 항목 수가 줄었을 때 배열 범위를 벗어나지 않게 한다.
        /// </summary>
        public static int ClampMode(int modeIndex)
        {
            return Mathf.Clamp(modeIndex, 0, Modes.Length - 1);
        }

        /// <summary>
        /// 수직 동기화를 켜면 Unity가 targetFrameRate를 무시하므로, 켤 때는 제한을 없앤 값으로 둬서 둘이 충돌하지 않게 한다.
        /// </summary>
        public static void Apply(GameOptions options)
        {
            int targetFps = Modes[ClampMode(options.frameLimit)].targetFps;
            bool useVSync = targetFps == 0;
            QualitySettings.vSyncCount = useVSync ? 1 : 0;
            Application.targetFrameRate = useVSync ? Unlimited : targetFps;
        }
    }
}
