namespace GuardVsMannequin.Settings
{
    /// <summary>나중에 AudioMixer로 바꾸기 쉽도록 소리 종류별 음량 배율 조회를 한곳에 모은다.</summary>
    public static class AudioVolumes
    {
        /// <summary>소음·총소리·전원 차단 등 효과음 전체에 곱한다.</summary>
        public static float Sfx => OptionsStore.Current.sfxVolume;

        public static float Bgm => OptionsStore.Current.bgmVolume;
    }
}
