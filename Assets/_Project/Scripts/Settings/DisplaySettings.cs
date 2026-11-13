using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Settings
{
    /// <summary>해상도와 화면 모드를 고르고 적용하며, 재생 빈도만 다른 해상도는 하나로 합친다.</summary>
    public static class DisplaySettings
    {
        /// <summary>저장값이 이 배열의 번호라 순서를 바꾸면 기존 설정이 다르게 읽힌다.</summary>
        public static readonly (FullScreenMode mode, string labelKey)[] Modes =
        {
            (FullScreenMode.FullScreenWindow, "options.screenMode.borderless"),
            (FullScreenMode.Windowed, "options.screenMode.windowed"),
            (FullScreenMode.MaximizedWindow, "options.screenMode.maximized")
        };

        /// <summary>
        /// 같은 크기가 재생 빈도별로 여러 번 나오면 목록이 길어지므로 하나로 합치고 작은 것부터 정렬한다.
        /// </summary>
        public static List<Vector2Int> GetResolutions()
        {
            var result = new List<Vector2Int>();
            foreach (Resolution resolution in Screen.resolutions)
            {
                var size = new Vector2Int(resolution.width, resolution.height);
                if (!result.Contains(size))
                    result.Add(size);
            }

            // 목록을 얻지 못하는 환경(일부 창 모드 등)에서는 현재 크기라도 고를 수 있게 한다.
            var currentSize = new Vector2Int(Screen.width, Screen.height);
            if (!result.Contains(currentSize))
                result.Add(currentSize);

            result.Sort((first, second) => first.x != second.x ? first.x.CompareTo(second.x) : first.y.CompareTo(second.y));
            return result;
        }

        /// <summary>
        /// 저장값이 손상되거나 모드 수가 줄었을 때 배열 범위를 벗어나지 않게 한다.
        /// </summary>
        public static int ClampMode(int modeIndex)
        {
            return Mathf.Clamp(modeIndex, 0, Modes.Length - 1);
        }

        /// <summary>에디터에서는 게임 뷰 크기를 바꿀 수 없어 적용하지 않는다.</summary>
        public static void Apply(GameOptions options)
        {
            if (Application.isEditor)
                return;

            int width = options.screenWidth > 0 ? options.screenWidth : Screen.width;
            int height = options.screenHeight > 0 ? options.screenHeight : Screen.height;
            FullScreenMode mode = Modes[ClampMode(options.screenMode)].mode;
            Screen.SetResolution(width, height, mode);
        }
    }
}
