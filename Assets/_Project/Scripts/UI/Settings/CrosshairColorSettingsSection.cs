using GuardVsMannequin.Localization;
using GuardVsMannequin.Settings;

namespace GuardVsMannequin.UI
{
    /// <summary>바꾸는 즉시 HUD에 반영되는 조준점 색 설정.</summary>
    public class CrosshairColorSettingsSection : OptionSelectorSection
    {
        private static readonly int[] order = Sequential(CrosshairSettings.Modes.Length);

        protected override int[] Order => order;

        protected override int GetMode(GameOptions options) => CrosshairSettings.ClampMode(options.crosshairColor);

        protected override void SetMode(ref GameOptions options, int mode) => options.crosshairColor = mode;

        protected override string GetLabel(int mode) => Loc.Get(CrosshairSettings.Modes[CrosshairSettings.ClampMode(mode)].labelKey);
    }
}
