using GuardVsMannequin.Core;
using GuardVsMannequin.Player.Mannequin;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Player
{
    /// <summary>관전자가 대상 플레이어의 시야로 보도록 카메라 위치와 방향을 계산한다.</summary>
    [RequireComponent(typeof(PlayerViewSync), typeof(PlayerIdentity))]
    public class SpectatorViewPoint : MonoBehaviour
    {
        [Tooltip("눈 위치. (가드: 카메라 피벗, 마네킹: 추적 대상)")]
        [SerializeField] private Transform eyePoint;

        [Tooltip("평소에 3인칭으로 보는 역할인지 여부. (마네킹)")]
        [SerializeField] private bool isThirdPerson;

        [SerializeField] private float followDistance = 3.5f;
        [SerializeField] private float cameraCollisionRadius = 0.2f;

        [Tooltip("1인칭에서 눈 앞으로 당기는 거리. (얼굴 모형이 화면을 가리지 않도록)")]
        [SerializeField] private float firstPersonForwardOffset = 0.35f;

        private PlayerViewSync viewSync;
        private PlayerIdentity identity;
        private IViewEffect huntViewEffect;
        private CharacterSkinView skinView;
        private PropDisguiseView disguiseView;
        private Renderer[] hiddenBodyRenderers;
        private bool isSpectated;
        private bool isEffectApplied;

        /// <summary>관전 화면이 대상의 이름과 역할을 띄우려고 쓴다.</summary>
        public PlayerIdentity Identity => identity;

        /// <summary>마네킹은 사냥 시간에만 1인칭이 되므로 카메라·몸 숨김이 이 값으로 갈린다.</summary>
        private bool IsHuntFirstPerson => isThirdPerson && GameServices.CurrentStateOrWaiting == GameState.Hunting;

        /// <summary>매 프레임 조회하지 않도록 미리 찾아 둔다.</summary>
        private void Awake()
        {
            viewSync = GetComponent<PlayerViewSync>();
            identity = GetComponent<PlayerIdentity>();
            huntViewEffect = GetComponent<IViewEffect>();
            skinView = GetComponent<CharacterSkinView>();
            disguiseView = GetComponent<PropDisguiseView>();
        }

        /// <summary>본인 화면에만 켜지는 마네킹 시야 효과를 관전자도 보도록 관전되는 동안 적용한다.</summary>
        private void Update()
        {
            UpdateBodyHidden();

            bool shouldApply = isSpectated && huntViewEffect != null
                               && GameServices.CurrentStateOrWaiting == GameState.Hunting;
            if (shouldApply == isEffectApplied)
                return;

            isEffectApplied = shouldApply;
            if (shouldApply)
                huntViewEffect.Activate();
            else
                huntViewEffect.Deactivate();
        }

        /// <summary>1인칭 관전 중 대상의 몸이 화면을 가리지 않도록 그림자만 남긴다.</summary>
        private void UpdateBodyHidden()
        {
            bool shouldHide = isSpectated && IsHuntFirstPerson;
            Renderer[] currentRenderers = shouldHide && skinView != null ? skinView.BodyRenderers : null;
            if (currentRenderers == hiddenBodyRenderers)
                return;

            SetShadowMode(hiddenBodyRenderers, ShadowCastingMode.On);
            SetShadowMode(currentRenderers, ShadowCastingMode.ShadowsOnly);
            hiddenBodyRenderers = currentRenderers;

            if (disguiseView != null)
                disguiseView.SetDisguiseShadowsOnly(shouldHide);
        }

        /// <summary>
        /// 외형 교체로 이미 파괴된 렌더러가 목록에 남아 있을 수 있어서 null을 건너뛴다.
        /// </summary>
        private static void SetShadowMode(Renderer[] renderers, ShadowCastingMode mode)
        {
            if (renderers == null)
                return;
            foreach (Renderer target in renderers)
            {
                if (target != null)
                    target.shadowCastingMode = mode;
            }
        }

        /// <summary>
        /// 관전 대상일 때만 시야 효과·몸 숨김을 걸도록 SpectatorController가 알려 준다.
        /// </summary>
        public void SetSpectated(bool spectated)
        {
            isSpectated = spectated;
        }

        /// <summary>
        /// 관전자가 대상과 같은 시야를 보도록 동기화된 시선 방향으로 카메라 위치를 계산한다.
        /// </summary>
        public void GetPose(out Vector3 position, out Quaternion rotation)
        {
            rotation = Quaternion.Euler(viewSync.ViewPitch, viewSync.ViewYaw, 0f);
            Vector3 eyePosition = eyePoint.position;

            bool useThirdPerson = isThirdPerson && !IsHuntFirstPerson;
            if (!useThirdPerson)
            {
                position = eyePosition + rotation * Vector3.forward * firstPersonForwardOffset;
                return;
            }

            // 벽에 붙어 있을 때 카메라가 벽 너머로 들어가지 않도록 뒤쪽 충돌 지점 앞에서 멈춘다.
            Vector3 backDirection = rotation * Vector3.back;
            float distance = followDistance;
            if (AimRaycaster.TryRaycast(new Ray(eyePosition, backDirection), followDistance, transform, out RaycastHit hit))
                distance = Mathf.Max(0.2f, hit.distance - cameraCollisionRadius);

            position = eyePosition + backDirection * distance;
        }
    }
}
