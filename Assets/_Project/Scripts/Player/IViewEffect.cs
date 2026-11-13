namespace GuardVsMannequin.Player
{
    /// <summary>관전자 화면에도 똑같이 적용할 수 있도록 특정 플레이어 시야 효과를 인터페이스로 둔다.</summary>
    public interface IViewEffect
    {
        /// <summary>본인과 관전자 화면에 똑같이 걸 수 있도록 켜는 시점을 밖에서 정한다.</summary>
        void Activate();

        /// <summary>관전 대상이 바뀌면 이전 대상의 효과가 남지 않도록 끈다.</summary>
        void Deactivate();
    }
}
