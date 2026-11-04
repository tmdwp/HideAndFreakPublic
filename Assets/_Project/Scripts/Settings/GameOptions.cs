using System;

namespace GuardVsMannequin.Settings
{
    /// <summary>
    /// 플레이어 개인 설정. 게임 설정(GameSettings, 호스트가 정함)과 달리 이 PC에만 저장된다.
    /// </summary>
    [Serializable]
    public struct GameOptions
    {
        public const float MinSensitivity = 0.1f;
        public const float MaxSensitivity = 3f;

        /// <summary>마우스 감도 배율. (1 = 기본)</summary>
        public float mouseSensitivity;

        /// <summary>전체 음량(0~1).</summary>
        public float masterVolume;

        /// <summary>배경 음악 음량(0~1).</summary>
        public float bgmVolume;

        /// <summary>효과음 음량(0~1).</summary>
        public float sfxVolume;

        /// <summary>표시 언어 코드(ko, en ...). 비어 있으면 시스템 언어를 따른다.</summary>
        public string languageCode;

        /// <summary>화면 가로 해상도. 0이면 아직 고르지 않은 것(현재 화면 유지).</summary>
        public int screenWidth;

        /// <summary>화면 세로 해상도. 0이면 아직 고르지 않은 것(현재 화면 유지).</summary>
        public int screenHeight;

        /// <summary>화면 모드. (<see cref="DisplaySettings.Modes"/>의 번호, 0 = 전체 화면 창)</summary>
        public int screenMode;

        /// <summary>프레임 제한. (<see cref="FrameRateSettings.Modes"/>의 번호, 0 = 모니터 주사율 수직 동기화)</summary>
        public int frameLimit;

        /// <summary>카메라 흔들림. (<see cref="CameraSwaySettings.Modes"/>의 번호, 0 = 보통)</summary>
        public int cameraSway;

        /// <summary>
        /// 화면 설정이 같은지 비교한다. (다를 때만 해상도를 다시 적용하기 위함)
        /// </summary>
        public bool HasSameDisplay(GameOptions other)
        {
            return screenWidth == other.screenWidth && screenHeight == other.screenHeight && screenMode == other.screenMode;
        }

        /// <summary>
        /// 기본값으로 채운 설정을 만든다.
        /// </summary>
        public static GameOptions CreateDefault()
        {
            return new GameOptions
            {
                mouseSensitivity = 1f,
                masterVolume = 1f,
                bgmVolume = 0.7f,
                sfxVolume = 1f,
                languageCode = string.Empty
            };
        }
    }
}
