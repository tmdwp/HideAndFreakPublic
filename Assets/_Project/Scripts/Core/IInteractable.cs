namespace GuardVsMannequin.Core
{
    /// <summary>서버에서만 호출되는 좌클릭 유지 상호작용 대상.</summary>
    public interface IInteractable
    {
        /// <summary>대상마다 크기가 달라 닿는 거리를 대상이 정한다.</summary>
        float InteractRange { get; }

        /// <summary>
        /// 이미 다른 사람이 쓰는 중이거나 조건이 안 맞을 수 있으므로, 시작 성공 여부를 돌려준다.
        /// </summary>
        bool TryBeginInteract(IInteractor interactor);

        /// <summary>
        /// 손을 뗀 사람이 진행에 계속 반영되지 않도록 참여를 끝낸다.
        /// </summary>
        void EndInteract(IInteractor interactor);

        /// <summary>
        /// 대상이 거리·조건 등으로 참여자를 스스로 뺄 수 있어서, 주체가 아직 참여 중인지 대상에게 묻는다.
        /// </summary>
        bool IsInteracting(IInteractor interactor);
    }

    /// <summary>애니메이션 표시용으로 모든 클라이언트에 동기화된 상호작용 중 여부.</summary>
    public interface IInteractionState
    {
        bool IsInteracting { get; }
    }

    /// <summary>상호작용을 하는 주체.</summary>
    public interface IInteractor
    {
        int ClientId { get; }

        /// <summary>대상이 거리 판정을 직접 하도록 위치를 노출한다.</summary>
        UnityEngine.Vector3 Position { get; }

        /// <summary>사망·변신 등으로 도중에 불가능해질 수 있어서, 대상이 매번 다시 확인한다.</summary>
        bool CanInteract { get; }
    }
}
