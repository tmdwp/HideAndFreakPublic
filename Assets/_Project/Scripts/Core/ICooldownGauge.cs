namespace GuardVsMannequin.Core
{
    /// <summary>
    /// 쿨타임을 게이지(세로 바)로 보여주는 도구(수류탄, 마네킹 공격·변신 등).
    /// HUD는 사용 직후 0에서 다시 쓸 수 있을 때 1까지 차오르는 진행도만 본다.
    /// </summary>
    public interface ICooldownGauge : ITool
    {
        /// <summary>쿨타임 진행도. 사용 직후 0, 쿨타임이 끝나면 1.</summary>
        float CooldownProgress { get; }
    }
}
