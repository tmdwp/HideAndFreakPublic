using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.PropPhysics
{
    /// <summary>
    /// 서버가 보낸 위치·회전 기록을 서버 시각 순으로 모아 두고, 원하는 시각의 자세를 보간해 꺼낸다. (클라이언트 화면용)
    /// 날아가는 사물과 변신한 마네킹의 사물 모드가 함께 쓴다.
    /// </summary>
    public sealed class PoseSnapshotBuffer
    {
        private const int Capacity = 16;

        private readonly List<(double time, Pose pose)> snapshots = new(Capacity);

        /// <summary>기록이 하나라도 있는지.</summary>
        public bool HasData => snapshots.Count > 0;

        /// <summary>가장 늦은 기록의 서버 시각. 기록이 없으면 음의 무한대.</summary>
        public double LatestTime => snapshots.Count > 0 ? snapshots[^1].time : double.NegativeInfinity;

        /// <summary>
        /// 기록을 더한다. 늦게 도착한 옛 기록(비신뢰 채널 순서 뒤바뀜)은 버린다.
        /// </summary>
        public void Add(double time, Pose pose)
        {
            if (snapshots.Count > 0 && time <= snapshots[^1].time)
                return;
            if (snapshots.Count == Capacity)
                snapshots.RemoveAt(0);
            snapshots.Add((time, pose));
        }

        /// <summary>
        /// 서버 시각 <paramref name="time"/>의 자세. 기록 범위 밖이면 가장 가까운 끝 기록을 쓴다.
        /// </summary>
        public Pose Sample(double time)
        {
            if (time <= snapshots[0].time)
                return snapshots[0].pose;

            for (int i = 1; i < snapshots.Count; i++)
            {
                if (time > snapshots[i].time)
                    continue;

                (double fromTime, Pose from) = snapshots[i - 1];
                (double toTime, Pose to) = snapshots[i];
                float t = (float)((time - fromTime) / (toTime - fromTime));
                return new Pose(Vector3.Lerp(from.position, to.position, t), Quaternion.Slerp(from.rotation, to.rotation, t));
            }
            return snapshots[^1].pose;
        }

        /// <summary>
        /// 기록을 모두 지운다.
        /// </summary>
        public void Clear()
        {
            snapshots.Clear();
        }
    }

    /// <summary>
    /// 클라이언트에서 "지금 보여줄 서버 시각"을 추정한다. 받은 기록의 서버 시각과 받은 순간의 로컬 시각 차이를 부드럽게 따라가고,
    /// 보간할 앞뒤 기록이 있도록 조금 늦은 시각(<see cref="Delay"/>)을 보여준다.
    /// </summary>
    public sealed class ServerClockEstimate
    {
        /// <summary>보여줄 시각을 늦추는 정도(초). 전송 간격(초당 20회 = 0.05초)의 두 배.</summary>
        public const float Delay = 0.1f;

        private const float Smoothing = 0.1f;

        private double offset;
        private bool hasOffset;

        /// <summary>
        /// 서버 시각 <paramref name="serverTime"/>의 기록을 로컬 시각 <paramref name="localTime"/>에 받았음을 반영한다.
        /// </summary>
        public void Observe(double serverTime, double localTime)
        {
            double sample = serverTime - localTime;
            offset = hasOffset ? offset + (sample - offset) * Smoothing : sample;
            hasOffset = true;
        }

        /// <summary>
        /// 로컬 시각 <paramref name="localTime"/>에 보여줄 서버 시각.
        /// </summary>
        public double GetRenderTime(double localTime)
        {
            return localTime + offset - Delay;
        }
    }
}
