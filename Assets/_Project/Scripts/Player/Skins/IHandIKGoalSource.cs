using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>값이 없는 항목은 IK를 쓰지 않는 조준 IK의 손·팔꿈치 목표.</summary>
    public struct HandIKGoals
    {
        public Vector3 RightHand;
        public Vector3? LeftHand;

        /// <summary>총에 손을 고정할 때만 주는 오른손 방향.</summary>
        public Quaternion? RightHandRotation;

        /// <summary>총에 손을 고정할 때만 주는 왼손 방향.</summary>
        public Quaternion? LeftHandRotation;

        /// <summary>팔이 몸 안쪽으로 꺾이지 않도록 팔꿈치가 향할 위치를 준다.</summary>
        public Vector3? RightElbowHint;

        /// <summary>팔이 몸 안쪽으로 꺾이지 않도록 팔꿈치가 향할 위치를 준다.</summary>
        public Vector3? LeftElbowHint;
    }

    /// <summary>손 방향을 기억할 때 쓰는 애니메이션 그대로의 손 IK 목표 방향.</summary>
    public struct HandIKPose
    {
        public Quaternion RightHandRotation;
        public Quaternion LeftHandRotation;
    }

    /// <summary>조준 IK에 손 목표 위치를 알려 주는 쪽.</summary>
    public interface IHandIKGoalSource
    {
        /// <summary>
        /// 한 손 총처럼 목표를 정할 필요가 없으면 false를 돌려 IK가 기본 오른손 위치를 쓰게 한다.
        /// </summary>
        /// <param name="animatedPose">IK를 적용하기 전 애니메이션 그대로의 손 목표 방향.</param>
        bool TryGetHandGoals(HandIKPose animatedPose, out HandIKGoals goals);
    }
}
