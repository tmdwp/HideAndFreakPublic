using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>휴머노이드와 Generic 리그 모두에서 몸의 뼈를 찾는다.</summary>
    public static class SkinBones
    {
        /// <summary>호출하는 쪽이 리그 종류를 몰라도 되도록 아바타 또는 이름으로 찾는다.</summary>
        public static Transform Find(Animator animator, HumanBodyBones bone)
        {
            if (animator == null)
                return null;
            return animator.isHuman ? animator.GetBoneTransform(bone) : FindByName(animator.transform, bone.ToString());
        }

        /// <summary>Generic 리그는 아바타 연결이 없어 휴머노이드 뼈와 같은 이름의 자식을 찾는다.</summary>
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
