using System.Collections;
using FishNet.Object;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>
    /// 가드의 수류탄(우클릭). 던지면 포물선으로 날아가 닿는 순간 터지고,
    /// 반경 안의 실제 마네킹(변신 여부 무관, 벽 너머 제외)에게 외곽선 표시·감속을 건다. 피해는 없다.
    /// 판정은 서버가 한다: 클라이언트는 조준만 보내고, 서버가 궤적·폭발 지점을 정해 모두에게 같은 연출(<see cref="GrenadeView"/>)을 보낸다.
    /// 궤적이 결정적이므로 수류탄을 네트워크 오브젝트로 만들지 않는다.
    /// </summary>
    public class GrenadeTool : NetworkBehaviour, ITool
    {
        /// <summary>비정상 원점을 대체할 눈 높이(m).</summary>
        private const float EyeHeight = 1.6f;

        /// <summary>던지는 위치를 몸에서 앞으로 띄우는 거리(m).</summary>
        private const float ThrowForwardOffset = 0.6f;

        /// <summary>조준보다 살짝 위로 던지는 각도 보정(위쪽 성분).</summary>
        private const float UpwardBias = 0.15f;

        [SerializeField] private GrenadeView view;

        private readonly ToolCooldown cooldown = new();
        private readonly MannequinFinder finder = new();
        private IAimProvider aimProvider;
        private IPlayerInput playerInput;
        private CharacterSkinView skinView;

        public string DisplayName => Loc.Get("tool.grenade");

        // 출발 보호(탐색 시작 직후 도구 잠금)도 쿨타임처럼 남은 시간으로 보여준다.
        public float CooldownRemaining => Mathf.Max(cooldown.LocalRemaining, DamageRules.GetGuardToolLockRemaining(GameServices.State));
        public bool IsReady => CooldownRemaining <= 0f && DamageRules.CanUseGuardTools(GameServices.State);

        /// <summary>호스트 설정의 수류탄 쿨타임.</summary>
        private static float Cooldown => GameServices.CurrentSettingsOrDefault.grenadeCooldown;

        /// <summary>
        /// 조준·입력·외형 구현을 캐시한다.
        /// </summary>
        private void Awake()
        {
            aimProvider = GetComponent<IAimProvider>();
            playerInput = GetComponent<IPlayerInput>();
            skinView = GetComponent<CharacterSkinView>();
            if (view == null)
                view = GetComponent<GrenadeView>();
        }

        /// <summary>
        /// 소유자만 우클릭 입력을 구독한다.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner && playerInput != null)
                playerInput.SecondaryPressed += OnSecondaryPressed;
        }

        /// <summary>
        /// 입력 구독을 해제한다.
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsOwner && playerInput != null)
                playerInput.SecondaryPressed -= OnSecondaryPressed;
        }

        /// <summary>
        /// 쿨타임이 끝났으면 조준 광선을 서버로 보낸다.
        /// </summary>
        private void OnSecondaryPressed()
        {
            if (!IsReady)
                return;

            Ray aim = aimProvider.GetAimRay();
            cooldown.StartLocal(Cooldown);
            ThrowServerRpc(aim.origin, aim.direction);
        }

        /// <summary>
        /// 서버에서 투척을 검증하고 궤적·폭발 지점을 정한다.
        /// </summary>
        [ServerRpc]
        private void ThrowServerRpc(Vector3 origin, Vector3 direction)
        {
            if (!AimRaycaster.IsValidAim(origin, direction))
                return;
            if (!DamageRules.CanUseGuardTools(GameServices.State))
                return;
            if (!cooldown.TryStartServer(Cooldown))
                return;

            origin = AimRaycaster.SanitizeOrigin(origin, direction, transform, EyeHeight);
            Vector3 throwDirection = (direction.normalized + Vector3.up * UpwardBias).normalized;
            Vector3 start = origin + throwDirection * ThrowForwardOffset;
            Vector3 velocity = throwDirection * GameTuning.GrenadeThrowSpeed;

            GrenadeTrajectory.TryFindImpact(start, velocity, GameTuning.GrenadeMaxFlightTime, transform, out Vector3 impact, out float flightTime);
            ThrownObserversRpc(start, velocity, flightTime);
            StartCoroutine(ExplodeAfter(impact, flightTime));
        }

        /// <summary>
        /// 비행 시간 뒤 폭발 지점 주변의 마네킹에게 효과를 건다. (서버)
        /// </summary>
        private IEnumerator ExplodeAfter(Vector3 impact, float flightTime)
        {
            yield return new WaitForSeconds(flightTime);
            if (!IsServerInitialized)
                yield break;

            foreach (PlayerIdentity mannequin in finder.Find(impact, GameTuning.GrenadeRadius, requireLineOfSight: true))
            {
                if (mannequin.TryGetComponent(out StatusEffects effects))
                    effects.ApplyGrenadeHit();
            }
        }

        /// <summary>
        /// 모든 클라이언트에서 같은 궤적의 수류탄 연출과 던지는 동작을 재생한다.
        /// </summary>
        [ObserversRpc]
        private void ThrownObserversRpc(Vector3 start, Vector3 velocity, float flightTime)
        {
            if (view != null)
                view.Play(start, velocity, flightTime);

            // 외형 애니메이터에 Throw 트리거가 있을 때만 던지는 동작을 재생한다.
            if (skinView != null)
                skinView.TryPlayTrigger(CharacterAnimatorParameters.Throw);
        }
    }
}
