namespace GuardVsMannequin.Core
{
    /// <summary>HUD가 0에서 1로 차오르는 세로 바로 쿨타임을 보여 주도록 하는 도구.</summary>
    public interface ICooldownGauge : ITool
    {
        /// <summary>도구마다 쿨타임 길이가 달라도 HUD가 같은 게이지로 그리도록 0~1로 정규화한다.</summary>
        float CooldownProgress { get; }
    }
}
