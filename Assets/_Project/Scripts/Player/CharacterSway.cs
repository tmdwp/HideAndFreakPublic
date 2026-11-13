using GuardVsMannequin.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Player
{
    /// <summary>카메라와 총이 같은 흔들림을 따라가도록 걷기 골반 출렁임을 재서 나눠 준다.</summary>
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

        [Header("상체 흔들림 (숨쉬기 등 어깨 움직임, 휴머노이드만)")]
        [Tooltip("상체 기준점이 따라오는 시간(초). 숨쉬기(3~4초 주기)가 남도록 골반보다 길게 둔다.")]
        [SerializeField] private float upperBodySmoothTime = 1.2f;

        [Tooltip("상체 흔들림 축별 배율. (좌우, 위아래, 앞뒤)")]
        [SerializeField] private Vector3 upperBodyAxisWeights = new(0.5f, 1f, 0.5f);

        [Tooltip("상체 흔들림 최대 거리(m).")]
        [SerializeField] private float upperBodyMaxOffset = 0.05f;

        [Header("확인용 (Play 중 값 보기, 수정해도 쓰지 않음)")]
        [Tooltip("지금 재고 있는 뼈. 비어 있으면 외형이 없거나 뼈를 찾지 못해 흔들림이 0이다.")]
        [SerializeField] private Transform debugBone;

        [Tooltip("이번 프레임 흔들림(월드, m). 걸을 때 수 cm씩 움직여야 정상이다.")]
        [SerializeField] private Vector3 debugWorldOffset;

        [Tooltip("이번 프레임 상체 흔들림(월드, m). 가만히 서 있을 때 숨쉬기만큼 조금씩 움직여야 정상이다.")]
        [SerializeField] private Vector3 debugUpperBodyOffset;

        private readonly BoneSwayTracker tracker = new();
        private readonly BoneSwayTracker upperBodyTracker = new();
        private Transform leftUpperArm;
        private Transform rightUpperArm;
        private Vector3 upperBodyWorldOffset;
        private CharacterSkinView skinView;
        private IPerspectiveCamera perspective;
        private Animator trackedAnimator;
        private Transform trackedBone;
        private Vector3 worldOffset;
        private int sampledFrame = -1;
        private Vector3 appliedCameraOffset;
        private bool isCameraOffsetApplied;

        /// <summary>골반 흔들림(월드 좌표, m).</summary>
        public Vector3 WorldOffset
        {
            get
            {
                SampleOncePerFrame();
                return worldOffset;
            }
        }

        /// <summary>손에 든 총이 어깨 오르내림을 따라가도록 골반 흔들림을 뺀 어깨 흔들림(월드 좌표, m).</summary>
        public Vector3 UpperBodyWorldOffset
        {
            get
            {
                SampleOncePerFrame();
                return upperBodyWorldOffset;
            }
        }

        /// <summary>카메라 앞 물체가 흔들림을 더하도록 지금 배율을 노출한다.</summary>
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
        /// 매 프레임 조회하지 않도록 외형과 시점 구현을 미리 찾아 둔다.
        /// </summary>
        private void Awake()
        {
            skinView = GetComponent<CharacterSkinView>();
            perspective = GetComponent<IPerspectiveCamera>();
        }

        /// <summary>
        /// 카메라를 실제로 옮기면 조준 광선도 흔들리므로, 그리는 동안에만 옮기려고 렌더링 전후 이벤트를 듣는다.
        /// </summary>
        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        /// <summary>
        /// 그리는 도중에 꺼지면 카메라가 옮겨진 자리에 남으므로 구독을 끊으면서 되돌린다.
        /// </summary>
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            RestoreCamera();
        }

        /// <summary>화면에만 흔들림이 보이도록 소유자 카메라를 그리기 직전에 옮긴다.</summary>
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
        /// 다음 프레임 조준·소리 계산이 흔들린 위치를 쓰지 않도록 다 그린 뒤 바로 되돌린다.
        /// </summary>
        private void OnEndCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
        {
            if (renderingCamera == viewCamera)
                RestoreCamera();
        }

        /// <summary>
        /// 옮기지 않았는데 빼면 카메라가 엉뚱한 곳으로 가므로, 옮긴 기록이 있을 때만 되돌린다.
        /// </summary>
        private void RestoreCamera()
        {
            if (!isCameraOffsetApplied)
                return;
            if (viewCamera != null)
                viewCamera.transform.position -= appliedCameraOffset;
            isCameraOffsetApplied = false;
        }

        /// <summary>카메라와 총이 다른 값을 쓰면 총이 떨리므로 한 프레임에 한 번만 잰다.</summary>
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
                bool isHuman = animator != null && animator.isHuman;
                leftUpperArm = isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null;
                rightUpperArm = isHuman ? animator.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
                tracker.Reset();
                upperBodyTracker.Reset();
            }

            if (trackedBone == null)
            {
                worldOffset = Vector3.zero;
                upperBodyWorldOffset = Vector3.zero;
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

            upperBodyWorldOffset = SampleUpperBody(modelRoot, boneLocal);
            debugUpperBodyOffset = upperBodyWorldOffset;
        }

        /// <summary>골반 흔들림과 겹치지 않도록 두 어깨 가운데를 골반 기준으로 잰다.</summary>
        private Vector3 SampleUpperBody(Transform modelRoot, Vector3 hipsLocal)
        {
            if (leftUpperArm == null || rightUpperArm == null)
                return Vector3.zero;

            Vector3 shouldersLocal = modelRoot.InverseTransformPoint((leftUpperArm.position + rightUpperArm.position) * 0.5f);
            Vector3 localOffset = upperBodyTracker.Sample(shouldersLocal - hipsLocal, Time.deltaTime, upperBodySmoothTime, upperBodyAxisWeights);
            return Vector3.ClampMagnitude(modelRoot.TransformVector(localOffset), upperBodyMaxOffset);
        }

        /// <summary>소유자 화면에서 뼈가 멈춰 흔들림이 0이 되지 않도록 소유자는 항상 뼈를 갱신한다.</summary>
        private void KeepOwnerAnimating(Animator animator)
        {
            if (viewCamera != null && viewCamera.isActiveAndEnabled && animator.cullingMode != AnimatorCullingMode.AlwaysAnimate)
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        /// <summary>걷기 출렁임이 가장 잘 드러나는 뼈를 고른다.</summary>
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
