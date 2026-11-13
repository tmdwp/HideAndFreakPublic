using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>즉시 반응과 서버 검증이 각자 시간을 재되, 지연으로 일찍 도착한 요청은 허용 오차로 받는다.</summary>
    public sealed class ToolCooldown
    {
        /// <summary>쿨타임 절반을 넘지 않는 허용 오차(초).</summary>
        public const float Tolerance = 0.15f;

        private float localEnd;
        private float localDuration;
        private float serverReadyTime;

        /// <summary>단위는 초.</summary>
        public float LocalRemaining => Mathf.Max(0f, localEnd - Time.time);

        /// <summary>HUD가 쿨타임 길이를 몰라도 그리도록 0~1로 주는 진행도.</summary>
        public float LocalProgress => localDuration > 0f ? Mathf.Clamp01(1f - LocalRemaining / localDuration) : 1f;

        /// <summary>지연을 고려한 허용 오차가 이미 반영돼 있다.</summary>
        public bool IsServerReady => Time.time >= serverReadyTime;

        /// <summary>
        /// 서버 응답을 기다리지 않고 바로 HUD에 쿨타임이 보이도록 소유자 화면에서 먼저 시작한다.
        /// </summary>
        public void StartLocal(float duration)
        {
            localEnd = Time.time + duration;
            localDuration = duration;
        }

        /// <summary>
        /// 장전처럼 사용 여부와 관계없이 쿨타임을 걸어야 하는 경우가 있어서 검사 없이 시작한다.
        /// </summary>
        public void StartServer(float duration)
        {
            serverReadyTime = Time.time + duration - Mathf.Min(Tolerance, duration * 0.5f);
        }

        /// <summary>검사와 시작 사이에 다른 요청이 끼어들지 않도록 한 번에 처리한다.</summary>
        public bool TryStartServer(float duration)
        {
            if (!IsServerReady)
                return false;
            StartServer(duration);
            return true;
        }
    }
}
