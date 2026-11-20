using GuardVsMannequin.Settings;

namespace GuardVsMannequin.UI
{
    /// <summary>익숙해진 사람이 화면을 넓게 쓰도록 HUD 조작 안내를 켜고 끄는 설정. 0번(기본)이 표시다.</summary>
    public class ControlsHintSettingsSection : OptionSelectorSection
    {
        private static readonly int[] order = { 0, 1 };

        protected override int[] Order => order;

        protected override int GetMode(GameOptions options) => options.hideControlsHint ? 1 : 0;

        protected override void SetMode(ref GameOptions options, int mode) => options.hideControlsHint = mode == 1;

        protected override string GetLabel(int mode) => OnOffLabels.Get(mode == 0);
    }
}
