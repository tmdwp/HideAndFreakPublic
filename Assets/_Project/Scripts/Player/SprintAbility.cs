using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>달리기 키를 누른 채 움직이면 스태미나를 쓰며 빨리 달리게 한다.</summary>
    public class SprintAbility : MonoBehaviour, IStaminaSource, IMoveSpeedModifier, IStaminaDepleter
    {
        // 제자리에서 Shift만 눌러도 스태미나가 닳지 않게 하는 이동 입력 하한
        private const float MinMoveInput = 0.01f;

        private readonly StaminaModel stamina = new();
        private IPlayerInput playerInput;
        private ISprintBlocker[] blockers;
        private IStaminaDrain[] drains;
        private bool isSprinting;

        /// <inheritdoc />
        public float Stamina => stamina.Current;

        /// <inheritdoc />
        public bool IsExhausted => stamina.IsExhausted;

        /// <inheritdoc />
        public float SpeedMultiplier => isSprinting && isActiveAndEnabled ? SprintRules.SpeedMultiplier : 1f;

        /// <summary>
        /// 매 프레임 GetComponent를 부르지 않도록 입력 구현, 달리기를 막는 쪽, 같은 스태미나를 쓰는 쪽을 미리 찾아 둔다.
        /// </summary>
        private void Awake()
        {
            playerInput = GetComponent<IPlayerInput>();
            blockers = GetComponents<ISprintBlocker>();
            drains = GetComponents<IStaminaDrain>();
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
            bool wantsSprint = playerInput != null && playerInput.IsSprintHeld && playerInput.Move.sqrMagnitude > MinMoveInput && !IsBlocked();
            float otherDrain = GetOtherDrainPerSecond();
            float drainPerSecond = (wantsSprint ? SprintRules.DrainPerSecond : 0f) + otherDrain;
            bool isDraining = stamina.Tick(wantsSprint || otherDrain > 0f, drainPerSecond, Time.deltaTime);
            isSprinting = wantsSprint && isDraining;
        }

        /// <inheritdoc />
        public void DepleteStamina()
        {
            stamina.Deplete();
            isSprinting = false;
        }

        /// <summary>은신처럼 달리기가 아닌 동작도 같은 스태미나를 쓰므로 그 속도를 합친다.</summary>
        private float GetOtherDrainPerSecond()
        {
            float total = 0f;
            foreach (IStaminaDrain drain in drains)
                total += Mathf.Max(0f, drain.StaminaDrainPerSecond);
            return total;
        }

        /// <summary>Shift가 다른 동작(구울 은신)에 쓰이는 동안 스태미나가 닳거나 빨라지지 않게 한다.</summary>
        private bool IsBlocked()
        {
            foreach (ISprintBlocker blocker in blockers)
            {
                if (blocker.IsSprintBlocked)
                    return true;
            }
            return false;
        }
    }
}
