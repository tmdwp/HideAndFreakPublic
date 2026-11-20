using FishNet.Object;
using GuardVsMannequin.Audio;
using GuardVsMannequin.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>사냥 시간 진입 시 마네킹을 구울 외형과 1인칭 사냥 모드로 바꾼다.</summary>
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
        private FirstPersonBodyView firstPersonBody;
        private NoiseEmitter noiseEmitter;
        private bool isSkinApplied;
        private bool isShriekPending;
        private bool isServerApplied;
        private bool isOwnerApplied;
        private bool spawnedDuringHunt;

        /// <summary>매 프레임 조회하지 않도록 미리 찾아 둔다.</summary>
        private void Awake()
        {
            perspective = GetComponent<IPerspectiveCamera>();
            transformer = GetComponent<MannequinTransformer>();
            health = GetComponent<PlayerHealth>();
            huntVision = GetComponent<HuntVisionEffect>();
            disguiseView = GetComponent<PropDisguiseView>();
            skinView = GetComponent<CharacterSkinView>();
            firstPersonBody = GetComponent<FirstPersonBodyView>();
            noiseEmitter = GetComponent<NoiseEmitter>();
        }

        /// <summary>사냥 중에 나타난 아바타가 엉뚱하게 비명을 지르지 않도록 기억해 둔다.</summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            spawnedDuringHunt = GameServices.CurrentStateOrWaiting == GameState.Hunting;
        }

        /// <summary>
        /// 사냥 중에 끊겼다가 돌아온 사람이 체력을 가득 다시 받지 않도록, 스폰 직전에 서버 쪽 적용을 이미 한 것으로 표시한다.
        /// </summary>
        public void ServerSkipHuntSetup()
        {
            isServerApplied = true;
        }

        /// <summary>이벤트를 놓쳐도 적용되도록 매 프레임 동기화된 상태를 보고 피어별로 한 번만 적용한다.</summary>
        private void Update()
        {
            if (!this.IsNetworkReady() || GameServices.CurrentStateOrWaiting != GameState.Hunting)
                return;

            if (!isSkinApplied)
                ApplyHuntSkin();
            else if (isShriekPending)
                Shriek();

            if (IsServerInitialized && !isServerApplied)
                ApplyOnServer();

            if (IsOwner && !isOwnerApplied)
                ApplyOnOwner();
        }

        /// <summary>
        /// 외형은 맵·프리팹이 같으므로 결과도 같아서 동기화하지 않고 모든 피어가 각자 바꾼다.
        /// </summary>
        private void ApplyHuntSkin()
        {
            isSkinApplied = true;
            isShriekPending = !spawnedDuringHunt;
            if (skinView != null)
                skinView.UseHuntSkin();
        }

        /// <summary>외형을 바꾼 프레임에는 새 애니메이터가 준비되지 않아 다음 프레임에 비명을 지른다.</summary>
        private void Shriek()
        {
            isShriekPending = false;
            if (transformer.IsDisguised || !health.IsAlive)
                return;
            if (skinView != null)
                skinView.TryPlayTrigger(CharacterAnimatorParameters.Shriek);
            if (noiseEmitter != null)
                noiseEmitter.PlayLocal(NoiseType.GhoulShriek);
        }

        /// <summary>
        /// 체력과 변신 상태는 서버 권한이라 서버에서 변신을 풀고 사냥 시간 체력 배율을 적용한다.
        /// 숨기 시간에 맞은 피해가 변신으로 사라지지 않도록 현재 체력도 같은 배율로만 늘린다.
        /// </summary>
        private void ApplyOnServer()
        {
            isServerApplied = true;
            transformer.ForceUntransform();

            GameSettings settings = GameServices.State.Settings;
            int baseMaxHealth = settings.GetStats(PlayerRole.Mannequin).MaxHealth;
            int huntMaxHealth = Mathf.Max(1, Mathf.RoundToInt(baseMaxHealth * settings.huntHealthMultiplier));
            health.SetMaxHealth(huntMaxHealth, refill: false);
        }

        /// <summary>카메라·시야 제한은 본인 화면에만 적용돼야 해서 소유자에서만 1인칭으로 바꾼다.</summary>
        private void ApplyOnOwner()
        {
            isOwnerApplied = true;
            perspective?.SetFirstPerson(true);

            foreach (Renderer bodyRenderer in CharacterSkinView.ResolveBodyRenderers(this, bodyRenderers))
                bodyRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;

            // 휘두르는 팔이 보이도록 팔다리만 남긴 복제 몸을 보인다.
            if (firstPersonBody != null)
                firstPersonBody.Show();

            // 사냥 시간에 다시 변신해도 사물이 1인칭 시야를 가리지 않게 한다.
            if (disguiseView != null)
                disguiseView.SetDisguiseShadowsOnly(true);

            if (huntVision != null)
                huntVision.Activate();
        }
    }
}
