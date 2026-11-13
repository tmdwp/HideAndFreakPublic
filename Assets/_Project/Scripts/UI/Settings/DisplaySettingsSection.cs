using System.Collections.Generic;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>화면 모드와 해상도를 바꾸는 즉시 적용·저장하는 화면 설정.</summary>
    public class DisplaySettingsSection : MonoBehaviour, ISettingsSection
    {
        [SerializeField] private Button previousScreenModeButton;
        [SerializeField] private Button nextScreenModeButton;
        [SerializeField] private Text screenModeValueText;

        [Tooltip("해상도 목록. 누르면 스크롤 목록이 펼쳐진다.")]
        [SerializeField] private Dropdown resolutionDropdown;

        private readonly List<Vector2Int> resolutions = new();

        /// <summary>
        /// 설정 화면이 처음 열릴 때 한 번만 연결해 이벤트가 중복되지 않게 한다.
        /// </summary>
        public void Initialize()
        {
            previousScreenModeButton.onClick.AddListener(() => ChangeScreenMode(-1));
            nextScreenModeButton.onClick.AddListener(() => ChangeScreenMode(1));
            resolutionDropdown.onValueChanged.AddListener(OnResolutionSelected);
        }

        /// <summary>모니터를 바꿨을 수 있어 열 때마다 지원 해상도 목록을 다시 만든다.</summary>
        public void Refresh()
        {
            GameOptions options = OptionsStore.Current;
            screenModeValueText.text = Loc.Get(DisplaySettings.Modes[DisplaySettings.ClampMode(options.screenMode)].labelKey);

            resolutions.Clear();
            resolutions.AddRange(DisplaySettings.GetResolutions());
            var resolutionLabels = new List<string>();
            foreach (Vector2Int size in resolutions)
                resolutionLabels.Add($"{size.x} × {size.y}");

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(resolutionLabels);

            int selectedIndex = resolutions.IndexOf(GetSelectedResolution(options));
            resolutionDropdown.SetValueWithoutNotify(selectedIndex >= 0 ? selectedIndex : resolutions.Count - 1);
            resolutionDropdown.RefreshShownValue();
        }

        /// <summary>
        /// 화면 설정을 되돌리면 화면이 깜빡이고 모니터에 맞춘 값을 잃으므로 비워 둔다.
        /// </summary>
        public void ResetToDefaults()
        {
        }

        /// <summary>버튼 두 개로 순환하도록 끝에서 처음으로 돌아가고, 모드만 바꿔도 크기가 바뀌지 않게 해상도를 함께 저장한다.</summary>
        private void ChangeScreenMode(int direction)
        {
            GameOptions options = OptionsStore.Current;
            int modeCount = DisplaySettings.Modes.Length;
            options.screenMode = (DisplaySettings.ClampMode(options.screenMode) + direction + modeCount) % modeCount;
            KeepCurrentResolution(ref options);
            OptionsStore.SetOptions(options);
            Refresh();
        }

        /// <summary>
        /// 목록이 다시 만들어지는 도중 범위를 벗어난 값이 올 수 있어 확인한 뒤 적용한다.
        /// </summary>
        private void OnResolutionSelected(int index)
        {
            if (index < 0 || index >= resolutions.Count)
                return;

            GameOptions options = OptionsStore.Current;
            options.screenWidth = resolutions[index].x;
            options.screenHeight = resolutions[index].y;
            OptionsStore.SetOptions(options);
        }

        /// <summary>
        /// 해상도를 고른 적이 없으면 저장값이 0이라, 그때는 지금 화면 크기를 선택된 것으로 본다.
        /// </summary>
        private static Vector2Int GetSelectedResolution(GameOptions options)
        {
            return options.screenWidth > 0 && options.screenHeight > 0
                ? new Vector2Int(options.screenWidth, options.screenHeight)
                : new Vector2Int(Screen.width, Screen.height);
        }

        /// <summary>
        /// 해상도를 고른 적이 없으면 지금 크기를 저장해, 모드만 바꿔도 크기가 유지되게 한다.
        /// </summary>
        private static void KeepCurrentResolution(ref GameOptions options)
        {
            if (options.screenWidth > 0 && options.screenHeight > 0)
                return;
            options.screenWidth = Screen.width;
            options.screenHeight = Screen.height;
        }
    }
}
