using GuardVsMannequin.Localization;
using GuardVsMannequin.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>바꾸는 즉시 저장되는 FPS·핑 표시 설정.</summary>
    public class StatsDisplaySettingsSection : MonoBehaviour, ISettingsSection
    {
        [SerializeField] private Button previousStatsDisplayButton;
        [SerializeField] private Button nextStatsDisplayButton;
        [SerializeField] private Text statsDisplayValueText;

        /// <summary>
        /// 설정 화면이 처음 열릴 때 한 번만 연결해 이벤트가 중복되지 않게 한다.
        /// </summary>
        public void Initialize()
        {
            previousStatsDisplayButton.onClick.AddListener(() => ChangeStatsDisplay(-1));
            nextStatsDisplayButton.onClick.AddListener(() => ChangeStatsDisplay(1));
        }

        /// <summary>
        /// 저장 파일을 손으로 고쳐 범위 밖 값이 들어와도 목록 밖을 읽지 않도록 보정해 표시한다.
        /// </summary>
        public void Refresh()
        {
            int mode = StatsDisplaySettings.ClampMode(OptionsStore.Current.statsDisplay);
            statsDisplayValueText.text = Loc.Get(StatsDisplaySettings.Modes[mode].labelKey);
        }

        /// <summary>
        /// 처음 설치했을 때와 같은 "FPS·핑 표시"로 되돌린다.
        /// </summary>
        public void ResetToDefaults()
        {
            SetStatsDisplay(0);
        }

        /// <summary>
        /// 버튼 두 개로 목록을 순환하도록 끝에서 처음으로 돌아간다.
        /// </summary>
        private void ChangeStatsDisplay(int direction)
        {
            int modeCount = StatsDisplaySettings.Modes.Length;
            int currentMode = StatsDisplaySettings.ClampMode(OptionsStore.Current.statsDisplay);
            SetStatsDisplay((currentMode + direction + modeCount) % modeCount);
        }

        /// <summary>
        /// 표시 쪽이 설정 변경 알림을 듣고 바로 바꾸므로 저장만 한다.
        /// </summary>
        private void SetStatsDisplay(int mode)
        {
            GameOptions options = OptionsStore.Current;
            options.statsDisplay = mode;
            OptionsStore.SetOptions(options);
            Refresh();
        }
    }
}
