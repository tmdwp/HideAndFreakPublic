using GuardVsMannequin.Localization;
using GuardVsMannequin.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>바꾸는 즉시 저장되는 카메라 흔들림 설정.</summary>
    public class CameraSwaySettingsSection : MonoBehaviour, ISettingsSection
    {
        [SerializeField] private Button previousCameraSwayButton;
        [SerializeField] private Button nextCameraSwayButton;
        [SerializeField] private Text cameraSwayValueText;

        /// <summary>
        /// 설정 화면이 처음 열릴 때 한 번만 연결해 이벤트가 중복되지 않게 한다.
        /// </summary>
        public void Initialize()
        {
            previousCameraSwayButton.onClick.AddListener(() => ChangeCameraSway(-1));
            nextCameraSwayButton.onClick.AddListener(() => ChangeCameraSway(1));
        }

        /// <summary>
        /// 저장 파일을 손으로 고쳐 범위 밖 값이 들어와도 목록 밖을 읽지 않도록 보정해 표시한다.
        /// </summary>
        public void Refresh()
        {
            int swayMode = CameraSwaySettings.ClampMode(OptionsStore.Current.cameraSway);
            cameraSwayValueText.text = Loc.Get(CameraSwaySettings.Modes[swayMode].labelKey);
        }

        /// <summary>
        /// 의도한 연출이 보이는 값이 "보통"이라 그 값으로 되돌린다.
        /// </summary>
        public void ResetToDefaults()
        {
            SetCameraSway(0);
        }

        /// <summary>
        /// 버튼 두 개로 목록을 순환하도록 끝에서 처음으로 돌아간다.
        /// </summary>
        private void ChangeCameraSway(int direction)
        {
            int modeCount = CameraSwaySettings.Modes.Length;
            int currentMode = CameraSwaySettings.ClampMode(OptionsStore.Current.cameraSway);
            SetCameraSway((currentMode + direction + modeCount) % modeCount);
        }

        /// <summary>
        /// 흔들림은 카메라가 매 프레임 설정을 읽으므로 저장만 하면 다음 화면부터 적용된다.
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
