using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>호스트가 정한 설정에 맞춰 역할 속도와 배율을 캐릭터 이동 속도에 적용한다.</summary>
    public class MoveSpeedController : MonoBehaviour
    {
        [Tooltip("속도 설정을 고를 역할. (Guard 또는 Mannequin)")]
        [SerializeField] private PlayerRole role = PlayerRole.Guard;

        private ICharacterMotor motor;
        private IMoveSpeedModifier[] modifiers;
        private float appliedSpeed = -1f;

        /// <summary>
        /// 매 프레임 GetComponent를 부르지 않도록 이동 구현과 배율 제공자를 미리 찾아 둔다.
        /// </summary>
        private void Awake()
        {
            motor = GetComponent<ICharacterMotor>();
            modifiers = GetComponents<IMoveSpeedModifier>();
        }

        /// <summary>속도가 바뀌는 계기가 여러 곳이라 매 프레임 계산하되 값이 같으면 건드리지 않는다.</summary>
        private void Update()
        {
            IGameStateProvider state = GameServices.State;
            if (state == null || motor == null)
                return;

            float baseSpeed = RoleDefinitions.Get(role).GetMoveSpeed(state.Settings, state.CurrentState);
            // 설정이 아직 동기화되지 않았으면(0) 멈춰 버리지 않도록 프리팹 값을 유지한다.
            if (baseSpeed <= 0f)
                return;

            float targetSpeed = baseSpeed * GetMultiplier();
            if (Mathf.Approximately(targetSpeed, appliedSpeed))
                return;

            appliedSpeed = targetSpeed;
            motor.MaxWalkSpeed = targetSpeed;
            motor.MaxFlySpeed = targetSpeed;
        }

        /// <summary>
        /// 달리기와 감속이 동시에 걸릴 수 있어서 모든 배율을 곱해 합친다.
        /// </summary>
        private float GetMultiplier()
        {
            float multiplier = 1f;
            foreach (IMoveSpeedModifier modifier in modifiers)
                multiplier *= modifier.SpeedMultiplier;
            return multiplier;
        }
    }
}
