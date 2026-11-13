namespace GuardVsMannequin.Player
{
    /// <summary>행동 컴포넌트가 잠금 원인을 몰라도 되도록 잠시 행동할 수 없는 상태를 알린다.</summary>
    public interface IActionLock
    {
        bool IsActionLocked { get; }
    }
}
