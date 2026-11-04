using GuardVsMannequin.Localization;
using GuardVsMannequin.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 카메라 흔들림 설정(◀ 보통 / 약하게 / 끄기 ▶). 바꾸는 즉시 저장되고 다음 화면부터 적용된다(<see cref="CameraSwaySettings"/>).
    /// "모두 기본값으로"를 누르면 보통으로 돌아간다.
    /// </summary>
    public class CameraSwaySettingsSection : MonoBehaviour, ISettingsSection
    {
        [SerializeField] private Button previousCameraSwayButton;
        [SerializeField] private Button nextCameraSwayButton;
        [SerializeField] private Text cameraSwayValueText;

        /// <summary>
        /// 버튼 이벤트를 연결한다.
        /// </summary>
        public void Initialize()
        {
            previousCameraSwayButton.onClick.AddListener(() => ChangeCameraSway(-1));
            nextCameraSwayButton.onClick.AddListener(() => ChangeCameraSway(1));
        }

        /// <summary>
        /// 현재 카메라 흔들림을 표시한다.
        /// </summary>
        public void Refresh()
        {
            int mode = CameraSwaySettings.ClampMode(OptionsStore.Current.cameraSway);
            cameraSwayValueText.text = Loc.Get(CameraSwaySettings.Modes[mode].labelKey);
        }

        /// <summary>
        /// 보통으로 되돌린다.
        /// </summary>
        public void ResetToDefaults()
        {
            SetCameraSway(0);
        }

        /// <summary>
        /// 이전/다음 흔들림 단계로 바꾼다.
        /// </summary>
        private void ChangeCameraSway(int direction)
        {
            int count = CameraSwaySettings.Modes.Length;
            int current = CameraSwaySettings.ClampMode(OptionsStore.Current.cameraSway);
            SetCameraSway((current + direction + count) % count);
        }

        /// <summary>
        /// 흔들림 단계를 저장하고 표시를 갱신한다.
        /// </summary>
        private void SetCameraSway(int mode)
        {
            GameOptions options = OptionsStore.Current;
            options.cameraSway = mode;
            OptionsStore.SetOptions(options);
            Refresh();
        }
    }
}
