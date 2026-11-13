using ECM2;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>ECM2가 물리 주기로만 몸을 돌려 화면이 끊겨 보이므로 아직 반영되지 않은 좌우 입력을 더해 준다.</summary>
    public class PendingLookYaw
    {
        private float pendingYaw;

        /// <summary>원격 아바타는 입력이 없어 몸 회전만 따라야 하므로, 입력을 받은 적이 있는지로 구분한다.</summary>
        public bool HasInput { get; private set; }

        /// <summary>
        /// 몸은 물리 단계에서야 돌므로 그때까지 카메라가 쓸 수 있게 입력을 쌓아 둔다.
        /// </summary>
        public void Add(float yaw)
        {
            pendingYaw += yaw;
            HasInput = true;
        }

        /// <summary>
        /// 몸에 반영된 입력을 다시 더하면 카메라가 두 배로 돌아가므로 반영된 순간 비운다.
        /// </summary>
        public void Consume()
        {
            pendingYaw = 0f;
        }

        /// <summary>입력이 지연 없이 보이도록 물리 상태 기준으로 구한 카메라 좌우 각도(도).</summary>
        public float ViewYaw(Character character)
        {
            // 일시 정지는 입력을 반영하지 않고 버리므로 함께 비운다.
            if (character.IsPaused())
                pendingYaw = 0f;

            Rigidbody body = character.characterMovement != null ? character.characterMovement.rigidbody : null;
            Quaternion bodyRotation = body != null ? body.rotation : character.transform.rotation;
            return bodyRotation.eulerAngles.y + pendingYaw;
        }
    }
}
