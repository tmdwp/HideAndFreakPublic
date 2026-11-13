using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>1인칭 카메라가 붙을 눈 위치를 정하는 구성 요소.</summary>
    public interface IEyePointProvider
    {
        /// <summary>
        /// 머리 뼈가 없는 외형도 있으므로, 구할 수 없으면 false를 돌려 고정된 추적 대상을 쓰게 한다.
        /// </summary>
        bool TryGetEyePosition(out Vector3 eyePosition);
    }

    /// <summary>숙이거나 기어가면 카메라도 따라가도록 머리 뼈 위치를 1인칭 눈 위치로 쓴다.</summary>
    [RequireComponent(typeof(CharacterSkinView))]
    public class SkinEyePoint : MonoBehaviour, IEyePointProvider
    {
        // 외형 비율로 눈 위치 보정을 맞추는 기준 골반~머리 거리.
        private const float HumanHipsToHead = 0.63f;

        [Tooltip("머리 뼈에서 눈까지 몸 기준 위쪽 거리(m, 사람 기준).")]
        [SerializeField] private float eyeUp = 0.1f;

        [Tooltip("머리 뼈에서 눈까지 몸 기준 앞쪽 거리(m, 사람 기준).")]
        [SerializeField] private float eyeForward = 0.1f;

        [Tooltip("머리 위치를 따라가는 빠르기. (클수록 즉시, 작을수록 흔들림이 줄어듦)")]
        [SerializeField] private float followSharpness = 14f;

        private CharacterSkinView skinView;
        private Animator cachedForAnimator;
        private Transform head;
        private float sizeScale = 1f;
        private Vector3 smoothedLocal;
        private bool hasSmoothed;

        /// <summary>
        /// 매 프레임 GetComponent를 부르지 않도록 미리 찾아 둔다.
        /// </summary>
        private void Awake()
        {
            skinView = GetComponent<CharacterSkinView>();
        }

        /// <inheritdoc />
        public bool TryGetEyePosition(out Vector3 eyePosition)
        {
            eyePosition = default;
            if (!RefreshHead())
                return false;

            // 몸 기준 좌표로 부드럽게 따라가 머리의 짧은 흔들림만 줄이고, 몸의 이동·회전은 그대로 따른다.
            Vector3 eyeWorld = head.position + (transform.up * eyeUp + transform.forward * eyeForward) * sizeScale;
            Vector3 targetLocal = transform.InverseTransformPoint(eyeWorld);
            smoothedLocal = hasSmoothed
                ? Vector3.Lerp(smoothedLocal, targetLocal, 1f - Mathf.Exp(-followSharpness * Time.deltaTime))
                : targetLocal;
            hasSmoothed = true;
            eyePosition = transform.TransformPoint(smoothedLocal);
            return true;
        }

        /// <summary>외형이 바뀌면 머리 뼈와 키가 달라지므로 다시 찾고 따라가기를 초기화한다.</summary>
        private bool RefreshHead()
        {
            Animator animator = skinView.Animator;
            if (animator != cachedForAnimator)
            {
                cachedForAnimator = animator;
                head = SkinBones.Find(animator, HumanBodyBones.Head);
                Transform hips = SkinBones.Find(animator, HumanBodyBones.Hips);
                sizeScale = head != null && hips != null ? Vector3.Distance(hips.position, head.position) / HumanHipsToHead : 1f;
                hasSmoothed = false;
            }
            return head != null;
        }
    }
}
