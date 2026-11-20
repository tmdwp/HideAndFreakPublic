namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>공격·소리 코드가 은신 구현을 몰라도 되도록 은신 상태만 연다.</summary>
    public interface IStealthState
    {
        /// <summary>소유자는 입력을 바로 반영한 값, 나머지 피어는 서버가 동기화한 값.</summary>
        bool IsStealthed { get; }

        /// <summary>
        /// 호스트에서는 소유자 값이 먼저 바뀌므로, 서버 판정은 소유자 예측과 섞이지 않도록 서버가 정한 값만 본다.
        /// </summary>
        bool ServerIsStealthed { get; }

        /// <summary>도약 공격이 은신을 소모하는 규칙을 은신 쪽이 정하도록, 소유자 화면에서 도약했다고 알린다.</summary>
        void OwnerConsumeForLunge();

        /// <summary>서버가 도약을 인정한 뒤 모든 피어에 은신 소모를 반영하도록 알린다.</summary>
        void ServerConsumeForLunge();
    }
}
