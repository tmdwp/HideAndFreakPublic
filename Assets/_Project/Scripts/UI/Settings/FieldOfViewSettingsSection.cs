using GuardVsMannequin.Settings;

namespace GuardVsMannequin.UI
{
    /// <summary>바꾸는 즉시 카메라에 반영되는 시야각 설정.</summary>
    public class FieldOfViewSettingsSection : OptionSelectorSection
    {
        private static readonly int[] order = Sequential(FieldOfViewSettings.Modes.Length);

        protected override int[] Order => order;

        protected override int GetMode(GameOptions options) => FieldOfViewSettings.ClampMode(options.fieldOfView);

        protected override void SetMode(ref GameOptions options, int mode) => options.fieldOfView = mode;

        protected override string GetLabel(int mode) => FieldOfViewSettings.GetLabel(mode);
    }
}
