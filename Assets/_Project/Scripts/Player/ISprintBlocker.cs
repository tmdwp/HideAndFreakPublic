namespace GuardVsMannequin.Player
{
    /// <summary>같은 키(Shift)를 다른 동작에 쓰는 동안 달리기가 함께 켜지지 않도록 막는다.</summary>
    public interface ISprintBlocker
    {
        bool IsSprintBlocked { get; }
    }
}
