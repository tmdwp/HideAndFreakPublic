using System.Collections.Generic;

namespace GuardVsMannequin.Room
{
    /// <summary>느리지만 살아 있는 클라이언트를 구분하도록 "아직 불러오는 중" 알림 시각을 기록한다.</summary>
    public sealed class LoadingReportTracker
    {
        private readonly Dictionary<int, float> lastReportTimes = new();
        private readonly float timeout;

        /// <summary>알림이 끊긴 사람을 멈춘 것으로 보기까지의 시간(초)을 받는다.</summary>
        public LoadingReportTracker(float timeout)
        {
            this.timeout = timeout;
        }

        /// <summary>
        /// 마지막 알림 시각만 있으면 판단할 수 있어서 시각 하나만 덮어쓴다.
        /// </summary>
        public void Record(int clientId, float now)
        {
            lastReportTimes[clientId] = now;
        }

        /// <summary>
        /// 느리지만 살아 있는 사람과 멈춘 사람을 구분하려고, 최근 인정 시간 안에 알림이 왔는지 본다.
        /// </summary>
        public bool IsLoading(int clientId, float now)
        {
            return lastReportTimes.TryGetValue(clientId, out float reportTime) && now - reportTime <= timeout;
        }

        /// <summary>
        /// 나간 사람의 기록이 남아 같은 ID로 들어온 사람에게 잘못 적용되지 않도록 지운다.
        /// </summary>
        public void Remove(int clientId)
        {
            lastReportTimes.Remove(clientId);
        }

        /// <summary>
        /// 지난 전환의 알림이 새 전환에서 로딩 중으로 잘못 인정되지 않도록 모두 지운다.
        /// </summary>
        public void Clear()
        {
            lastReportTimes.Clear();
        }
    }
}
