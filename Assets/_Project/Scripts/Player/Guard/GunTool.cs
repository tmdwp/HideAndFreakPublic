using FishNet.Connection;
using FishNet.Object;
using GuardVsMannequin.Audio;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>
    /// 가드의 총. 좌클릭 시 서버가 히트스캔으로 판정하고, 맞은 대상이 충격을 받을 수 있으면(<see cref="IImpactReceiver"/>) 튕겨낸다.
    /// 탄창(<see cref="GunMagazine"/>)을 다 쓰면 자동으로 장전하고, 탄창 안에서는 발사 간격(쿨타임)을 지킨다.
    /// 쿨타임·탄창은 서버가 최종 판정하고, 소유자는 즉시 반응·HUD 표시용으로 같은 값을 예측한다.
    /// </summary>
    public class GunTool : NetworkBehaviour, ITool, IAmmoSource
    {
        /// <summary>비정상 원점을 대체할 눈 높이(m).</summary>
        private const float EyeHeight = 1.6f;

        [SerializeField] private ShotTracerView tracerView;

        private readonly ToolCooldown cooldown = new();
        private readonly GunMagazine localMagazine = new(0f);
        private readonly GunMagazine serverMagazine = new(ToolCooldown.Tolerance);
        private IAimProvider aimProvider;
        private IPlayerInput playerInput;
        private NoiseEmitter noiseEmitter;
        private CharacterSkinView skinView;

        public string DisplayName => Loc.Get("tool.gun");
        // 출발 보호(탐색 시작 직후 도구 잠금)와 장전도 쿨타임처럼 남은 시간으로 보여준다.
        public float CooldownRemaining => Mathf.Max(Mathf.Max(cooldown.LocalRemaining, ReloadRemaining), DamageRules.GetGuardToolLockRemaining(GameServices.State));
        public bool IsReady => CooldownRemaining <= 0f && DamageRules.CanUseGuardTools(GameServices.State);

        /// <inheritdoc />
        public int Ammo => localMagazine.GetAmmo(MagazineSize, Time.time);

        /// <inheritdoc />
        public int MagazineSize => Mathf.Max(1, GameServices.CurrentSettingsOrDefault.gunMagazineSize);

        /// <inheritdoc />
        public float ReloadRemaining => localMagazine.GetReloadRemaining(Time.time);

        /// <summary>호스트 설정의 총 발사 간격.</summary>
        private static float Cooldown => GameServices.CurrentSettingsOrDefault.gunCooldown;

        /// <summary>호스트 설정의 장전 시간.</summary>
        private static float ReloadTime => Mathf.Max(0f, GameServices.CurrentSettingsOrDefault.gunReloadTime);

        /// <summary>한 발의 피해량. (가드 능력치, 최소 1)</summary>
        private static int Damage => Mathf.Max(1, GameServices.CurrentSettingsOrDefault.GetStats(PlayerRole.Guard).AttackDamage);

        /// <summary>
        /// 조준, 입력, 소음 구현을 캐시한다.
        /// </summary>
        private void Awake()
        {
            aimProvider = GetComponent<IAimProvider>();
            noiseEmitter = GetComponent<NoiseEmitter>();
            playerInput = GetComponent<IPlayerInput>();
            skinView = GetComponent<CharacterSkinView>();
        }

        /// <summary>
        /// 소유자만 사격 입력을 구독한다.
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
        /// 쿨타임·장전이 끝났고 탄이 있으면 조준 광선을 서버로 보낸다.
        /// </summary>
        private void OnPrimaryPressed()
        {
            if (!IsReady || !localMagazine.TryFire(MagazineSize, ReloadTime, Time.time, out _))
                return;

            Ray aim = aimProvider.GetAimRay();
            cooldown.StartLocal(Cooldown);
            FireServerRpc(aim.origin, aim.direction);
        }

        /// <summary>
        /// 서버에서 사격을 검증하고 히트스캔 판정을 한다.
        /// </summary>
        [ServerRpc]
        private void FireServerRpc(Vector3 origin, Vector3 direction)
        {
            if (!AimRaycaster.IsValidAim(origin, direction))
                return;
            if (!DamageRules.CanUseGuardTools(GameServices.State))
                return;
            // 거부하면 소유자가 이미 한 발을 쓴 것으로 예측했으므로 서버 탄창 상태로 맞춘다. (지연 등)
            bool startedReload = false;
            if (!cooldown.IsServerReady || !serverMagazine.TryFire(MagazineSize, ReloadTime, Time.time, out startedReload))
            {
                MagazineCorrectionTargetRpc(Owner, serverMagazine.GetAmmo(MagazineSize, Time.time), serverMagazine.GetReloadRemaining(Time.time));
                return;
            }
            cooldown.StartServer(Cooldown);

            origin = AimRaycaster.SanitizeOrigin(origin, direction, transform, EyeHeight);

            Ray ray = new Ray(origin, direction.normalized);
            Vector3 endPoint = ray.GetPoint(GameConstants.GunRange);

            bool didHit = AimRaycaster.TryRaycast(ray, GameConstants.GunRange, transform, out RaycastHit hit);
            if (didHit)
            {
                endPoint = hit.point;
                IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                target?.TakeDamage(Damage, NetworkObject);

                // 맞은 변신 사물·변신한 마네킹을 튕겨낸다. (피해 뒤에 줘서 이번 발에 죽은 마네킹은 날아가지 않음)
                IImpactReceiver impactReceiver = hit.collider.GetComponentInParent<IImpactReceiver>();
                if (impactReceiver != null)
                    impactReceiver.ReceiveImpact(hit.point, ray.direction * PropPhysicsRules.GetGunImpulse(impactReceiver.ImpactMass));
            }

            ShotFiredObserversRpc(origin, endPoint, didHit, startedReload);
        }

        /// <summary>
        /// 소유자의 탄창 예측을 서버 상태로 맞춘다.
        /// </summary>
        [TargetRpc]
        private void MagazineCorrectionTargetRpc(NetworkConnection target, int ammo, float reloadRemaining)
        {
            localMagazine.Overwrite(MagazineSize, ammo, reloadRemaining, Time.time);
        }

        /// <summary>
        /// 모든 클라이언트에 사격 궤적, 탄착 지점, 총소리(마지막 발이면 장전 동작)를 재생한다.
        /// </summary>
        [ObserversRpc]
        private void ShotFiredObserversRpc(Vector3 origin, Vector3 endPoint, bool didHit, bool startedReload)
        {
            if (tracerView != null)
                tracerView.Show(origin, endPoint, didHit);

            // 사격 RPC가 이미 모든 클라이언트에 전달되므로 소음은 추가 전송 없이 로컬 재생한다.
            if (noiseEmitter != null)
                noiseEmitter.PlayLocal(NoiseType.GunShot);

            // 외형 애니메이터에 Shoot 트리거가 있을 때만 사격 동작을 재생한다.
            if (skinView != null)
            {
                skinView.TryPlayTrigger(CharacterAnimatorParameters.Shoot);
                // 장전 동작도 트리거가 있는 외형에서만 재생한다.
                if (startedReload)
                    skinView.TryPlayTrigger(CharacterAnimatorParameters.Reload);
            }
        }
    }
}
