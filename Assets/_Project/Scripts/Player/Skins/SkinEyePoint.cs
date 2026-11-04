using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 1인칭 카메라가 붙을 눈 위치를 정하는 구성 요소. (캐릭터 이동 구현이 1인칭에서 읽는다)
    /// </summary>
    public interface IEyePointProvider
    {
        /// <summary>
        /// 지금 눈 위치(월드)를 구한다. 구할 수 없으면 false. (그때는 고정된 추적 대상을 쓴다)
        /// </summary>
        bool TryGetEyePosition(out Vector3 eyePosition);
    }

    /// <summary>
    /// 외형의 머리 뼈 위치를 1인칭 눈 위치로 쓴다. (마네킹 — 사냥 시간 구울처럼 키·자세가 바뀌는 외형)
    /// 숙이거나 기어가면 카메라도 머리를 따라 내려간다. 머리 꺾기 같은 짧은 흔들림은 부드럽게 줄인다.
    /// 머리 뼈는 목과 이어지는 머리 아래쪽이므로, 몸 기준 위·앞으로 조금 옮겨 눈 높이로 맞춘다. (외형 크기에 비례)
    /// </summary>
    [RequireComponent(typeof(CharacterSkinView))]
    public class SkinEyePoint : MonoBehaviour, IEyePointProvider
    {
        // 사람 기준(키 1.8m) 골반~머리 거리. 외형의 실제 거리와의 비로 눈 위치 보정을 맞춘다.
        private const float HumanHipsToHead = 0.63f;

        [Tooltip("머리 뼈에서 눈까지 몸 기준 위쪽 거리(m, 사람 기준).")]
        [SerializeField] private float eyeUp = 0.1f;

        [Tooltip("머리 뼈에서 눈까지 몸 기준 앞쪽 거리(m, 사람 기준).")]
        [SerializeField] private float eyeForward = 0.1f;

        [Tooltip("머리 위치를 따라가는 빠르기. (클수록 즉시, 작을수록 흔들림이 줄어듦)")]
        [SerializeField] private float followSharpness = 14f;

        private CharacterSkinView skinView;
        private Animator cachedFor;
        private Transform head;
        private float scale = 1f;
        private Vector3 smoothedLocal;
        private bool hasSmoothed;

        /// <summary>
        /// 외형을 캐시한다.
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
            Vector3 eye = head.position + (transform.up * eyeUp + transform.forward * eyeForward) * scale;
            Vector3 targetLocal = transform.InverseTransformPoint(eye);
            smoothedLocal = hasSmoothed
                ? Vector3.Lerp(smoothedLocal, targetLocal, 1f - Mathf.Exp(-followSharpness * Time.deltaTime))
                : targetLocal;
            hasSmoothed = true;
            eyePosition = transform.TransformPoint(smoothedLocal);
            return true;
        }

        /// <summary>
        /// 외형이 바뀌었으면 머리 뼈와 크기 비율을 다시 찾는다. 머리 뼈가 있으면 true.
        /// </summary>
        private bool RefreshHead()
        {
            Animator animator = skinView.Animator;
            if (animator != cachedFor)
            {
                cachedFor = animator;
                head = SkinBones.Find(animator, HumanBodyBones.Head);
                Transform hips = SkinBones.Find(animator, HumanBodyBones.Hips);
                scale = head != null && hips != null ? Vector3.Distance(hips.position, head.position) / HumanHipsToHead : 1f;
                hasSmoothed = false;
            }
            return head != null;
        }
    }
}
