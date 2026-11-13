using FishNet.Connection;
using FishNet.Object;
using GuardVsMannequin.Audio;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>판정을 조작하지 못하도록 서버가 히트스캔으로 판정하는 가드의 총.</summary>
    public class GunTool : NetworkBehaviour, ITool, IAmmoSource
    {
        /// <summary>클라이언트가 보낸 원점이 비정상일 때 대신 쓸 눈 높이(m).</summary>
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
        // 장전도 쿨타임처럼 남은 시간으로 보여 준다.
        public float CooldownRemaining => Mathf.Max(cooldown.LocalRemaining, ReloadRemaining);
        public bool IsReady => CooldownRemaining <= 0f && DamageRules.CanUseGuardTools(GameServices.State);

        /// <inheritdoc />
        public int Ammo => localMagazine.GetAmmo(MagazineSize, Time.time);

        /// <inheritdoc />
        public int MagazineSize => Mathf.Max(1, GameServices.CurrentSettingsOrDefault.gunMagazineSize);

        /// <inheritdoc />
        public float ReloadRemaining => localMagazine.GetReloadRemaining(Time.time);

        /// <inheritdoc />
        public float FireCooldownRemaining => cooldown.LocalRemaining;

        /// <summary>테스트 방에서 설정을 바꾸면 바로 반영되도록 캐시하지 않고 매번 읽는다.</summary>
        private static float Cooldown => GameServices.CurrentSettingsOrDefault.gunCooldown;

        /// <summary>테스트 방 설정 변경이 바로 반영되도록 매번 읽는 발당 장전 시간.</summary>
        private static float ReloadTimePerRound => Mathf.Max(0f, GameServices.CurrentSettingsOrDefault.gunReloadTime);

        /// <summary>설정이 0이어도 맞히면 피해가 들어가도록 최소 1로 둔다.</summary>
        private static int Damage => Mathf.Max(1, GameServices.CurrentSettingsOrDefault.GetStats(PlayerRole.Guard).AttackDamage);

        /// <summary>
        /// 사격마다 GetComponent를 부르지 않도록 조준·입력·소음·외형 구현을 미리 찾아 둔다.
        /// </summary>
        private void Awake()
        {
            aimProvider = GetComponent<IAimProvider>();
            noiseEmitter = GetComponent<NoiseEmitter>();
            playerInput = GetComponent<IPlayerInput>();
            skinView = GetComponent<CharacterSkinView>();
        }

        /// <summary>
        /// 남의 아바타가 내 클릭에 반응하지 않도록 소유자만 사격·장전 입력을 듣는다.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner && playerInput != null)
            {
                playerInput.PrimaryPressed += OnPrimaryPressed;
                playerInput.ReloadPressed += OnReloadPressed;
            }
        }

        /// <summary>
        /// 아바타가 사라진 뒤 입력 이벤트가 이 객체를 부르지 않도록 구독을 끊는다.
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsOwner && playerInput != null)
            {
                playerInput.PrimaryPressed -= OnPrimaryPressed;
                playerInput.ReloadPressed -= OnReloadPressed;
            }
        }

        /// <summary>
        /// 쏘는 느낌이 늦지 않도록 소유자 화면에서 먼저 탄과 쿨타임을 줄이고, 판정은 서버에 맡긴다.
        /// </summary>
        private void OnPrimaryPressed()
        {
            if (!IsReady || !localMagazine.TryFire(MagazineSize, ReloadTimePerRound, Time.time, out _))
                return;

            Ray aim = aimProvider.GetAimRay();
            cooldown.StartLocal(Cooldown);
            FireServerRpc(aim.origin, aim.direction);
        }

        /// <summary>장전은 발사 간격과 별개라 쿨타임 중에도 시작하고, 소유자에서 먼저 시작해 서버에 알린다.</summary>
        private void OnReloadPressed()
        {
            if (!DamageRules.CanUseGuardTools(GameServices.State))
                return;
            if (!localMagazine.TryStartReload(MagazineSize, ReloadTimePerRound, Time.time))
                return;

            ReloadServerRpc();
        }

        /// <summary>
        /// 소유자가 이미 장전을 시작했다고 예측했으므로, 서버가 거부하면 소유자 탄창을 서버 상태로 되돌린다.
        /// </summary>
        [ServerRpc]
        private void ReloadServerRpc()
        {
            if (!DamageRules.CanUseGuardTools(GameServices.State) || !serverMagazine.TryStartReload(MagazineSize, ReloadTimePerRound, Time.time))
            {
                MagazineCorrectionTargetRpc(Owner, serverMagazine.GetAmmo(MagazineSize, Time.time), serverMagazine.GetReloadRemaining(Time.time));
                return;
            }

            ReloadStartedObserversRpc();
        }

        /// <summary>다른 사람 화면에서도 장전이 보이도록 모두에게 재생한다.</summary>
        [ObserversRpc]
        private void ReloadStartedObserversRpc()
        {
            if (skinView != null)
                skinView.TryPlayTrigger(CharacterAnimatorParameters.Reload);
        }

        /// <summary>
        /// 클라이언트는 조작할 수 있으므로 쿨타임·탄창·원점을 서버에서 다시 확인한 뒤 히트스캔으로 판정한다.
        /// </summary>
        [ServerRpc]
        private void FireServerRpc(Vector3 origin, Vector3 direction)
        {
            if (!AimRaycaster.IsValidAim(origin, direction))
                return;
            if (!DamageRules.CanUseGuardTools(GameServices.State))
                return;
            // 소유자가 이미 한 발을 쓴 것으로 예측했으므로 거부하면 서버 탄창 상태로 맞춘다.
            bool startedReload = false;
            if (!cooldown.IsServerReady || !serverMagazine.TryFire(MagazineSize, ReloadTimePerRound, Time.time, out startedReload))
            {
                MagazineCorrectionTargetRpc(Owner, serverMagazine.GetAmmo(MagazineSize, Time.time), serverMagazine.GetReloadRemaining(Time.time));
                return;
            }
            cooldown.StartServer(Cooldown);

            origin = AimRaycaster.SanitizeOrigin(origin, direction, transform, EyeHeight);

            Ray ray = new Ray(origin, direction.normalized);
            Vector3 endPoint = ray.GetPoint(GameConstants.GunRange);

            // 캐릭터는 외형 자세를 따라가는 판정 캡슐로 맞힌다.
            bool didHit = AimRaycaster.TryRaycastHittable(ray, GameConstants.GunRange, transform, out RaycastHit hit);
            if (didHit)
            {
                endPoint = hit.point;
                IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                target?.TakeDamage(Damage, NetworkObject);

                // 이번 발에 죽은 마네킹은 날아가지 않도록 피해 뒤에 충격을 준다.
                IImpactReceiver impactReceiver = hit.collider.GetComponentInParent<IImpactReceiver>();
                if (impactReceiver != null)
                    impactReceiver.ReceiveImpact(hit.point, ray.direction * PropPhysicsRules.GetGunImpulse(impactReceiver.ImpactMass));
            }

            ShotFiredObserversRpc(origin, endPoint, didHit, startedReload);
        }

        /// <summary>
        /// 서버가 거부한 사격·장전을 소유자 화면이 계속 믿고 있으면 탄 수가 어긋나므로 서버 값으로 덮어쓴다.
        /// </summary>
        [TargetRpc]
        private void MagazineCorrectionTargetRpc(NetworkConnection target, int ammo, float reloadRemaining)
        {
            localMagazine.Overwrite(MagazineSize, ammo, reloadRemaining, Time.time);
        }

        /// <summary>
        /// 판정은 서버에서 끝났으므로, 모든 화면에서 같은 사격이 보이고 들리도록 궤적·소리·동작만 재생한다.
        /// </summary>
        [ObserversRpc]
        private void ShotFiredObserversRpc(Vector3 origin, Vector3 endPoint, bool didHit, bool startedReload)
        {
            if (tracerView != null)
                tracerView.Show(origin, endPoint, didHit);

            // 사격 RPC가 이미 모든 클라이언트에 전달되므로 소음은 추가 전송 없이 로컬 재생한다.
            if (noiseEmitter != null)
                noiseEmitter.PlayLocal(NoiseType.GunShot);

            // 외형마다 애니메이터가 달라 트리거가 없을 수 있으므로 있는 경우에만 재생한다.
            if (skinView != null)
            {
                skinView.TryPlayTrigger(CharacterAnimatorParameters.Shoot);
                if (startedReload)
                    skinView.TryPlayTrigger(CharacterAnimatorParameters.Reload);
            }
        }
    }
}
