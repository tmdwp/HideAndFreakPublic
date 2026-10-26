using FishNet.Object;
using FishNet.Object.Synchronizing;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>
    /// 마네킹의 좌클릭 유지 상호작용(전원 장치 파괴 등).
    /// 누르면 조준한 대상과 상호작용을 시작하고, 떼면 끝낸다. 판정은 서버가 한다.
    /// 상호작용 중인지는 모든 클라이언트에 동기화한다. (상호작용 애니메이션 표시용, <see cref="IInteractionState"/>)
    /// </summary>
    [RequireComponent(typeof(MannequinTransformer), typeof(PlayerHealth))]
    public class MannequinInteractor : NetworkBehaviour, IInteractor, IInteractionState
    {
        private readonly SyncVar<bool> isInteracting = new();

        private MannequinTransformer transformer;
        private PlayerHealth health;
        private IAimProvider aimProvider;
        private IPlayerInput playerInput;
        private IInteractable currentTarget;

        public int ClientId => Owner.ClientId;
        public Vector3 Position => transform.position;

        /// <summary>상호작용 중인지 여부. (서버가 정하고 모든 클라이언트에 동기화)</summary>
        public bool IsInteracting => isInteracting.Value;

        /// <summary>
        /// 살아 있고, 변신하지 않았고, 탐색 단계일 때만 상호작용할 수 있다.
        /// </summary>
        public bool CanInteract => this != null && health.IsAlive && !transformer.IsDisguised
                                   && GameServices.CurrentStateOrWaiting == GameState.Playing;

        /// <summary>
        /// 같은 오브젝트의 구성 요소를 캐시한다.
        /// </summary>
        private void Awake()
        {
            transformer = GetComponent<MannequinTransformer>();
            health = GetComponent<PlayerHealth>();
            aimProvider = GetComponent<IAimProvider>();
            playerInput = GetComponent<IPlayerInput>();
        }

        /// <summary>
        /// 소유자만 좌클릭 입력을 구독한다.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner || playerInput == null)
                return;

            playerInput.PrimaryPressed += OnPrimaryPressed;
            playerInput.PrimaryReleased += OnPrimaryReleased;
        }

        /// <summary>
        /// 입력 구독을 해제한다.
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!IsOwner || playerInput == null)
                return;

            playerInput.PrimaryPressed -= OnPrimaryPressed;
            playerInput.PrimaryReleased -= OnPrimaryReleased;
        }

        /// <summary>
        /// 제거될 때 진행 중인 상호작용을 끝낸다.
        /// </summary>
        public override void OnStopServer()
        {
            base.OnStopServer();
            EndCurrentInteraction();
        }

        /// <summary>
        /// 서버에서 대상이 참여자를 뺐는지(거리·변신·파괴 등) 확인하고 상호작용 상태를 동기화한다.
        /// </summary>
        private void Update()
        {
            if (!this.IsServerReady())
                return;

            if (currentTarget != null && !currentTarget.IsInteracting(this))
                currentTarget = null;

            bool interacting = currentTarget != null;
            if (isInteracting.Value != interacting)
                isInteracting.Value = interacting;
        }

        /// <summary>
        /// 조준한 네트워크 오브젝트에 상호작용 시작을 요청한다.
        /// </summary>
        private void OnPrimaryPressed()
        {
            // 전원 장치 파괴는 탐색 단계에서만 가능하다. (사냥 시간의 좌클릭은 MannequinAttackTool이 처리)
            if (transformer.IsDisguised || GameServices.CurrentStateOrWaiting != GameState.Playing)
                return;

            Ray aim = aimProvider.GetAimRay();
            float cameraDistance = Vector3.Distance(aim.origin, transform.position);
            if (!AimRaycaster.TryRaycast(aim, GameTuning.InteractRange + cameraDistance, transform, out RaycastHit hit))
                return;

            NetworkObject target = hit.collider.GetComponentInParent<NetworkObject>();
            if (target != null && target.GetComponent<IInteractable>() != null)
                BeginInteractServerRpc(target);
        }

        /// <summary>
        /// 좌클릭을 떼면 상호작용 종료를 요청한다.
        /// </summary>
        private void OnPrimaryReleased()
        {
            EndInteractServerRpc();
        }

        /// <summary>
        /// 서버에서 대상의 상호작용 조건을 검증하고 시작한다.
        /// </summary>
        [ServerRpc]
        private void BeginInteractServerRpc(NetworkObject target)
        {
            EndCurrentInteraction();

            if (target == null || !CanInteract)
                return;

            IInteractable interactable = target.GetComponent<IInteractable>();
            if (interactable != null && interactable.TryBeginInteract(this))
                currentTarget = interactable;
        }

        /// <summary>
        /// 서버에서 상호작용을 끝낸다.
        /// </summary>
        [ServerRpc]
        private void EndInteractServerRpc()
        {
            EndCurrentInteraction();
        }

        /// <summary>
        /// 진행 중인 상호작용이 있으면 끝낸다.
        /// </summary>
        private void EndCurrentInteraction()
        {
            currentTarget?.EndInteract(this);
            currentTarget = null;
        }
    }
}
