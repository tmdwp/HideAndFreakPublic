namespace GuardVsMannequin.Player
{
    /// <summary>MoveSpeedController가 역할 속도에 곱하도록 이동 속도 배율을 제공한다.</summary>
    public interface IMoveSpeedModifier
    {
        /// <summary>여러 효과를 곱해서 합치므로, 효과가 없을 때는 1이어야 한다.</summary>
        float SpeedMultiplier { get; }
    }
}
