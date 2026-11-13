namespace GuardVsMannequin.Core
{
    /// <summary>HUD가 켜짐 여부로 아이콘 색을 정하도록 하는 켜고 끄는 도구.</summary>
    public interface ISwitchableTool : ITool
    {
        bool IsOn { get; }
    }
}
