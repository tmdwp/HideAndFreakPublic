using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 이번 판 게임 설정(룸에서 호스트가 정한 값)에 맞춰 캐릭터 이동 속도를 적용한다.
    /// 역할별 속도와 사냥 시간 배율은 <see cref="RoleDefinitions"/>가 정한다. (마네킹은 사냥 시간에 느려짐)
    /// 같은 오브젝트의 <see cref="IMoveSpeedModifier"/>(달리기 등) 배율을 곱한다.
    /// 벽타기(Flying 모드)도 걷기와 같은 속도를 쓴다.
    /// 이동은 소유자 권한이지만 설정은 모든 피어에 동기화되므로 어디서 실행해도 같은 값이 된다.
    /// (달리기 배율은 소유자에서만 1보다 커지며, 서버는 <see cref="MovementValidator"/>가 달리기 거리를 따로 허용한다)
    /// </summary>
    public class MoveSpeedController : MonoBehaviour
    {
        [Tooltip("속도 설정을 고를 역할. (Guard 또는 Mannequin)")]
        [SerializeField] private PlayerRole role = PlayerRole.Guard;

        private ICharacterMotor motor;
        private IMoveSpeedModifier[] modifiers;
        private float appliedSpeed = -1f;

        /// <summary>
        /// 캐릭터 이동 구현과 속도 배율 제공자를 캐시한다.
        /// </summary>
        private void Awake()
        {
            motor = GetComponent<ICharacterMotor>();
            modifiers = GetComponents<IMoveSpeedModifier>();
        }

        /// <summary>
        /// 목표 속도가 바뀌었을 때만 캐릭터에 적용한다. (설정 동기화, 사냥 시간 진입, 달리기)
        /// </summary>
        private void Update()
        {
            IGameStateProvider state = GameServices.State;
            if (state == null || motor == null)
                return;

            float baseSpeed = RoleDefinitions.Get(role).GetMoveSpeed(state.Settings, state.CurrentState);
            // 설정이 아직 동기화되지 않았으면(0) 프리팹 값을 유지한다.
            if (baseSpeed <= 0f)
                return;

            float speed = baseSpeed * GetMultiplier();
            if (Mathf.Approximately(speed, appliedSpeed))
                return;

            appliedSpeed = speed;
            motor.MaxWalkSpeed = speed;
            motor.MaxFlySpeed = speed;
        }

        /// <summary>
        /// 모든 속도 배율 제공자의 배율을 곱한다.
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
