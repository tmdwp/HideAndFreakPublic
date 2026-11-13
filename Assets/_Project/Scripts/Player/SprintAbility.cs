using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>달리기 키를 누른 채 움직이면 스태미나를 쓰며 빨리 달리게 한다.</summary>
    public class SprintAbility : MonoBehaviour, IStaminaSource, IMoveSpeedModifier
    {
        // 제자리에서 Shift만 눌러도 스태미나가 닳지 않게 하는 이동 입력 하한
        private const float MinMoveInput = 0.01f;

        private readonly StaminaModel stamina = new();
        private IPlayerInput playerInput;
        private bool isSprinting;

        /// <inheritdoc />
        public float Stamina => stamina.Current;

        /// <inheritdoc />
        public bool IsExhausted => stamina.IsExhausted;

        /// <inheritdoc />
        public float SpeedMultiplier => isSprinting && isActiveAndEnabled ? SprintRules.SpeedMultiplier : 1f;

        /// <summary>
        /// 매 프레임 GetComponent를 부르지 않도록 입력 구현을 미리 찾아 둔다.
        /// </summary>
        private void Awake()
        {
            playerInput = GetComponent<IPlayerInput>();
        }

        /// <summary>
        /// 다시 켜졌을 때 달리던 상태가 남아 있으면 키를 떼도 빨리 달리므로 초기화한다.
        /// </summary>
        private void OnDisable()
        {
            isSprinting = false;
        }

        /// <summary>
        /// 실제로 움직일 때만 달리기로 보도록 이동 입력도 함께 확인한다.
        /// </summary>
        private void Update()
        {
            bool wantsSprint = playerInput != null && playerInput.IsSprintHeld && playerInput.Move.sqrMagnitude > MinMoveInput;
            isSprinting = stamina.Tick(wantsSprint, Time.deltaTime);
        }
    }
}
