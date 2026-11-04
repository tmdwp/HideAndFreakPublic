using UnityEngine;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 화면 문구를 매 프레임이 아니라 일정 간격으로만 다시 만들게 하는 타이머.
    /// 문자열 조합(Loc.Format, 보간 문자열)은 호출마다 가비지를 남기므로, 사람이 알아채지 못할 간격(0.2초 정도)으로 줄인다.
    /// 시간 배율(일시 정지)과 관계없이 실제 시간으로 잰다. 처음 확인할 때는 바로 갱신한다.
    /// </summary>
    public struct RefreshTimer
    {
        /// <summary>UI 문구 갱신에 쓰는 기본 간격(초).</summary>
        public const float DefaultInterval = 0.2f;

        private float nextTime;

        /// <summary>
        /// 간격이 지났으면 true를 돌려주고 다음 시각을 정한다.
        /// </summary>
        public bool Tick(float interval = DefaultInterval)
        {
            float now = Time.unscaledTime;
            if (now < nextTime)
                return false;
            nextTime = now + interval;
            return true;
        }

        /// <summary>
        /// 다음 확인 때 바로 갱신하도록 한다. (값이 바뀐 것을 알았을 때)
        /// </summary>
        public void Reset()
        {
            nextTime = 0f;
        }
    }
}
