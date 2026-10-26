using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 도구 쿨타임. 소유자 화면(즉시 반응·HUD 표시)과 서버(검증)가 각자 시간을 잰다.
    /// 서버는 네트워크 지연으로 요청이 조금 일찍 도착할 수 있어 <see cref="Tolerance"/>만큼 봐준다.
    /// 짧은 쿨타임(예: 손전등 0.2초)은 허용 오차가 쿨타임을 거의 없애지 않도록 쿨타임의 절반까지만 봐준다.
    /// </summary>
    public sealed class ToolCooldown
    {
        /// <summary>서버 검증 허용 오차(초). (쿨타임의 절반을 넘지 않음)</summary>
        public const float Tolerance = 0.15f;

        private float localEnd;
        private float serverReadyTime;

        /// <summary>소유자 화면의 남은 쿨타임(초).</summary>
        public float LocalRemaining => Mathf.Max(0f, localEnd - Time.time);

        /// <summary>서버에서 다시 사용할 수 있는지 여부. (허용 오차 포함)</summary>
        public bool IsServerReady => Time.time >= serverReadyTime;

        /// <summary>
        /// 소유자 화면의 쿨타임을 시작한다.
        /// </summary>
        public void StartLocal(float duration)
        {
            localEnd = Time.time + duration;
        }

        /// <summary>
        /// 서버의 쿨타임을 시작한다. (사용 가능 여부와 관계없이)
        /// </summary>
        public void StartServer(float duration)
        {
            serverReadyTime = Time.time + duration - Mathf.Min(Tolerance, duration * 0.5f);
        }

        /// <summary>
        /// 서버에서 사용할 수 있으면 쿨타임을 시작하고 true, 아직 쿨타임이면 false.
        /// </summary>
        public bool TryStartServer(float duration)
        {
            if (!IsServerReady)
                return false;
            StartServer(duration);
            return true;
        }
    }
}
