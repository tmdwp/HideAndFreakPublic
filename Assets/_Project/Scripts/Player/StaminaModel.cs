using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>입력·MonoBehaviour 없이 시험하도록 스태미나 계산만 맡는 순수 로직.</summary>
    public sealed class StaminaModel
    {
        private float recoverDelayRemaining;

        /// <summary>0~1 비율.</summary>
        public float Current { get; private set; } = 1f;

        /// <summary>바닥난 뒤 조금만 차도 바로 달리면 끊기듯 반복되므로, 다시 달릴 수 없는 구간을 따로 표시한다.</summary>
        public bool IsExhausted { get; private set; }

        /// <summary>
        /// 입력과 떨어진 순수 로직으로 두어야 테스트와 재사용이 쉬워서, 판단에 필요한 값만 받아 한 프레임 진행한다.
        /// </summary>
        /// <param name="wantsSprint">달리기 키를 누른 채 움직이고 있는지.</param>
        /// <returns>소진 상태면 키를 눌러도 달리지 않으므로 실제로 달리는지를 돌려준다.</returns>
        /// <summary>
        /// 도약처럼 한 번에 모두 쓰는 동작이 있어, 바닥내고 소진 상태로 만든다.
        /// 바로 회복이 시작되면 곧장 다시 쓸 수 있으므로 회복 대기도 처음부터 다시 센다.
        /// </summary>
        public void Deplete()
        {
            Current = 0f;
            IsExhausted = true;
            recoverDelayRemaining = SprintRules.RecoverDelay;
        }

        public bool Tick(bool wantsSprint, float deltaTime)
        {
            return Tick(wantsSprint, SprintRules.DrainPerSecond, deltaTime);
        }

        /// <summary>달리기 말고도 스태미나를 쓰는 동작(구울 은신)이 있어, 쓰는 속도를 받아 한 프레임 진행한다.</summary>
        /// <param name="wantsDrain">스태미나를 쓰는 동작을 하려는지.</param>
        /// <param name="drainPerSecond">초당 쓰는 비율(0~1).</param>
        /// <returns>소진 상태면 쓰지 못하므로 실제로 쓰고 있는지를 돌려준다.</returns>
        public bool Tick(bool wantsDrain, float drainPerSecond, float deltaTime)
        {
            if (IsExhausted && Current >= SprintRules.RestartRatio)
                IsExhausted = false;

            bool isSprinting = wantsDrain && !IsExhausted && Current > 0f;
            if (isSprinting)
            {
                Current = Mathf.Max(0f, Current - drainPerSecond * deltaTime);
                recoverDelayRemaining = SprintRules.RecoverDelay;
                if (Current <= 0f)
                    IsExhausted = true;
            }
            else if (recoverDelayRemaining > 0f)
            {
                // 잠깐 멈췄다 다시 달리는 것으로 회복을 챙기지 못하게 멈춘 직후에는 바로 채우지 않는다.
                recoverDelayRemaining -= deltaTime;
            }
            else
            {
                Current = Mathf.Min(1f, Current + SprintRules.RecoverPerSecond * deltaTime);
            }

            return isSprinting;
        }
    }
}
