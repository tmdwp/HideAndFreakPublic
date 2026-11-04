namespace GuardVsMannequin.Core
{
    /// <summary>
    /// 켜고 끄는 도구(손전등 등). HUD는 켜짐 여부와 <see cref="ITool.IsReady"/>로 아이콘 색을 정한다.
    /// </summary>
    public interface ISwitchableTool : ITool
    {
        /// <summary>지금 켜져 있는지 여부.</summary>
        bool IsOn { get; }
    }
}
