using GuardVsMannequin.Localization;
using UnityEngine;

namespace GuardVsMannequin.Settings
{
    /// <summary>시야각을 넓혀 멀미를 줄이거나 좁혀 멀리 보도록 고를 수 있게 한다.</summary>
    public static class FieldOfViewSettings
    {
        /// <summary>저장값이 이 배열의 번호라 순서를 바꾸면 안 되고, 값이 없던 예전 설정을 위해 0번은 카메라에 정해 둔 기본값이다.</summary>
        public static readonly float[] Modes = { 0f, 60f, 75f, 85f, 95f, 105f };

        /// <summary>
        /// 저장값이 손상되거나 항목 수가 줄었을 때 배열 범위를 벗어나지 않게 한다.
        /// </summary>
        public static int ClampMode(int modeIndex)
        {
            return Mathf.Clamp(modeIndex, 0, Modes.Length - 1);
        }

        /// <summary>카메라 쪽이 번호를 몰라도 되도록, 기본값이면 카메라 원래 값을 그대로 쓰게 돌려준다.</summary>
        public static float Resolve(GameOptions options, float cameraDefault)
        {
            float fieldOfView = Modes[ClampMode(options.fieldOfView)];
            return fieldOfView > 0f ? fieldOfView : cameraDefault;
        }

        /// <summary>숫자는 언어와 상관없어 그대로 쓰고, 기본값만 번역 문구로 보인다.</summary>
        public static string GetLabel(int modeIndex)
        {
            float fieldOfView = Modes[ClampMode(modeIndex)];
            return fieldOfView > 0f ? $"{fieldOfView:0}°" : Loc.Get("options.fov.default");
        }
    }

    /// <summary>배경에 묻히지 않도록 조준점 색을 고를 수 있게 한다.</summary>
    public static class CrosshairSettings
    {
        /// <summary>저장값이 이 배열의 번호라 순서를 바꾸면 안 되고, 값이 없던 예전 설정을 위해 0번은 흰색이다.</summary>
        public static readonly (Color color, string labelKey)[] Modes =
        {
            (new Color(0.93f, 0.93f, 0.93f, 1f), "options.crosshairColor.white"),
            (new Color(0.35f, 1f, 0.45f, 1f), "options.crosshairColor.green"),
            (new Color(1f, 0.9f, 0.25f, 1f), "options.crosshairColor.yellow"),
            (new Color(0.35f, 0.9f, 1f, 1f), "options.crosshairColor.cyan"),
            (new Color(1f, 0.45f, 0.85f, 1f), "options.crosshairColor.pink")
        };

        /// <summary>
        /// 저장값이 손상되거나 항목 수가 줄었을 때 배열 범위를 벗어나지 않게 한다.
        /// </summary>
        public static int ClampMode(int modeIndex)
        {
            return Mathf.Clamp(modeIndex, 0, Modes.Length - 1);
        }

        /// <summary>HUD 쪽이 번호를 몰라도 되도록 바로 쓸 색으로 바꿔 준다.</summary>
        public static Color GetColor(GameOptions options)
        {
            return Modes[ClampMode(options.crosshairColor)].color;
        }
    }

    /// <summary>켜기·끄기 두 가지뿐인 개인 설정이 같은 표시 문구를 쓰도록 모은다.</summary>
    public static class OnOffLabels
    {
        /// <summary>
        /// 두 상태의 표시 문구를 한곳에 둔다.
        /// </summary>
        public static string Get(bool isOn)
        {
            return Loc.Get(isOn ? "options.on" : "options.off");
        }
    }
}
