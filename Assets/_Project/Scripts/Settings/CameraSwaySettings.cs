using UnityEngine;

namespace GuardVsMannequin.Settings
{
    /// <summary>멀미가 나는 사람을 위해 카메라 흔들림을 약하게·끄기로 고를 수 있게 한다.</summary>
    public static class CameraSwaySettings
    {
        /// <summary>저장값이 이 배열의 번호라 순서를 바꾸면 안 되고, 값이 없던 예전 설정을 위해 0번은 보통이다.</summary>
        public static readonly (float scale, string labelKey)[] Modes =
        {
            (1f, "options.cameraSway.normal"),
            (0.5f, "options.cameraSway.weak"),
            (0f, "options.cameraSway.off")
        };

        /// <summary>
        /// 저장값이 손상되거나 항목 수가 줄었을 때 배열 범위를 벗어나지 않게 한다.
        /// </summary>
        public static int ClampMode(int modeIndex)
        {
            return Mathf.Clamp(modeIndex, 0, Modes.Length - 1);
        }

        /// <summary>흔들림 계산 쪽이 번호를 몰라도 되도록 바로 곱할 배율로 바꿔 준다.</summary>
        public static float GetScale(GameOptions options)
        {
            return Modes[ClampMode(options.cameraSway)].scale;
        }
    }
}
