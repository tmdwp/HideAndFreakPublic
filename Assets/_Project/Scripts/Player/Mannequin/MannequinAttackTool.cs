using System.Collections;
using FishNet.Connection;
using FishNet.Object;
using GuardVsMannequin.Audio;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>피해가 외형의 맞는 순간과 맞도록 서버가 그때 범위 안 가드를 판정하는 사냥 시간 근접 공격.</summary>
    [RequireComponent(typeof(NoiseEmitter), typeof(MannequinTransformer))]
    public class MannequinAttackTool : NetworkBehaviour, ICrosshairCooldown
    {
        /// <summary>클라이언트가 보낸 원점이 비정상일 때 대신 쓸 눈 높이(m).</summary>
        private const float EyeHeight = 1.5f;

        /// <summary>도약 뒤 위치가 서버에 늦게 도착해도 되돌려지지 않도록 도약 허용 거리를 남겨 두는 시간(초).</summary>
        private const float LungeAllowanceGrace = 1f;

        // 정면이 거의 수직(바로 위·아래를 봄)일 때는 수평 방향을 믿을 수 없어 몸 정면을 쓴다.
        private const float MinHorizontalAim = 0.01f;

        // 도약 중 몸이 닿았는지 보는 캡슐의 높이(m). 구울이 숙인 자세라 사람 키보다 낮게 둔다.
        private const float LungeContactHeight = 1.6f;

        // 소유자 위치가 서버에 늦게 도착하는 만큼 도약이 끝난 뒤에도 충돌을 더 지켜보는 시간(초).
        private const float LungeContactGrace = 0.25f;

        // 판정마다 배열을 새로 만들지 않도록 재사용한다.
        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        private readonly Collider[] contactBuffer = new Collider[16];
        private readonly ToolCooldown cooldown = new();

        private IAimProvider aimProvider;
        private IPlayerInput playerInput;
        private NoiseEmitter noiseEmitter;
        private MannequinTransformer transformer;
        private CharacterSkinView skinView;
        private PlayerViewSync viewSync;
        private PlayerHealth health;
        private IStealthState stealth;
        private ICharacterMotor motor;
        private MovementValidator validator;

        public string DisplayName => Loc.Get("tool.attack");

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
            stealth = GetComponent<IStealthState>();
            motor = GetComponent<ICharacterMotor>();
            validator = GetComponent<MovementValidator>();
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
        /// HUD 쿨타임과 도약이 바로 반영되도록 소유자 화면에서 먼저 시작하고, 판정은 서버에 맡긴다.
        /// </summary>
        private void OnPrimaryPressed()
        {
            if (!IsReady)
                return;

            Ray aim = aimProvider.GetAimRay();
            cooldown.StartLocal(GameTuning.MannequinAttackCooldown);

            bool isLunge = stealth != null && stealth.IsStealthed;
            if (isLunge)
                BeginLunge(aim.direction);
            AttackServerRpc(aim.origin, aim.direction, isLunge);
        }

        /// <summary>
        /// 위치는 소유자 권한이라 소유자가 바로 뛰어들어야 반응이 늦지 않고, 다른 화면에는 위치 동기화로 보인다.
        /// 위·아래를 봐도 땅을 따라 뛰도록 수평 정면으로 뛴다.
        /// </summary>
        private void BeginLunge(Vector3 aimDirection)
        {
            stealth.OwnerConsumeForLunge();
            if (motor == null)
                return;

            Vector3 forward = Vector3.ProjectOnPlane(aimDirection, Vector3.up);
            if (forward.sqrMagnitude < MinHorizontalAim)
                forward = transform.forward;

            float duration = GameTuning.GhoulLungeDuration;
            float lungeSpeed = GhoulRules.GetLungeSpeed(GameTuning.GhoulLungeDistance, duration);
            GhoulRules.GetLungeArc(GameTuning.GhoulLungeHeight, duration, out float launchSpeed, out float arcGravity);
            motor.Dash(forward.normalized * lungeSpeed, duration, launchSpeed, arcGravity);
        }

        /// <summary>
        /// 클라이언트는 조작할 수 있어 서버가 단계·쿨타임·원점을 다시 확인하고,
        /// 도약 여부도 클라이언트 말이 아니라 서버가 아는 은신 상태로 정한다.
        /// </summary>
        [ServerRpc]
        private void AttackServerRpc(Vector3 origin, Vector3 direction, bool wantsLunge)
        {
            if (!AimRaycaster.IsValidAim(origin, direction))
                return;
            if (!CanAttackNow)
                return;
            if (!cooldown.TryStartServer(GameTuning.MannequinAttackCooldown))
                return;

            bool isLunge = wantsLunge && stealth != null && stealth.ServerIsStealthed;
            if (isLunge)
                ServerAcceptLunge();

            origin = AimRaycaster.SanitizeOrigin(origin, direction, transform, EyeHeight);
            AttackObserversRpc(isLunge);

            int damage = isLunge ? GameTuning.GhoulLungeDamage : Damage;
            float hitDelay = skinView == null ? 0f : isLunge ? skinView.LungeHitDelay : skinView.AttackHitDelay;
            if (isLunge)
                StartCoroutine(LungeHit(origin, direction, hitDelay, damage));
            else if (hitDelay <= 0f)
                ApplyHit(origin, direction, damage);
            else
                StartCoroutine(HitAfterDelay(origin, direction, hitDelay, damage));
        }

        /// <summary>
        /// 날아가는 도중 가드와 부딪히면 끝까지 가지 않아도 그 자리에서 바로 맞히고, 부딪히지 않았으면 원래처럼 맞는 순간에 앞을 판정한다.
        /// 한 번 도약으로 여러 번 맞히지 않도록 둘 중 먼저 맞힌 한 번만 피해를 준다.
        /// </summary>
        private IEnumerator LungeHit(Vector3 origin, Vector3 direction, float hitDelay, int damage)
        {
            Vector3 startPosition = transform.position;
            float startTime = Time.time;
            // 서버는 소유자가 보낸 위치를 조금 늦게 받으므로 도약 시간보다 조금 더 지켜본다.
            float contactEndTime = startTime + Mathf.Max(hitDelay, GameTuning.GhoulLungeDuration) + LungeContactGrace;
            Vector3 lungeForward = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (lungeForward.sqrMagnitude < MinHorizontalAim)
                lungeForward = transform.forward;
            lungeForward.Normalize();
            bool isSwingChecked = false;

            while (Time.time <= contactEndTime || !isSwingChecked)
            {
                // 사냥 시간이 끝났거나 변신했거나 죽었으면 도약이 끊긴 것으로 본다.
                if (!IsServerInitialized || !CanAttackNow || (health != null && !health.IsAlive))
                    yield break;

                IDamageable contact = FindLungeContact(lungeForward);
                if (contact != null && TryDamage(contact, damage))
                    yield break;

                if (!isSwingChecked && Time.time - startTime >= hitDelay)
                {
                    isSwingChecked = true;
                    Vector3 currentDirection = viewSync != null
                        ? Quaternion.Euler(viewSync.ViewPitch, viewSync.ViewYaw, 0f) * Vector3.forward
                        : direction;
                    if (ApplyHit(origin + (transform.position - startPosition), currentDirection, damage))
                        yield break;
                }
                yield return null;
            }
        }

        /// <summary>
        /// 몸이 닿았는지만 보면 되므로 구울 몸 높이의 캡슐 안에 들어온 가장 가까운 대상을 찾는다.
        /// 같은 편은 피해 규칙에서 걸러지지만, 가드가 아닌 대상에 막혀 뒤의 가드를 놓치지 않게 여기서도 가드만 고른다.
        /// 뛰어든 방향 뒤쪽에 붙어 있던 가드는 덮친 것이 아니므로 앞쪽에 있는 대상만 인정한다.
        /// </summary>
        private IDamageable FindLungeContact(Vector3 lungeForward)
        {
            float radius = GameTuning.GhoulLungeContactRadius;
            Vector3 bottom = transform.position + Vector3.up * radius;
            Vector3 top = transform.position + Vector3.up * Mathf.Max(radius, LungeContactHeight - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, contactBuffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            IDamageable closest = null;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider candidate = contactBuffer[i];
                if (candidate.transform.IsChildOf(transform))
                    continue;
                IDamageable target = candidate.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || target.Role != PlayerRole.Guard)
                    continue;

                Vector3 offset = candidate.ClosestPoint(transform.position) - transform.position;
                if (Vector3.Dot(Vector3.ProjectOnPlane(offset, Vector3.up), lungeForward) < 0f)
                    continue;

                float distance = offset.sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = target;
                }
            }
            return closest;
        }

        /// <summary>은신 중에는 이동 속도가 0이라 도약 거리가 부정 이동으로 되돌려지므로, 그 거리만큼만 따로 허락한다.</summary>
        private void ServerAcceptLunge()
        {
            stealth.ServerConsumeForLunge();
            if (validator != null)
                validator.ServerAllowDash(GhoulRules.GetLungeAllowance(GameTuning.GhoulLungeDistance),
                    GameTuning.GhoulLungeDuration + LungeAllowanceGrace);
        }

        /// <summary>
        /// 휘두르는 동안 대상을 따라 돌아보면 맞도록, 맞는 순간의 위치와 동기화된 현재 시선으로 판정한다.
        /// 도약했다면 이동한 거리만큼 원점도 함께 옮겨져 도착한 자리에서 판정한다.
        /// </summary>
        private IEnumerator HitAfterDelay(Vector3 origin, Vector3 direction, float delay, int damage)
        {
            Vector3 startPosition = transform.position;
            yield return new WaitForSeconds(delay);

            // 사냥 시간이 끝났거나 변신했거나 죽었으면 휘두르기가 끊긴 것으로 본다.
            if (!IsServerInitialized || !CanAttackNow || (health != null && !health.IsAlive))
                yield break;

            Vector3 currentDirection = viewSync != null
                ? Quaternion.Euler(viewSync.ViewPitch, viewSync.ViewYaw, 0f) * Vector3.forward
                : direction;
            ApplyHit(origin + (transform.position - startPosition), currentDirection, damage);
        }

        /// <summary>
        /// 한 번 휘두르기로 여러 명을 맞히지 않도록 가장 가까운 대상 하나에게만 피해를 준다.
        /// </summary>
        /// <returns>피해가 들어갔으면 true.</returns>
        private bool ApplyHit(Vector3 origin, Vector3 direction, int damage)
        {
            IDamageable target = FindTarget(new Ray(origin, direction.normalized));
            return target != null && TryDamage(target, damage);
        }

        /// <summary>
        /// 어두운 사냥 시간에는 맞혔는지 보기 어려워, 피해가 실제로 들어가면 공격한 사람에게만 알린다.
        /// </summary>
        /// <returns>피해가 들어갔으면 true.</returns>
        private bool TryDamage(IDamageable target, int damage)
        {
            if (!target.TakeDamage(damage, NetworkObject))
                return false;
            HitConfirmedTargetRpc(Owner, !target.IsAlive);
            return true;
        }

        /// <summary>
        /// HUD가 공격 도구를 몰라도 되도록 이 PC 안의 이벤트로 넘긴다.
        /// </summary>
        [TargetRpc]
        private void HitConfirmedTargetRpc(NetworkConnection target, bool isEliminated)
        {
            LocalCombatEvents.RaiseHitConfirmed(isEliminated);
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
        private void AttackObserversRpc(bool isLunge)
        {
            if (noiseEmitter != null)
                noiseEmitter.PlayLocal(NoiseType.MannequinAttack);

            // 외형마다 애니메이터가 달라 트리거가 없을 수 있으므로 있는 경우에만 재생하고, 도약 동작이 없으면 일반 공격으로 대신한다.
            if (skinView == null)
                return;
            if (!isLunge || !skinView.TryPlayTrigger(CharacterAnimatorParameters.Lunge))
                skinView.TryPlayTrigger(CharacterAnimatorParameters.Attack);
        }
    }
}
