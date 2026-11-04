using UnityEngine;

namespace GuardVsMannequin.Settings
{
    /// <summary>
    /// 프레임 제한을 고르고 적용한다. 기본은 모니터 주사율(수직 동기화)이다.
    /// 제한이 없으면 로비·룸 화면에서도 수백 fps로 돌아 노트북이 뜨거워지고, 발열로 성능이 떨어져 게임 중 프레임까지 낮아질 수 있다.
    /// </summary>
    public static class FrameRateSettings
    {
        /// <summary>제한 없음을 뜻하는 목표 프레임.</summary>
        private const int Unlimited = -1;

        /// <summary>
        /// 고를 수 있는 프레임 제한과 표시 문구 key. (순서 = GameOptions.frameLimit 번호, 0 = 수직 동기화)
        /// targetFps가 0이면 수직 동기화를 켠다.
        /// </summary>
        public static readonly (int targetFps, string labelKey)[] Modes =
        {
            (0, "options.frameLimit.vsync"),
            (60, "options.frameLimit.60"),
            (120, "options.frameLimit.120"),
            (144, "options.frameLimit.144"),
            (Unlimited, "options.frameLimit.unlimited")
        };

        /// <summary>
        /// 설정의 프레임 제한 번호를 목록 범위로 맞춘다.
        /// </summary>
        public static int ClampMode(int modeIndex)
        {
            return Mathf.Clamp(modeIndex, 0, Modes.Length - 1);
        }

        /// <summary>
        /// 설정의 프레임 제한을 적용한다. 수직 동기화를 켜면 목표 프레임은 무시된다.
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
