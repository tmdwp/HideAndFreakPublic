namespace GuardVsMannequin.Player
{
    /// <summary>HUD가 달리기 구현에 의존하지 않도록 스태미나 상태를 인터페이스로 둔다.</summary>
    public interface IStaminaSource
    {
        /// <summary>0~1 비율.</summary>
        float Stamina { get; }

        /// <summary>남은 양이 있어도 다시 달릴 수 없는 구간이 있어서, HUD가 색으로 구분할 수 있게 따로 알린다.</summary>
        bool IsExhausted { get; }
    }
}
