namespace GuardVsMannequin.Core
{
    /// <summary>HUD가 이 인터페이스만 보고 쿨타임을 표시하도록 하는 쿨타임 있는 도구.</summary>
    public interface ITool
    {
        /// <summary>HUD가 도구 종류를 몰라도 이름을 띄울 수 있도록 도구가 직접 제공한다.</summary>
        string DisplayName { get; }

        bool IsReady { get; }

        /// <summary>단위는 초.</summary>
        float CooldownRemaining { get; }
    }
}
