using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>원격 아바타에서도 걸음이 보이도록 입력 대신 위치 변화로 애니메이터 속도를 정한다.</summary>
    public sealed class LocomotionAnimatorDriver
    {
        /// <summary>네트워크 끊김을 가리면서 멈춤에는 빨리 반응하도록 정한 따라가기 세기.</summary>
        private const float VelocitySmoothing = 12f;

        private readonly AnimatorParameterSet parameters;
        private readonly Transform body;
        private Vector3 lastPosition;
        private Vector3 smoothedVelocity;

        /// <summary>첫 프레임 속도가 튀지 않도록 지금 위치를 기준으로 시작한다.</summary>
        /// <param name="body">위치를 잴 아바타 트랜스폼.</param>
        public LocomotionAnimatorDriver(AnimatorParameterSet parameters, Transform body)
        {
            this.parameters = parameters;
            this.body = body;
            lastPosition = body.position;
        }

        /// <summary>모든 피어가 같은 걸음을 보이도록 위치 변화로 속도와 방향 성분을 구한다.</summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            Vector3 currentPosition = body.position;
            Vector3 velocity = (currentPosition - lastPosition) / deltaTime;
            lastPosition = currentPosition;
            velocity.y = 0f;

            // 네트워크로 받은 위치는 프레임마다 고르지 않으므로 부드럽게 만든다.
            smoothedVelocity = Vector3.Lerp(smoothedVelocity, velocity, 1f - Mathf.Exp(-VelocitySmoothing * deltaTime));
            Vector3 localVelocity = body.InverseTransformDirection(smoothedVelocity);

            parameters.SetFloatIfExists(CharacterAnimatorParameters.Speed, smoothedVelocity.magnitude);
            parameters.SetFloatIfExists(CharacterAnimatorParameters.MoveX, localVelocity.x);
            parameters.SetFloatIfExists(CharacterAnimatorParameters.MoveZ, localVelocity.z);
        }
    }
}
