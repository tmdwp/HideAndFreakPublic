using ECM2;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 1인칭 좌우 시점이 끊겨 보이지 않게 한다.
    /// ECM2는 좌우 입력(AddYawInput)을 모아 두었다가 물리 단계(Fixed Timestep, 50Hz)에서만 몸을 돌리므로,
    /// 몸의 회전을 그대로 카메라에 쓰면 화면이 물리 주기로 끊겨 돈다.
    /// 아직 몸에 반영되지 않은 입력을 따로 더해, 카메라는 매 프레임 최신 입력 방향을 보게 한다.
    /// (사용: 입력 때 <see cref="Add"/>, 캐릭터의 ConsumeRotationInput에서 <see cref="Consume"/>, 카메라 갱신 때 <see cref="ViewYaw"/>)
    /// </summary>
    public class PendingLookYaw
    {
        private float pendingYaw;

        /// <summary>시점 입력을 받은 적이 있는지. (로컬 소유자만 입력을 받는다)</summary>
        public bool HasInput { get; private set; }

        /// <summary>
        /// 몸에 아직 반영되지 않은 좌우 입력을 더한다.
        /// </summary>
        public void Add(float yaw)
        {
            pendingYaw += yaw;
            HasInput = true;
        }

        /// <summary>
        /// 물리 단계에서 입력이 몸에 반영되었을 때 부른다.
        /// </summary>
        public void Consume()
        {
            pendingYaw = 0f;
        }

        /// <summary>
        /// 카메라가 향할 좌우 각도(도). 물리 상태의 몸 회전 + 아직 반영되지 않은 입력.
        /// 화면용 보간 회전(transform)이 아니라 물리 상태(rigidbody)를 기준으로 해 입력이 지연 없이 보인다.
        /// </summary>
        public float ViewYaw(Character character)
        {
            // 일시 정지는 입력을 반영하지 않고 버리므로 함께 비운다.
            if (character.IsPaused())
                pendingYaw = 0f;

            Rigidbody body = character.characterMovement != null ? character.characterMovement.rigidbody : null;
            Quaternion rotation = body != null ? body.rotation : character.transform.rotation;
            return rotation.eulerAngles.y + pendingYaw;
        }
    }
}
