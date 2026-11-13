using System.Collections;
using FishNet.Object;
using GuardVsMannequin.Audio;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>피해가 외형의 맞는 순간과 맞도록 서버가 그때 범위 안 가드를 판정하는 사냥 시간 근접 공격.</summary>
    [RequireComponent(typeof(NoiseEmitter), typeof(MannequinTransformer))]
    public class MannequinAttackTool : NetworkBehaviour, ICrosshairCooldown, IMoveSpeedModifier
    {
        /// <summary>클라이언트가 보낸 원점이 비정상일 때 대신 쓸 눈 높이(m).</summary>
        private const float EyeHeight = 1.5f;

        // 판정마다 배열을 새로 만들지 않도록 재사용한다.
        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        private readonly ToolCooldown cooldown = new();

        private IAimProvider aimProvider;
        private IPlayerInput playerInput;
        private NoiseEmitter noiseEmitter;
        private MannequinTransformer transformer;
        private CharacterSkinView skinView;
        private PlayerViewSync viewSync;
        private PlayerHealth health;
        private float moveLockEndTime;

        public string DisplayName => Loc.Get("tool.attack");

        /// <inheritdoc />
        public float SpeedMultiplier => Time.time < moveLockEndTime ? 0f : 1f;
        public float CooldownRemaining => cooldown.LocalRemaining;
        public bool IsReady => CooldownRemaining <= 0f && CanAttackNow;

        /// <inheritdoc />
        public float CooldownProgress => cooldown.LocalProgress;

        /// <summary>좌클릭이 탐색 단계에서는 전원 장치 파괴라 사냥 시간에만, 사물 모습으로는 공격할 수 없어 변신하지 않았을 때만 허용한다.</summary>
        private bool CanAttackNow => GameServices.CurrentStateOrWaiting == GameState.Hunting && !transformer.IsDisguised;

        /// <summary>테스트 방에서 설정을 바꾸면 바로 반영되도록 매번 읽는다.</summary>
        private static float AttackRange => GameServices.CurrentSettingsOrDefault.mannequinAttackRange;

        /// <summary>설정이 0이어도 맞히면 피해가 들어가도록 최소 1로 둔다.</summary>
        private static int Damage => Mathf.Max(1, GameServices.CurrentSettingsOrDefault.GetStats(PlayerRole.Mannequin).AttackDamage);

        /// <summary>
        /// 공격마다 GetComponent를 부르지 않도록 미리 찾아 둔다.
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
        /// 남의 아바타가 내 클릭에 반응하지 않도록 소유자만 좌클릭 입력을 듣는다.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner && playerInput != null)
                playerInput.PrimaryPressed += OnPrimaryPressed;
        }

        /// <summary>
        /// 아바타가 사라진 뒤 입력 이벤트가 이 객체를 부르지 않도록 구독을 끊는다.
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsOwner && playerInput != null)
                playerInput.PrimaryPressed -= OnPrimaryPressed;
        }

        /// <summary>
        /// HUD 쿨타임과 이동 멈춤이 바로 반영되도록 소유자 화면에서 먼저 시작하고, 판정은 서버에 맡긴다.
        /// </summary>
        private void OnPrimaryPressed()
        {
            if (!IsReady)
                return;

            Ray aim = aimProvider.GetAimRay();
            cooldown.StartLocal(GameTuning.MannequinAttackCooldown);
            // 서버 응답을 기다리면 그사이 움직이므로 누른 순간 바로 멈춘다.
            LockMovement();
            AttackServerRpc(aim.origin, aim.direction);
        }

        /// <summary>
        /// 휘두르면서 쫓아가면 피하기가 너무 어려워서, 공격 동작 동안 이동 속도 배율을 0으로 만든다.
        /// </summary>
        private void LockMovement()
        {
            moveLockEndTime = Time.time + GameTuning.MannequinAttackMoveLock;
        }

        /// <summary>클라이언트는 조작할 수 있어 서버가 단계·쿨타임·원점을 다시 확인한다.</summary>
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
        /// 휘두르는 동안 대상을 따라 돌아보면 맞도록, 맞는 순간의 위치와 동기화된 현재 시선으로 판정한다.
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
        /// 한 번 휘두르기로 여러 명을 맞히지 않도록 가장 가까운 대상 하나에게만 피해를 준다.
        /// </summary>
        private void ApplyHit(Vector3 origin, Vector3 direction)
        {
            IDamageable target = FindTarget(new Ray(origin, direction.normalized));
            target?.TakeDamage(Damage, NetworkObject);
        }

        /// <summary>근접 공격은 정확히 조준하기 어려워 광선 대신 구체로 판정한다.</summary>
        private IDamageable FindTarget(Ray ray)
        {
            int count = Physics.SphereCastNonAlloc(ray, GameTuning.MannequinAttackRadius, hitBuffer, AttackRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            RaycastHit? closestHit = null;
            for (int i = 0; i < count; i++)
            {
                if (hitBuffer[i].transform.IsChildOf(transform))
                    continue;
                if (closestHit == null || hitBuffer[i].distance < closestHit.Value.distance)
                    closestHit = hitBuffer[i];
            }

            return closestHit?.collider.GetComponentInParent<IDamageable>();
        }

        /// <summary>
        /// 모든 화면에서 공격이 보이고 들리도록 소리와 동작을 재생한다.
        /// </summary>
        [ObserversRpc]
        private void AttackObserversRpc()
        {
            if (noiseEmitter != null)
                noiseEmitter.PlayLocal(NoiseType.MannequinAttack);

            // 외형마다 애니메이터가 달라 Attack 트리거가 없을 수 있으므로 있는 경우에만 재생한다.
            if (skinView != null)
                skinView.TryPlayTrigger(CharacterAnimatorParameters.Attack);
        }
    }
}
