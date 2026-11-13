using GuardVsMannequin.Localization;
using GuardVsMannequin.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>바꾸는 즉시 적용·저장하는 프레임 제한 설정.</summary>
    public class FrameLimitSettingsSection : MonoBehaviour, ISettingsSection
    {
        [SerializeField] private Button previousFrameLimitButton;
        [SerializeField] private Button nextFrameLimitButton;
        [SerializeField] private Text frameLimitValueText;

        /// <summary>
        /// 설정 화면이 처음 열릴 때 한 번만 연결해 이벤트가 중복되지 않게 한다.
        /// </summary>
        public void Initialize()
        {
            previousFrameLimitButton.onClick.AddListener(() => ChangeFrameLimit(-1));
            nextFrameLimitButton.onClick.AddListener(() => ChangeFrameLimit(1));
        }

        /// <summary>
        /// 저장 파일을 손으로 고쳐 범위 밖 값이 들어와도 목록 밖을 읽지 않도록 보정해 표시한다.
        /// </summary>
        public void Refresh()
        {
            int frameLimitMode = FrameRateSettings.ClampMode(OptionsStore.Current.frameLimit);
            frameLimitValueText.text = Loc.Get(FrameRateSettings.Modes[frameLimitMode].labelKey);
        }

        /// <summary>
        /// 화면 찢어짐이 없는 가장 무난한 값이 모니터 주사율(수직 동기화)이라 그 값으로 되돌린다.
        /// </summary>
        public void ResetToDefaults()
        {
            SetFrameLimit(0);
        }

        /// <summary>
        /// 버튼 두 개로 목록을 순환하도록 끝에서 처음으로 돌아간다.
        /// </summary>
        private void ChangeFrameLimit(int direction)
        {
            int modeCount = FrameRateSettings.Modes.Length;
            int currentMode = FrameRateSettings.ClampMode(OptionsStore.Current.frameLimit);
            SetFrameLimit((currentMode + direction + modeCount) % modeCount);
        }

        /// <summary>
        /// 바꾼 결과를 바로 확인할 수 있게 저장과 동시에 적용하고 표시한다.
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
