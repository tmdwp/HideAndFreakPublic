using FishNet.Object;
using GuardVsMannequin.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>
    /// 사냥 시간 진입 시 마네킹을 사냥 모드로 바꾼다.
    /// - 모든 피어: 외형을 사냥 외형(맵 지정, 없으면 기본 Ghoul)으로 교체 (<see cref="CharacterSkinView.UseHuntSkin"/>)
    /// - 서버: 강제 변신 해제(이후 다시 변신 가능), 최대 체력 = 기본 체력 × huntHealthMultiplier (가득 채움)
    /// - 소유자: 1인칭 전환(본인 몸은 그림자만), 제한된 시야(HuntVisionEffect)
    /// 동기화된 게임 상태를 보고 각자 한 번씩 적용한다.
    /// </summary>
    [RequireComponent(typeof(MannequinTransformer), typeof(PlayerHealth))]
    public class MannequinHuntingMode : NetworkBehaviour
    {
        [Tooltip("1인칭에서 본인 화면에 보이지 않게(그림자만) 할 몸 렌더러.")]
        [SerializeField] private Renderer[] bodyRenderers;

        private IPerspectiveCamera perspective;
        private MannequinTransformer transformer;
        private PlayerHealth health;
        private HuntVisionEffect huntVision;
        private PropDisguiseView disguiseView;
        private CharacterSkinView skinView;
        private bool skinApplied;
        private bool serverApplied;
        private bool ownerApplied;

        /// <summary>
        /// 같은 오브젝트의 구성 요소를 캐시한다.
        /// </summary>
        private void Awake()
        {
            perspective = GetComponent<IPerspectiveCamera>();
            transformer = GetComponent<MannequinTransformer>();
            health = GetComponent<PlayerHealth>();
            huntVision = GetComponent<HuntVisionEffect>();
            disguiseView = GetComponent<PropDisguiseView>();
            skinView = GetComponent<CharacterSkinView>();
        }

        /// <summary>
        /// 사냥 시간이 되면 외형 교체와 서버/소유자 측 변경을 한 번씩 적용한다.
        /// 외형을 먼저 바꿔야 소유자의 1인칭 그림자 처리가 새 몸체에 적용된다.
        /// </summary>
        private void Update()
        {
            if (!this.IsNetworkReady() || GameServices.CurrentStateOrWaiting != GameState.Hunting)
                return;

            if (!skinApplied)
                ApplyHuntSkin();

            if (IsServerInitialized && !serverApplied)
                ApplyOnServer();

            if (IsOwner && !ownerApplied)
                ApplyOnOwner();
        }

        /// <summary>
        /// 사냥 외형으로 바꾼다. (모든 피어에서 각자 실행, 외형은 맵·프리팹이 같으므로 결과도 같다)
        /// </summary>
        private void ApplyHuntSkin()
        {
            skinApplied = true;
            if (skinView != null)
                skinView.UseHuntSkin();
        }

        /// <summary>
        /// 변신을 해제하고 사냥 시간 체력을 적용한다.
        /// </summary>
        private void ApplyOnServer()
        {
            serverApplied = true;
            transformer.ForceUntransform();

            GameSettings settings = GameServices.State.Settings;
            int baseMaxHealth = settings.GetStats(PlayerRole.Mannequin).MaxHealth;
            int huntMaxHealth = Mathf.Max(1, Mathf.RoundToInt(baseMaxHealth * settings.huntHealthMultiplier));
            health.SetMaxHealth(huntMaxHealth, refill: true);
        }

        /// <summary>
        /// 1인칭으로 전환한다. (사냥 시간 속도는 MoveSpeedController가 적용)
        /// </summary>
        private void ApplyOnOwner()
        {
            ownerApplied = true;
            perspective?.SetFirstPerson(true);

            foreach (Renderer bodyRenderer in CharacterSkinView.ResolveBodyRenderers(this, bodyRenderers))
                bodyRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;

            // 사냥 시간에 다시 변신해도 사물이 1인칭 시야를 가리지 않게 한다.
            if (disguiseView != null)
                disguiseView.SetDisguiseShadowsOnly(true);

            // 주변만 보이도록 시야를 제한한다. (본인 화면에만 적용)
            if (huntVision != null)
                huntVision.Activate();
        }
    }
}
