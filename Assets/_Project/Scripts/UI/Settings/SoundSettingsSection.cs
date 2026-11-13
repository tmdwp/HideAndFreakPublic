using GuardVsMannequin.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>바꾸는 즉시 적용·저장하는 음량 설정.</summary>
    public class SoundSettingsSection : MonoBehaviour, ISettingsSection
    {
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Text masterVolumeValueText;
        [SerializeField] private Slider bgmVolumeSlider;
        [SerializeField] private Text bgmVolumeValueText;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Text sfxVolumeValueText;

        /// <summary>
        /// 저장값이 0~1 비율이라 씬에 어떤 범위가 들어 있든 슬라이더를 같은 범위로 맞춘다.
        /// </summary>
        public void Initialize()
        {
            foreach (Slider volumeSlider in new[] { masterVolumeSlider, bgmVolumeSlider, sfxVolumeSlider })
            {
                volumeSlider.minValue = 0f;
                volumeSlider.maxValue = 1f;
                volumeSlider.onValueChanged.AddListener(_ => Save());
            }
        }

        /// <summary>
        /// 표시만 바꾸는데 저장이 다시 일어나지 않도록 알림 없이 슬라이더 값을 바꾼다.
        /// </summary>
        public void Refresh()
        {
            GameOptions options = OptionsStore.Current;
            masterVolumeSlider.SetValueWithoutNotify(options.masterVolume);
            bgmVolumeSlider.SetValueWithoutNotify(options.bgmVolume);
            sfxVolumeSlider.SetValueWithoutNotify(options.sfxVolume);
            RefreshTexts(options);
        }

        /// <summary>
        /// 다른 묶음의 설정을 건드리지 않도록 음량 항목만 기본값으로 바꾼다.
        /// </summary>
        public void ResetToDefaults()
        {
            GameOptions defaults = GameOptions.CreateDefault();
            GameOptions options = OptionsStore.Current;
            options.masterVolume = defaults.masterVolume;
            options.bgmVolume = defaults.bgmVolume;
            options.sfxVolume = defaults.sfxVolume;
            OptionsStore.SetOptions(options);
            Refresh();
        }

        /// <summary>움직이는 즉시 소리로 확인하도록 바로 저장·적용하고 보정된 값을 보여 준다.</summary>
        private void Save()
        {
            GameOptions options = OptionsStore.Current;
            options.masterVolume = masterVolumeSlider.value;
            options.bgmVolume = bgmVolumeSlider.value;
            options.sfxVolume = sfxVolumeSlider.value;
            OptionsStore.SetOptions(options);
            RefreshTexts(OptionsStore.Current);
        }

        /// <summary>
        /// 0~1 비율은 사람이 읽기 어려우므로 퍼센트로 보여 준다.
        /// </summary>
        private void RefreshTexts(GameOptions options)
        {
            masterVolumeValueText.text = Mathf.RoundToInt(options.masterVolume * 100f) + "%";
            bgmVolumeValueText.text = Mathf.RoundToInt(options.bgmVolume * 100f) + "%";
            sfxVolumeValueText.text = Mathf.RoundToInt(options.sfxVolume * 100f) + "%";
        }
    }
}
