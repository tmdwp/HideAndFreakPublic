namespace GuardVsMannequin.Player
{
    /// <summary>달리기 말고도 같은 스태미나를 쓰는 동작이 있어, 스태미나를 가진 쪽이 쓰는 속도를 물어볼 수 있게 한다.</summary>
    public interface IStaminaDrain
    {
        /// <summary>지금 초당 쓰려는 스태미나 비율(0~1). 쓰지 않으면 0.</summary>
        float StaminaDrainPerSecond { get; }
    }
}
