using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>총을 든 가드가 조준 방향을 보도록 오른손과 머리를 조준 방향에 맞춘다.</summary>
    [RequireComponent(typeof(Animator))]
    public class HandAimIK : MonoBehaviour
    {
        // 머리가 가까운 점을 보면 사팔뜨기처럼 보여서 충분히 먼 점을 보게 한다(m).
        private const float LookDistance = 10f;

        private Animator animator;

        /// <summary>따라갈 조준 방향의 기준인 카메라 피벗.</summary>
        public Transform AimSource { get; set; }

        /// <summary>외형마다 손 위치가 달라 매 프레임 넣는 조준 기준 손 위치.</summary>
        public Vector3 HandOffset { get; set; } = new Vector3(0.18f, -0.15f, 0.45f);

        /// <summary>양손 총처럼 손 목표를 직접 정하는 쪽.</summary>
        public IHandIKGoalSource GoalSource { get; set; }

        /// <summary>
        /// IK 콜백마다 GetComponent를 부르지 않도록 미리 찾아 둔다.
        /// </summary>
        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        /// <summary>총이 조준 방향을 향하도록 IK 단계마다 손과 시선을 맞춘다.</summary>
        private void OnAnimatorIK(int layerIndex)
        {
            if (AimSource == null)
                return;

            // 이번 프레임 IK 목표는 애니메이션 자세에서 새로 시작하므로, 값을 넣기 전에 읽으면 애니메이션 그대로의 손 방향이다.
            var animatedPose = new HandIKPose
            {
                RightHandRotation = animator.GetIKRotation(AvatarIKGoal.RightHand),
                LeftHandRotation = animator.GetIKRotation(AvatarIKGoal.LeftHand)
            };
            if (GoalSource == null || !GoalSource.TryGetHandGoals(animatedPose, out HandIKGoals goals))
                goals = new HandIKGoals { RightHand = AimSource.TransformPoint(HandOffset) };

            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);
            animator.SetIKPosition(AvatarIKGoal.RightHand, goals.RightHand);
            ApplyOptional(AvatarIKGoal.LeftHand, goals.LeftHand);
            ApplyOptionalRotation(AvatarIKGoal.RightHand, goals.RightHandRotation);
            ApplyOptionalRotation(AvatarIKGoal.LeftHand, goals.LeftHandRotation);
            ApplyOptionalHint(AvatarIKHint.RightElbow, goals.RightElbowHint);
            ApplyOptionalHint(AvatarIKHint.LeftElbow, goals.LeftElbowHint);

            animator.SetLookAtWeight(1f, 0.3f, 0.8f);
            animator.SetLookAtPosition(AimSource.position + AimSource.forward * LookDistance);
        }

        /// <summary>
        /// IK 가중치는 프레임마다 남지 않으므로, 값이 없을 때는 가중치를 0으로 둬서 애니메이션 그대로 둔다.
        /// </summary>
        private void ApplyOptional(AvatarIKGoal goal, Vector3? position)
        {
            animator.SetIKPositionWeight(goal, position.HasValue ? 1f : 0f);
            if (position.HasValue)
                animator.SetIKPosition(goal, position.Value);
        }

        /// <summary>
        /// 손 방향을 고정하지 않을 때는 애니메이션 손 모양을 살리도록 가중치를 0으로 둔다.
        /// </summary>
        private void ApplyOptionalRotation(AvatarIKGoal goal, Quaternion? rotation)
        {
            animator.SetIKRotationWeight(goal, rotation.HasValue ? 1f : 0f);
            if (rotation.HasValue)
                animator.SetIKRotation(goal, rotation.Value);
        }

        /// <summary>
        /// 힌트가 없으면 Unity가 알아서 팔꿈치 방향을 정하도록 가중치를 0으로 둔다.
        /// </summary>
        private void ApplyOptionalHint(AvatarIKHint hint, Vector3? position)
        {
            animator.SetIKHintPositionWeight(hint, position.HasValue ? 1f : 0f);
            if (position.HasValue)
                animator.SetIKHintPosition(hint, position.Value);
        }
    }
}
