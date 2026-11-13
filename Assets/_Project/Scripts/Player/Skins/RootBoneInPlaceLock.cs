using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>이동은 캐릭터 코드가 맡으므로 Generic 모델의 이동 애니메이션을 제자리로 고정한다.</summary>
    [RequireComponent(typeof(Animator))]
    public class RootBoneInPlaceLock : MonoBehaviour
    {
        private Transform rootBone;
        private Vector3 restLocalPosition;

        /// <summary>
        /// 이후 매 프레임 얼마나 밀려났는지 알려면 기준이 필요해서, 기본 자세의 뿌리 뼈 위치(애니메이터 기준 좌표)를 저장한다.
        /// </summary>
        private void Awake()
        {
            SkinnedMeshRenderer skinned = GetComponentInChildren<SkinnedMeshRenderer>(true);
            rootBone = skinned != null ? skinned.rootBone : null;
            if (rootBone == null || rootBone == transform)
            {
                // 뿌리 뼈가 없거나 애니메이터 자신이면 되돌릴 기준이 없으므로 끈다.
                enabled = false;
                return;
            }
            restLocalPosition = transform.InverseTransformPoint(rootBone.position);
        }

        /// <summary>애니메이션이 몸을 앞으로 미는 몫만 지우고, 걸음의 튀는 느낌은 남기려고 높이는 그대로 둔다.</summary>
        private void LateUpdate()
        {
            Vector3 currentLocalPosition = transform.InverseTransformPoint(rootBone.position);
            Vector3 drift = currentLocalPosition - restLocalPosition;
            drift.y = 0f;
            if (drift.sqrMagnitude > 0f)
                rootBone.position -= transform.TransformVector(drift);
        }
    }
}
