using GuardVsMannequin.Localization;
using GuardVsMannequin.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 프레임 제한 설정(◀ 모니터 주사율 / 60 / 120 / 144 / 제한 없음 ▶). 바꾸는 즉시 적용·저장된다(<see cref="FrameRateSettings"/>).
    /// "모두 기본값으로"를 누르면 모니터 주사율(수직 동기화)로 돌아간다.
    /// </summary>
    public class FrameLimitSettingsSection : MonoBehaviour, ISettingsSection
    {
        [SerializeField] private Button previousFrameLimitButton;
        [SerializeField] private Button nextFrameLimitButton;
        [SerializeField] private Text frameLimitValueText;

        /// <summary>
        /// 버튼 이벤트를 연결한다.
        /// </summary>
        public void Initialize()
        {
            previousFrameLimitButton.onClick.AddListener(() => ChangeFrameLimit(-1));
            nextFrameLimitButton.onClick.AddListener(() => ChangeFrameLimit(1));
        }

        /// <summary>
        /// 현재 프레임 제한을 표시한다.
        /// </summary>
        public void Refresh()
        {
            int mode = FrameRateSettings.ClampMode(OptionsStore.Current.frameLimit);
            frameLimitValueText.text = Loc.Get(FrameRateSettings.Modes[mode].labelKey);
        }

        /// <summary>
        /// 모니터 주사율(수직 동기화)로 되돌린다.
        /// </summary>
        public void ResetToDefaults()
        {
            SetFrameLimit(0);
        }

        /// <summary>
        /// 이전/다음 프레임 제한으로 바꾼다.
        /// </summary>
        private void ChangeFrameLimit(int direction)
        {
            int count = FrameRateSettings.Modes.Length;
            int current = FrameRateSettings.ClampMode(OptionsStore.Current.frameLimit);
            SetFrameLimit((current + direction + count) % count);
        }

        /// <summary>
        /// 프레임 제한을 저장·적용하고 표시를 갱신한다.
        /// </summary>
        private void SetFrameLimit(int mode)
        {
            GameOptions options = OptionsStore.Current;
            options.frameLimit = mode;
            OptionsStore.SetOptions(options);
            Refresh();
        }
    }
}
