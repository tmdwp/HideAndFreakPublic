using System.Collections;
using FishNet.Object;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>숨은 마네킹을 찾아내도록 반경 안 마네킹에 외곽선 표시와 감속을 거는 가드의 수류탄.</summary>
    public class GrenadeTool : NetworkBehaviour, ICooldownGauge
    {
        /// <summary>클라이언트가 보낸 원점이 비정상일 때 대신 쓸 눈 높이(m).</summary>
        private const float EyeHeight = 1.6f;

        /// <summary>던지자마자 자기 몸에 닿아 터지지 않도록 몸에서 앞으로 띄우는 거리(m).</summary>
        private const float ThrowForwardOffset = 0.6f;

        /// <summary>조준점 그대로 던지면 금방 바닥에 떨어지므로 살짝 위로 띄우는 보정(위쪽 성분).</summary>
        private const float UpwardBias = 0.15f;

        [SerializeField] private GrenadeView view;

        private readonly ToolCooldown cooldown = new();
        private readonly MannequinFinder finder = new();
        private IAimProvider aimProvider;
        private IPlayerInput playerInput;
        private CharacterSkinView skinView;

        public string DisplayName => Loc.Get("tool.grenade");

        public float CooldownRemaining => cooldown.LocalRemaining;
        public bool IsReady => CooldownRemaining <= 0f && DamageRules.CanUseGuardTools(GameServices.State);

        /// <inheritdoc />
        public float CooldownProgress => cooldown.LocalProgress;

        /// <summary>테스트 방에서 설정을 바꾸면 바로 반영되도록 매번 읽는다.</summary>
        private static float Cooldown => GameServices.CurrentSettingsOrDefault.grenadeCooldown;

        /// <summary>투척마다 찾지 않도록 미리 찾아 둔다.</summary>
        private void Awake()
        {
            aimProvider = GetComponent<IAimProvider>();
            playerInput = GetComponent<IPlayerInput>();
            skinView = GetComponent<CharacterSkinView>();
            if (view == null)
                view = GetComponent<GrenadeView>();
        }

        /// <summary>
        /// 남의 아바타가 내 우클릭에 반응하지 않도록 소유자만 입력을 듣는다.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner && playerInput != null)
                playerInput.SecondaryPressed += OnSecondaryPressed;
        }

        /// <summary>
        /// 아바타가 사라진 뒤 입력 이벤트가 이 객체를 부르지 않도록 구독을 끊는다.
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsOwner && playerInput != null)
                playerInput.SecondaryPressed -= OnSecondaryPressed;
        }

        /// <summary>
        /// HUD 쿨타임이 바로 돌도록 소유자 화면에서 먼저 시작하고, 궤적·판정은 서버에 맡긴다.
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
        /// 클라이언트는 조작할 수 있으므로 서버가 쿨타임·원점을 다시 확인하고, 튕기는 경로를 한 번만 정해 모두에게 같은 경로를 보낸다.
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

            float fuseTime = GameTuning.GrenadeFuseTime;
            GrenadeSegment[] segments = GrenadeTrajectory.Simulate(start, velocity, fuseTime, transform);
            ThrownObserversRpc(segments, fuseTime);
            StartCoroutine(ExplodeAfter(GrenadeTrajectory.GetPosition(segments, fuseTime), fuseTime));
        }

        /// <summary>터지는 연출과 효과가 같은 순간이 되도록 신관 시간만큼 기다린 뒤 서버에서 효과를 건다.</summary>
        private IEnumerator ExplodeAfter(Vector3 impactPoint, float fuseTime)
        {
            yield return new WaitForSeconds(fuseTime);
            if (!IsServerInitialized)
                yield break;

            foreach (PlayerIdentity mannequin in finder.Find(impactPoint, GameTuning.GrenadeRadius, requireLineOfSight: true))
            {
                if (mannequin.TryGetComponent(out StatusEffects effects))
                    effects.ApplyGrenadeHit();
            }
        }

        /// <summary>
        /// 튕김은 피어마다 물리 상태가 달라 어긋날 수 있으므로 서버가 정한 구간을 그대로 보내 모든 화면이 같은 수류탄을 그리게 한다.
        /// </summary>
        [ObserversRpc]
        private void ThrownObserversRpc(GrenadeSegment[] segments, float fuseTime)
        {
            if (view != null)
                view.Play(segments, fuseTime);

            // 외형마다 애니메이터가 달라 Throw 트리거가 없을 수 있으므로 있는 경우에만 재생한다.
            if (skinView != null)
                skinView.TryPlayTrigger(CharacterAnimatorParameters.Throw);
        }
    }
}
