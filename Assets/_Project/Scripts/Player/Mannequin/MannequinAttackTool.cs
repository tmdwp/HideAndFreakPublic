using System.Collections;
using FishNet.Object;
using GuardVsMannequin.Audio;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>
    /// 사냥 시간 마네킹의 근접 공격. 좌클릭 시 공격 동작을 시작하고, 외형의 맞는 순간에 서버가 공격 범위(호스트 설정) 안의 가드를 판정한다.
    /// 탐색 단계의 좌클릭(전원 장치 파괴)과는 게임 상태로 구분된다. 변신 중에는 공격할 수 없다.
    /// </summary>
    [RequireComponent(typeof(NoiseEmitter), typeof(MannequinTransformer))]
    public class MannequinAttackTool : NetworkBehaviour, ITool
    {
        /// <summary>비정상 원점을 대체할 눈 높이(m).</summary>
        private const float EyeHeight = 1.5f;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        private readonly ToolCooldown cooldown = new();

        private IAimProvider aimProvider;
        private IPlayerInput playerInput;
        private NoiseEmitter noiseEmitter;
        private MannequinTransformer transformer;
        private CharacterSkinView skinView;
        private PlayerViewSync viewSync;
        private PlayerHealth health;

        public string DisplayName => Loc.Get("tool.attack");
        public float CooldownRemaining => cooldown.LocalRemaining;
        public bool IsReady => CooldownRemaining <= 0f && CanAttackNow;

        /// <summary>사냥 시간이고 변신하지 않은 상태인지 여부.</summary>
        private bool CanAttackNow => GameServices.CurrentStateOrWaiting == GameState.Hunting && !transformer.IsDisguised;

        /// <summary>호스트 설정의 공격 범위.</summary>
        private static float AttackRange => GameServices.CurrentSettingsOrDefault.mannequinAttackRange;

        /// <summary>공격 한 번의 피해량. (마네킹 능력치, 최소 1)</summary>
        private static int Damage => Mathf.Max(1, GameServices.CurrentSettingsOrDefault.GetStats(PlayerRole.Mannequin).AttackDamage);

        /// <summary>
        /// 구성 요소를 캐시한다.
        /// </summary>
        private void Awake()
        {
            aimProvider = GetComponent<IAimProvider>();
            playerInput = GetComponent<IPlayerInput>();
            noiseEmitter = GetComponent<NoiseEmitter>();
            transformer = GetComponent<MannequinTransformer>();
            skinView = GetComponent<CharacterSkinView>();
            viewSync = GetComponent<PlayerViewSync>();
            health = GetComponent<PlayerHealth>();
        }

        /// <summary>
        /// 소유자만 좌클릭 입력을 구독한다.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner && playerInput != null)
                playerInput.PrimaryPressed += OnPrimaryPressed;
        }

        /// <summary>
        /// 입력 구독을 해제한다.
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsOwner && playerInput != null)
                playerInput.PrimaryPressed -= OnPrimaryPressed;
        }

        /// <summary>
        /// 사냥 시간이고 쿨타임이 끝났으면 조준 방향으로 공격을 요청한다.
        /// </summary>
        private void OnPrimaryPressed()
        {
            if (!IsReady)
                return;

            Ray aim = aimProvider.GetAimRay();
            cooldown.StartLocal(GameTuning.MannequinAttackCooldown);
            AttackServerRpc(aim.origin, aim.direction);
        }

        /// <summary>
        /// 서버에서 공격을 검증하고 공격 동작을 시작한다. 피해 판정은 외형의 맞는 순간(<see cref="CharacterSkinView.AttackHitDelay"/>)에 한다.
        /// </summary>
        [ServerRpc]
        private void AttackServerRpc(Vector3 origin, Vector3 direction)
        {
            if (!AimRaycaster.IsValidAim(origin, direction))
                return;
            if (!CanAttackNow)
                return;
            if (!cooldown.TryStartServer(GameTuning.MannequinAttackCooldown))
                return;

            origin = AimRaycaster.SanitizeOrigin(origin, direction, transform, EyeHeight);
            AttackObserversRpc();

            float hitDelay = skinView != null ? skinView.AttackHitDelay : 0f;
            if (hitDelay <= 0f)
                ApplyHit(origin, direction);
            else
                StartCoroutine(HitAfterDelay(origin, direction, hitDelay));
        }

        /// <summary>
        /// 맞는 순간까지 기다린 뒤 판정한다. 그 사이 움직인 만큼 원점을 옮기고, 시선은 동기화된 현재 방향을 쓴다.
        /// (휘두르는 동안 대상을 따라 돌아보면 맞도록)
        /// </summary>
        private IEnumerator HitAfterDelay(Vector3 origin, Vector3 direction, float delay)
        {
            Vector3 startPosition = transform.position;
            yield return new WaitForSeconds(delay);

            // 사냥 시간이 끝났거나 변신했거나 죽었으면 휘두르기가 끊긴 것으로 본다.
            if (!IsServerInitialized || !CanAttackNow || (health != null && !health.IsAlive))
                yield break;

            Vector3 currentDirection = viewSync != null
                ? Quaternion.Euler(viewSync.ViewPitch, viewSync.ViewYaw, 0f) * Vector3.forward
                : direction;
            ApplyHit(origin + (transform.position - startPosition), currentDirection);
        }

        /// <summary>
        /// 범위 안의 첫 대상에게 피해를 준다.
        /// </summary>
        private void ApplyHit(Vector3 origin, Vector3 direction)
        {
            IDamageable target = FindTarget(new Ray(origin, direction.normalized));
            target?.TakeDamage(Damage, NetworkObject);
        }

        /// <summary>
        /// 구체 판정으로 범위 안에서 자기 자신을 제외한 가장 가까운 피해 대상을 찾는다.
        /// 벽 등 다른 물체가 먼저 막으면 대상이 없는 것으로 본다.
        /// </summary>
        private IDamageable FindTarget(Ray ray)
        {
            int count = Physics.SphereCastNonAlloc(ray, GameTuning.MannequinAttackRadius, hitBuffer, AttackRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            RaycastHit? closest = null;
            for (int i = 0; i < count; i++)
            {
                if (hitBuffer[i].transform.IsChildOf(transform))
                    continue;
                if (closest == null || hitBuffer[i].distance < closest.Value.distance)
                    closest = hitBuffer[i];
            }

            return closest?.collider.GetComponentInParent<IDamageable>();
        }

        /// <summary>
        /// 모든 클라이언트에서 공격 소리를 재생한다.
        /// </summary>
        [ObserversRpc]
        private void AttackObserversRpc()
        {
            if (noiseEmitter != null)
                noiseEmitter.PlayLocal(NoiseType.MannequinAttack);

            // 외형 애니메이터에 Attack 트리거가 있을 때만 공격 동작을 재생한다.
            if (skinView != null)
                skinView.TryPlayTrigger(CharacterAnimatorParameters.Attack);
        }
    }
}
