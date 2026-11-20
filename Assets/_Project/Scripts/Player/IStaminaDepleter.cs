namespace GuardVsMannequin.Player
{
    /// <summary>도약처럼 스태미나를 한 번에 모두 쓰는 동작이 스태미나 구현을 몰라도 되도록 바닥내기만 연다.</summary>
    public interface IStaminaDepleter
    {
        /// <summary>스태미나를 0으로 만들고 소진 상태로 둔다.</summary>
        void DepleteStamina();
    }
}
