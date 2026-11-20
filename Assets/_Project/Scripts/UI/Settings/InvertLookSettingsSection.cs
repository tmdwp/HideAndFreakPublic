using GuardVsMannequin.Settings;

namespace GuardVsMannequin.UI
{
    /// <summary>비행 조종처럼 위아래를 반대로 쓰는 사람을 위한 Y축 반전 설정. 0번(기본)이 끔이다.</summary>
    public class InvertLookSettingsSection : OptionSelectorSection
    {
        private static readonly int[] order = { 0, 1 };

        protected override int[] Order => order;

        protected override int GetMode(GameOptions options) => options.invertLookY ? 1 : 0;

        protected override void SetMode(ref GameOptions options, int mode) => options.invertLookY = mode == 1;

        protected override string GetLabel(int mode) => OnOffLabels.Get(mode == 1);
    }
}
