using FishNet.Object;
using FishNet.Object.Synchronizing;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>판정은 서버가 하는 마네킹의 좌클릭 유지 상호작용.</summary>
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

        /// <summary>다른 화면에서도 상호작용 동작이 보이도록 서버가 정해 모두에게 동기화한다.</summary>
        public bool IsInteracting => isInteracting.Value;

        /// <summary>
        /// 변신한 채 전원 장치를 부수거나 사냥 시간에 좌클릭이 공격과 겹치지 않도록 살아 있고 변신하지 않은 탐색 단계에서만 허용하고,
        /// 스폰 근처 장치를 시작하자마자 부수지 못하도록 시작 구간에는 막는다.
        /// </summary>
        public bool CanInteract => this != null && health.IsAlive && !transformer.IsDisguised && IsInteractPhase;

        /// <summary>입력 쪽과 서버 검증이 같은 단계 조건을 쓰도록 모은다.</summary>
        private static bool IsInteractPhase => GameServices.CurrentStateOrWaiting == GameState.Playing
                                               && !MatchOpeningRules.IsActive(GameServices.State);

        /// <summary>
        /// 매 프레임 GetComponent를 부르지 않도록 미리 찾아 둔다.
        /// </summary>
        private void Awake()
        {
            transformer = GetComponent<MannequinTransformer>();
            health = GetComponent<PlayerHealth>();
            aimProvider = GetComponent<IAimProvider>();
            playerInput = GetComponent<IPlayerInput>();
        }

        /// <summary>
        /// 남의 아바타가 내 클릭에 반응하지 않도록 소유자만 좌클릭 입력을 듣는다.
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
        /// 아바타가 사라진 뒤 입력 이벤트가 이 객체를 부르지 않도록 구독을 끊는다.
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
        /// 죽거나 나간 마네킹이 계속 참여자로 남아 진행을 올리지 않도록, 제거될 때 상호작용을 끝낸다.
        /// </summary>
        public override void OnStopServer()
        {
            base.OnStopServer();
            EndCurrentInteraction();
        }

        /// <summary>
        /// 대상이 거리·변신·파괴 등으로 참여자를 스스로 뺄 수 있어서, 서버가 매 프레임 확인해 동기화 값을 맞춘다.
        /// </summary>
        private void Update()
        {
            if (!this.IsServerReady())
                return;

            if (currentTarget != null && !currentTarget.IsInteracting(this))
                currentTarget = null;

            bool hasTarget = currentTarget != null;
            if (isInteracting.Value != hasTarget)
                isInteracting.Value = hasTarget;
        }

        /// <summary>3인칭 카메라가 몸 뒤에 있어 그 거리만큼 광선을 늘려 대상을 찾는다.</summary>
        private void OnPrimaryPressed()
        {
            if (transformer.IsDisguised || !IsInteractPhase)
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
        /// 누르고 있어야 진행되는 동작이라 떼는 순간 서버에 알린다.
        /// </summary>
        private void OnPrimaryReleased()
        {
            EndInteractServerRpc();
        }

        /// <summary>
        /// 한 번에 한 대상과만 상호작용하도록 이전 것을 먼저 끝내고, 클라이언트 조건은 믿지 않고 서버에서 다시 확인한다.
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
        /// 종료는 서버가 대상에게 알려야 진행이 멈추므로 RPC로 보낸다.
        /// </summary>
        [ServerRpc]
        private void EndInteractServerRpc()
        {
            EndCurrentInteraction();
        }

        /// <summary>
        /// 대상 쪽 참여자 목록에서도 빠지도록 대상에게 끝났다고 알린다.
        /// </summary>
        private void EndCurrentInteraction()
        {
            currentTarget?.EndInteract(this);
            currentTarget = null;
        }
    }
}
