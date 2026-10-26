namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 아바타가 잠시 행동(변신·해제 등)을 할 수 없는 상태인지 알려준다. (변신한 마네킹이 날아가는 동안)
    /// 행동 컴포넌트는 잠금의 원인을 몰라도 이 인터페이스만 확인한다.
    /// </summary>
    public interface IActionLock
    {
        /// <summary>지금 행동이 잠겨 있는지.</summary>
        bool IsActionLocked { get; }
    }
}
