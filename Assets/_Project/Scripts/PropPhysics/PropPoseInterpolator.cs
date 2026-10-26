using System.Collections.Generic;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.PropPhysics
{
    /// <summary>
    /// 클라이언트에서 서버가 보낸 날아가는 사물 위치를 보간해 적용한다.
    /// 날아가는 동안에는 충돌체를 꺼 두고, 멈춘 위치를 받아 그 시각까지 보여준 뒤 충돌체를 다시 켠다.
    /// </summary>
    public sealed class PropPoseInterpolator
    {
        /// <summary>
        /// 사물 하나의 기록과 멈춤 정보.
        /// </summary>
        private sealed class Track
        {
            public PropBody body;
            public readonly PoseSnapshotBuffer buffer = new();
            // 멈춘 위치의 서버 시각. 아직 움직이는 중이면 null.
            public double? settleTime;
        }

        private readonly Dictionary<int, Track> tracks = new();
        // 사물마다 마지막으로 멈춘 서버 시각. 그보다 옛 위치(늦게 도착한 비신뢰 전송)는 버린다.
        private readonly Dictionary<int, double> lastSettleTimes = new();
        private readonly ServerClockEstimate clock = new();
        private readonly List<int> finished = new();

        /// <summary>
        /// 움직이는 사물들의 위치를 받는다.
        /// </summary>
        public void AddMoving(double serverTime, double localTime, PropPoseData[] poses)
        {
            clock.Observe(serverTime, localTime);
            foreach (PropPoseData pose in poses)
            {
                if (lastSettleTimes.TryGetValue(pose.index, out double settled) && serverTime <= settled)
                    continue;

                Track track = GetOrCreateTrack(pose.index);
                if (track == null)
                    continue;
                track.settleTime = null;
                track.buffer.Add(serverTime, new Pose(pose.position, pose.rotation));
            }
        }

        /// <summary>
        /// 멈춘 사물들의 최종 위치를 받는다. 보간 시각이 그 위치에 이르면 적용을 끝낸다.
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

        /// <summary>
        /// 보여줄 서버 시각의 자세를 모든 움직이는 사물에 적용한다.
        /// </summary>
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

                bool done = track.settleTime.HasValue && renderTime >= track.settleTime.Value;
                Pose pose = track.buffer.Sample(done ? track.buffer.LatestTime : renderTime);
                track.body.SetPose(pose.position, pose.rotation);
                if (done)
                {
                    track.body.SetCollidersEnabled(true);
                    finished.Add(pair.Key);
                }
            }

            foreach (int index in finished)
                tracks.Remove(index);
        }

        /// <summary>
        /// 모든 기록을 지우고 날아가던 사물의 충돌체를 다시 켠다. (판 종료, 접속 종료)
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

        /// <summary>
        /// 사물 번호의 기록을 찾거나 새로 만든다. 새로 움직이기 시작하면 충돌체를 끈다. 물리 몸체가 없는 번호면 null.
        /// </summary>
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

    /// <summary>
    /// 네트워크로 보내는 사물 자세 한 건. (번호 + 위치 + 회전)
    /// </summary>
    public struct PropPoseData
    {
        public int index;
        public Vector3 position;
        public Quaternion rotation;
    }
}
