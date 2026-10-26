using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 위치 변화로 이동 속도를 계산해 애니메이터의 Speed / MoveX / MoveZ에 넣는다.
    /// 입력이 아니라 위치를 쓰므로 원격 아바타에서도 동작한다.
    /// </summary>
    public sealed class LocomotionAnimatorDriver
    {
        /// <summary>속도 부드럽게 만들기 정도. (클수록 빨리 따라감)</summary>
        private const float VelocitySmoothing = 12f;

        private readonly AnimatorParameterSet parameters;
        private readonly Transform body;
        private Vector3 lastPosition;
        private Vector3 smoothedVelocity;

        /// <param name="parameters">값을 넣을 애니메이터 파라미터.</param>
        /// <param name="body">위치를 잴 아바타 트랜스폼.</param>
        public LocomotionAnimatorDriver(AnimatorParameterSet parameters, Transform body)
        {
            this.parameters = parameters;
            this.body = body;
            lastPosition = body.position;
        }

        /// <summary>
        /// 이번 프레임의 이동 속도를 애니메이터에 전달한다.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            Vector3 position = body.position;
            Vector3 velocity = (position - lastPosition) / deltaTime;
            lastPosition = position;
            velocity.y = 0f;

            // 네트워크로 받은 위치는 프레임마다 고르지 않으므로 부드럽게 만든다.
            smoothedVelocity = Vector3.Lerp(smoothedVelocity, velocity, 1f - Mathf.Exp(-VelocitySmoothing * deltaTime));
            Vector3 local = body.InverseTransformDirection(smoothedVelocity);

            parameters.SetFloatIfExists(CharacterAnimatorParameters.Speed, smoothedVelocity.magnitude);
            parameters.SetFloatIfExists(CharacterAnimatorParameters.MoveX, local.x);
            parameters.SetFloatIfExists(CharacterAnimatorParameters.MoveZ, local.z);
        }
    }
}
