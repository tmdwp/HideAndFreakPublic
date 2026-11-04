using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 외형 애니메이터에서 몸의 뼈를 찾는다.
    /// 휴머노이드는 아바타 연결로, 아니면(구울 같은 Generic 리그) 휴머노이드 뼈와 같은 이름(LeftUpperArm, Head 등)의 트랜스폼으로 찾는다.
    /// (Blender로 만든 캐릭터는 리그 종류와 관계없이 이 이름 규칙을 쓴다)
    /// </summary>
    public static class SkinBones
    {
        /// <summary>
        /// 뼈를 찾는다. 없으면 null.
        /// </summary>
        public static Transform Find(Animator animator, HumanBodyBones bone)
        {
            if (animator == null)
                return null;
            return animator.isHuman ? animator.GetBoneTransform(bone) : FindByName(animator.transform, bone.ToString());
        }

        /// <summary>
        /// 자식 중 이름이 같은 트랜스폼을 찾는다. 없으면 null.
        /// </summary>
        private static Transform FindByName(Transform root, string boneName)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == boneName)
                    return child;
            }
            return null;
        }
    }
}
