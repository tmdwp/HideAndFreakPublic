using System;

namespace GuardVsMannequin.Settings
{
    /// <summary>호스트가 정하는 게임 설정과 달리 이 PC에만 저장되는 개인 설정.</summary>
    [Serializable]
    public struct GameOptions
    {
        public const float MinSensitivity = 0.1f;
        public const float MaxSensitivity = 3f;

        /// <summary>1 = 기본.</summary>
        public float mouseSensitivity;

        /// <summary>0~1.</summary>
        public float masterVolume;

        /// <summary>0~1.</summary>
        public float bgmVolume;

        /// <summary>0~1.</summary>
        public float sfxVolume;

        /// <summary>언어 코드(비어 있으면 시스템 언어).</summary>
        public string languageCode;

        /// <summary>0이면 아직 고르지 않은 것이라 현재 화면을 유지한다.</summary>
        public int screenWidth;

        /// <summary>0이면 아직 고르지 않은 것이라 현재 화면을 유지한다.</summary>
        public int screenHeight;

        /// <summary><see cref="DisplaySettings.Modes"/>의 번호.</summary>
        public int screenMode;

        /// <summary><see cref="FrameRateSettings.Modes"/>의 번호.</summary>
        public int frameLimit;

        /// <summary><see cref="CameraSwaySettings.Modes"/>의 번호.</summary>
        public int cameraSway;

        /// <summary><see cref="StatsDisplaySettings.Modes"/>의 번호.</summary>
        public int statsDisplay;

        /// <summary><see cref="FieldOfViewSettings.Modes"/>의 번호.</summary>
        public int fieldOfView;

        /// <summary><see cref="CrosshairSettings.Modes"/>의 번호.</summary>
        public int crosshairColor;

        /// <summary>마우스를 위로 밀면 아래를 보게 한다.</summary>
        public bool invertLookY;

        /// <summary>익숙해진 뒤 화면을 넓게 쓰도록 HUD 왼쪽 조작 안내를 숨긴다. 예전 설정이 숨김으로 읽히지 않게 "숨김"으로 저장한다.</summary>
        public bool hideControlsHint;

        /// <summary>
        /// 해상도를 다시 적용하면 화면이 깜빡이므로, 화면 설정이 실제로 바뀌었을 때만 적용하려고 비교한다.
        /// </summary>
        public bool HasSameDisplay(GameOptions other)
        {
            return screenWidth == other.screenWidth && screenHeight == other.screenHeight && screenMode == other.screenMode;
        }

        /// <summary>
        /// 구조체 기본값(감도·음량 0)으로 시작하면 소리가 안 나고 마우스가 안 움직이므로 쓸 수 있는 기본값을 채운다.
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
