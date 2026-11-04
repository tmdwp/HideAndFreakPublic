using GuardVsMannequin.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 캐릭터 몸의 흔들림(걷기·달리기 애니메이션의 골반 출렁임)을 재서 나눠 준다.
    /// - 카메라: 소유자가 1인칭일 때 개인 설정(<see cref="CameraSwaySettings"/>) 배율만큼 카메라를 흔든다.
    ///   그리는 동안에만 옮겼다가 되돌리므로 조준(카메라 정면 광선)·소리 위치는 흔들리지 않는다.
    /// - 총 등 다른 구성 요소: <see cref="WorldOffset"/>을 읽어 같은 흔들림을 더한다(<see cref="Guard.GuardWeaponView"/>).
    /// 휴머노이드 외형은 골반(Hips), 일반 외형은 스킨드 메시의 뿌리 뼈를 쓴다. 외형이 없으면 흔들림 없음.
    /// </summary>
    public class CharacterSway : MonoBehaviour
    {
        [Tooltip("소유자 화면 카메라. 이 카메라를 그릴 때만 흔든다.")]
        [SerializeField] private Camera viewCamera;

        [Tooltip("기준점이 따라오는 시간(초). 길수록 느린 움직임까지 흔들림으로 남는다.")]
        [SerializeField] private float smoothTime = 0.35f;

        [Tooltip("축별 배율. (좌우, 위아래, 앞뒤)")]
        [SerializeField] private Vector3 axisWeights = new(0.6f, 1f, 0.3f);

        [Tooltip("흔들림 최대 거리(m).")]
        [SerializeField] private float maxOffset = 0.08f;

        [Header("확인용 (Play 중 값 보기, 수정해도 쓰지 않음)")]
        [Tooltip("지금 재고 있는 뼈. 비어 있으면 외형이 없거나 뼈를 찾지 못해 흔들림이 0이다.")]
        [SerializeField] private Transform debugBone;

        [Tooltip("이번 프레임 흔들림(월드, m). 걸을 때 수 cm씩 움직여야 정상이다.")]
        [SerializeField] private Vector3 debugWorldOffset;

        private readonly BoneSwayTracker tracker = new();
        private CharacterSkinView skinView;
        private IPerspectiveCamera perspective;
        private Animator trackedAnimator;
        private Transform trackedBone;
        private Vector3 worldOffset;
        private int sampledFrame = -1;
        private Vector3 appliedCameraOffset;
        private bool isCameraOffsetApplied;

        /// <summary>이번 프레임 몸 흔들림(월드 좌표, m). 외형이 없으면 0.</summary>
        public Vector3 WorldOffset
        {
            get
            {
                SampleOncePerFrame();
                return worldOffset;
            }
        }

        /// <summary>
        /// 지금 카메라에 적용하는 흔들림 배율. 1인칭이 아니거나 설정이 꺼져 있으면 0.
        /// (1인칭 총처럼 카메라 앞에 붙은 물체가 카메라 흔들림 위에 자기 흔들림을 더할 때 쓴다)
        /// </summary>
        public float CameraSwayScale
        {
            get
            {
                if (viewCamera == null || !viewCamera.isActiveAndEnabled)
                    return 0f;
                if (perspective != null && !perspective.IsFirstPerson)
                    return 0f;
                return CameraSwaySettings.GetScale(OptionsStore.Current);
            }
        }

        /// <summary>
        /// 외형과 시점 구현을 캐시한다.
        /// </summary>
        private void Awake()
        {
            skinView = GetComponent<CharacterSkinView>();
            perspective = GetComponent<IPerspectiveCamera>();
        }

        /// <summary>
        /// 카메라를 그리기 직전·직후 이벤트를 구독한다.
        /// </summary>
        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        /// <summary>
        /// 구독을 해제하고 옮겨 둔 카메라를 되돌린다.
        /// </summary>
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            RestoreCamera();
        }

        /// <summary>
        /// 소유자 카메라를 그리기 직전에 흔들림만큼 옮긴다. (1인칭이고 설정이 켜져 있을 때)
        /// </summary>
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
        {
            if (renderingCamera != viewCamera || isCameraOffsetApplied)
                return;

            float scale = CameraSwayScale;
            if (scale <= 0f)
                return;

            appliedCameraOffset = WorldOffset * scale;
            viewCamera.transform.position += appliedCameraOffset;
            isCameraOffsetApplied = true;
        }

        /// <summary>
        /// 다 그린 뒤 카메라를 원래 자리로 되돌린다.
        /// </summary>
        private void OnEndCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
        {
            if (renderingCamera == viewCamera)
                RestoreCamera();
        }

        /// <summary>
        /// 옮겨 둔 카메라가 있으면 되돌린다.
        /// </summary>
        private void RestoreCamera()
        {
            if (!isCameraOffsetApplied)
                return;
            if (viewCamera != null)
                viewCamera.transform.position -= appliedCameraOffset;
            isCameraOffsetApplied = false;
        }

        /// <summary>
        /// 한 프레임에 한 번만 흔들림을 잰다. (카메라·총이 같은 값을 쓰도록)
        /// </summary>
        private void SampleOncePerFrame()
        {
            if (sampledFrame == Time.frameCount)
                return;
            sampledFrame = Time.frameCount;

            Animator animator = skinView != null ? skinView.Animator : null;
            if (animator != trackedAnimator)
            {
                trackedAnimator = animator;
                trackedBone = FindSwayBone(animator);
                tracker.Reset();
            }

            if (trackedBone == null)
            {
                worldOffset = Vector3.zero;
                return;
            }
            KeepOwnerAnimating(trackedAnimator);

            Transform modelRoot = trackedAnimator.transform;
            Vector3 boneLocal = modelRoot.InverseTransformPoint(trackedBone.position);
            Vector3 localOffset = tracker.Sample(boneLocal, Time.deltaTime, smoothTime, axisWeights);
            // 외형 크기(모델 배율)를 반영한 월드 거리로 바꾼 뒤, 순간이동·자세 급변 때 튀지 않게 자른다.
            worldOffset = Vector3.ClampMagnitude(modelRoot.TransformVector(localOffset), maxOffset);
            debugBone = trackedBone;
            debugWorldOffset = worldOffset;
        }

        /// <summary>
        /// 소유자 화면에서는 몸이 그림자로만 그려져, "안 보이면 뼈 갱신 생략" 설정이면 그림자가 없는 곳에서 뼈가 멈춘다.
        /// 그러면 흔들림이 0이 되므로 소유자(카메라가 켜진 아바타)의 애니메이터는 항상 갱신하게 한다.
        /// </summary>
        private void KeepOwnerAnimating(Animator animator)
        {
            if (viewCamera != null && viewCamera.isActiveAndEnabled && animator.cullingMode != AnimatorCullingMode.AlwaysAnimate)
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        /// <summary>
        /// 흔들림을 잴 뼈: 휴머노이드는 골반, 일반 외형은 스킨드 메시의 뿌리 뼈. 없으면 null.
        /// </summary>
        private static Transform FindSwayBone(Animator animator)
        {
            if (animator == null)
                return null;
            if (animator.isHuman)
                return animator.GetBoneTransform(HumanBodyBones.Hips);

            SkinnedMeshRenderer skinned = animator.GetComponentInChildren<SkinnedMeshRenderer>();
            return skinned != null ? skinned.rootBone : null;
        }
    }
}
