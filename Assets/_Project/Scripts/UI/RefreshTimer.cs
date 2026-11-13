using UnityEngine;

namespace GuardVsMannequin.UI
{
    /// <summary>문자열 조합 가비지를 줄이도록 화면 문구를 일정 간격으로만 다시 만들게 하는 타이머.</summary>
    public struct RefreshTimer
    {
        /// <summary>UI 문구 갱신에 쓰는 기본 간격(초).</summary>
        public const float DefaultInterval = 0.2f;

        private float nextTime;

        /// <summary>if 한 줄로 쓰도록 확인과 다음 예약을 한 번에 하고, 일시 정지 중에도 멈추지 않게 실제 시간으로 잰다.</summary>
        public bool Tick(float interval = DefaultInterval)
        {
            float now = Time.unscaledTime;
            if (now < nextTime)
                return false;
            nextTime = now + interval;
            return true;
        }

        /// <summary>
        /// 값이 바뀐 것을 알았을 때 간격을 기다리면 화면이 늦게 바뀌므로 다음 확인에서 바로 갱신되게 한다.
        /// </summary>
        public void Reset()
        {
            nextTime = 0f;
        }
    }
}
