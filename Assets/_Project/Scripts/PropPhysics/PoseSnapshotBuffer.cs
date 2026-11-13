using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.PropPhysics
{
    /// <summary>클라이언트 화면이 부드럽도록 서버 위치 기록을 모아 원하는 시각의 자세를 보간해 꺼낸다.</summary>
    public sealed class PoseSnapshotBuffer
    {
        // 보간에는 앞뒤 몇 개만 있으면 되므로 메모리가 늘지 않도록 상한을 둔다.
        private const int Capacity = 16;

        private readonly List<(double time, Pose pose)> snapshots = new(Capacity);

        /// <summary>기록이 없을 때 Sample을 부르면 예외가 나므로 먼저 확인하는 데 쓴다.</summary>
        public bool HasData => snapshots.Count > 0;

        /// <summary>멈춘 뒤 마지막 자리를 꺼낼 때 쓰는 가장 늦은 기록 시각.</summary>
        public double LatestTime => snapshots.Count > 0 ? snapshots[^1].time : double.NegativeInfinity;

        /// <summary>
        /// 비신뢰 채널은 순서가 뒤바뀔 수 있어서, 늦게 도착한 옛 기록은 시간이 거꾸로 가지 않도록 버린다.
        /// </summary>
        public void Add(double time, Pose pose)
        {
            if (snapshots.Count > 0 && time <= snapshots[^1].time)
                return;
            if (snapshots.Count == Capacity)
                snapshots.RemoveAt(0);
            snapshots.Add((time, pose));
        }

        /// <summary>전송 간격 사이에도 부드럽도록 앞뒤 기록을 보간하고, 범위 밖이면 가장 가까운 기록을 쓴다.</summary>
        public Pose Sample(double time)
        {
            if (time <= snapshots[0].time)
                return snapshots[0].pose;

            for (int i = 1; i < snapshots.Count; i++)
            {
                if (time > snapshots[i].time)
                    continue;

                (double fromTime, Pose fromPose) = snapshots[i - 1];
                (double toTime, Pose toPose) = snapshots[i];
                float ratio = (float)((time - fromTime) / (toTime - fromTime));
                return new Pose(Vector3.Lerp(fromPose.position, toPose.position, ratio), Quaternion.Slerp(fromPose.rotation, toPose.rotation, ratio));
            }
            return snapshots[^1].pose;
        }

        /// <summary>
        /// 다음에 날아갈 때 이전 기록과 섞여 엉뚱하게 보간되지 않도록 비운다.
        /// </summary>
        public void Clear()
        {
            snapshots.Clear();
        }
    }

    /// <summary>보간할 앞뒤 기록이 있도록 서버 시각을 추정해 조금 늦은 시각을 보여 준다.</summary>
    public sealed class ServerClockEstimate
    {
        /// <summary>보간할 다음 기록이 늘 있도록 전송 간격의 두 배만큼 늦게 보여 주는 시간(초).</summary>
        public const float Delay = 0.1f;

        // 지연이 순간적으로 튀어도 화면이 흔들리지 않도록 차이를 천천히 따라가는 비율
        private const float Smoothing = 0.1f;

        private double offset;
        private bool hasOffset;

        /// <summary>네트워크 지연이 매번 달라 받은 순간의 시각 차이를 부드럽게 섞어 시계 차이를 추정한다.</summary>
        public void Observe(double serverTime, double localTime)
        {
            double sampleOffset = serverTime - localTime;
            offset = hasOffset ? offset + (sampleOffset - offset) * Smoothing : sampleOffset;
            hasOffset = true;
        }

        /// <summary>
        /// 추정한 시계 차이를 더한 뒤 보간 여유만큼 늦춘 서버 시각을 준다.
        /// </summary>
        public double GetRenderTime(double localTime)
        {
            return localTime + offset - Delay;
        }
    }
}
