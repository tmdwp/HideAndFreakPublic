using UnityEngine;

namespace GuardVsMannequin.Settings
{
    /// <summary>
    /// 카메라 흔들림(캐릭터 몸의 흔들림을 카메라가 따라가는 정도)을 고른다. 기본은 "보통"이다.
    /// 멀미가 나는 사람을 위해 약하게·끄기를 둔다.
    /// </summary>
    public static class CameraSwaySettings
    {
        /// <summary>
        /// 고를 수 있는 흔들림 배율과 표시 문구 key. (순서 = GameOptions.cameraSway 번호, 0 = 보통)
        /// 예전에 저장한 설정에는 값이 없어 0이 되므로 0번을 기본값(보통)으로 둔다.
        /// </summary>
        public static readonly (float scale, string labelKey)[] Modes =
        {
            (1f, "options.cameraSway.normal"),
            (0.5f, "options.cameraSway.weak"),
            (0f, "options.cameraSway.off")
        };

        /// <summary>
        /// 설정의 흔들림 번호를 목록 범위로 맞춘다.
        /// </summary>
        public static int ClampMode(int modeIndex)
        {
            return Mathf.Clamp(modeIndex, 0, Modes.Length - 1);
        }

        /// <summary>
        /// 설정의 카메라 흔들림 배율(0 = 끔, 1 = 몸 흔들림 그대로).
        /// </summary>
        public static float GetScale(GameOptions options)
        {
            return Modes[ClampMode(options.cameraSway)].scale;
        }
    }
}
