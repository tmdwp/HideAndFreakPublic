using System.Collections.Generic;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.PropPhysics
{
    /// <summary>클라이언트에서 날아가는 사물 위치를 보간해 적용하고, 날아가는 동안에는 충돌체를 끈다.</summary>
    public sealed class PropPoseInterpolator
    {
        /// <summary>
        /// 사물마다 따로 보간해야 해서 기록과 멈춤 정보를 사물별로 묶는다.
        /// </summary>
        private sealed class Track
        {
            public PropBody body;
            public readonly PoseSnapshotBuffer buffer = new();
            // 멈춘 위치의 서버 시각(움직이는 중이면 null).
            public double? settleTime;
        }

        private readonly Dictionary<int, Track> tracks = new();
        // 늦게 도착한 옛 위치를 버리려고 기억하는 사물마다의 마지막 멈춤 시각.
        private readonly Dictionary<int, double> lastSettleTimes = new();
        private readonly ServerClockEstimate clock = new();
        // 반복 중에 사전을 고칠 수 없어서 끝난 번호를 모아 두는 버퍼
        private readonly List<int> finished = new();

        /// <summary>멈춘 사물이 다시 움직이는 것처럼 보이지 않도록 멈춤 알림보다 옛 위치는 버린다.</summary>
        public void AddMoving(double serverTime, double localTime, PropPoseData[] poses)
        {
            clock.Observe(serverTime, localTime);
            foreach (PropPoseData pose in poses)
            {
                if (lastSettleTimes.TryGetValue(pose.index, out double settledTime) && serverTime <= settledTime)
                    continue;

                Track track = GetOrCreateTrack(pose.index);
                if (track == null)
                    continue;
                track.settleTime = null;
                track.buffer.Add(serverTime, new Pose(pose.position, pose.rotation));
            }
        }

        /// <summary>
        /// 멈춘 자리로 바로 순간이동하면 끊겨 보이므로, 기록에 더해 두고 보간 시각이 그 위치에 이르면 적용을 끝낸다.
        /// </summary>
        public void AddSettled(double serverTime, PropPoseData[] poses)
        {
            foreach (PropPoseData pose in poses)
            {
                lastSettleTimes[pose.index] = serverTime;
                Track track = GetOrCreateTrack(pose.index);
                if (track == null)
                    continue;
                track.buffer.Add(serverTime, new Pose(pose.position, pose.rotation));
                track.settleTime = serverTime;
            }
        }

        /// <summary>서로 부딪히는 장면이 맞도록 모든 사물을 한 서버 시각으로 적용한다.</summary>
        public void Apply(double localTime)
        {
            if (tracks.Count == 0)
                return;

            double renderTime = clock.GetRenderTime(localTime);
            finished.Clear();
            foreach (KeyValuePair<int, Track> pair in tracks)
            {
                Track track = pair.Value;
                if (track.body == null)
                {
                    finished.Add(pair.Key);
                    continue;
                }

                bool isDone = track.settleTime.HasValue && renderTime >= track.settleTime.Value;
                Pose pose = track.buffer.Sample(isDone ? track.buffer.LatestTime : renderTime);
                track.body.SetPose(pose.position, pose.rotation);
                if (isDone)
                {
                    track.body.SetCollidersEnabled(true);
                    finished.Add(pair.Key);
                }
            }

            foreach (int index in finished)
                tracks.Remove(index);
        }

        /// <summary>
        /// 판이 끝나거나 접속이 끊긴 뒤 충돌체가 꺼진 채 남아 사물을 통과하지 않도록 모두 다시 켜고 기록을 지운다.
        /// </summary>
        public void Clear()
        {
            foreach (Track track in tracks.Values)
            {
                if (track.body != null)
                    track.body.SetCollidersEnabled(true);
            }
            tracks.Clear();
            lastSettleTimes.Clear();
        }

        /// <summary>늦게 도착하는 위치에 플레이어가 밀리거나 끼지 않도록 움직이기 시작하면 충돌체를 끈다.</summary>
        private Track GetOrCreateTrack(int index)
        {
            if (tracks.TryGetValue(index, out Track track))
                return track;
            if (!PropRegistry.TryGet(index, out TransformableProp prop) || !prop.TryGetComponent(out PropBody body))
                return null;

            track = new Track { body = body };
            body.SetCollidersEnabled(false);
            tracks[index] = track;
            return track;
        }
    }

    /// <summary>네트워크로 보내는 사물 자세 한 건.</summary>
    public struct PropPoseData
    {
        public int index;
        public Vector3 position;
        public Quaternion rotation;
    }
}
