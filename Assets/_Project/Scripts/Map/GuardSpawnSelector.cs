using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Map
{
    /// <summary>씬·네트워크 없이 시험할 수 있도록 가드 스폰 위치를 고르는 계산만 맡는다.</summary>
    public sealed class GuardSpawnSelector
    {
        private readonly List<Vector3> candidates;
        private readonly Vector3 mapCenter;
        private readonly float minGuardSeparation;
        private readonly List<Vector3> chosenPositions = new();

        /// <param name="candidatePoints">맵 지점 + NavMesh 무작위 지점 중 갈 수 있는 곳만.</param>
        /// <param name="mapCenter">스폰 직후 맵 쪽을 바라보도록 시점 방향 기준으로 쓴다.</param>
        /// <param name="minGuardSeparation">가드끼리 한곳에 몰리지 않게 하는 최소 거리(m).</param>
        public GuardSpawnSelector(IEnumerable<Vector3> candidatePoints, Vector3 mapCenter, float minGuardSeparation)
        {
            candidates = new List<Vector3>(candidatePoints);
            this.mapCenter = mapCenter;
            this.minGuardSeparation = minGuardSeparation;
        }

        /// <summary>매번 같은 자리가 되지 않게 다른 가드와 떨어진 곳 중 무작위로 고르고, 없으면 가장 떨어진 곳을 쓴다.</summary>
        public bool TrySelect(out Pose pose)
        {
            pose = default;
            if (candidates.Count == 0)
                return false;

            var validPositions = new List<Vector3>();
            Vector3 bestPosition = candidates[0];
            float bestDistance = float.MinValue;
            foreach (Vector3 candidate in candidates)
            {
                float guardDistance = DistanceToNearest(candidate, chosenPositions);
                if (guardDistance >= minGuardSeparation)
                    validPositions.Add(candidate);
                if (guardDistance > bestDistance)
                {
                    bestDistance = guardDistance;
                    bestPosition = candidate;
                }
            }

            Vector3 selectedPosition = validPositions.Count > 0 ? validPositions[Random.Range(0, validPositions.Count)] : bestPosition;
            chosenPositions.Add(selectedPosition);
            pose = new Pose(selectedPosition, FaceTowards(selectedPosition, mapCenter));
            return true;
        }

        /// <summary>바로 위층 가드와 겹친 것으로 보지 않도록 높이를 뺀 수평 거리로 잰다.</summary>
        private static float DistanceToNearest(Vector3 point, IReadOnlyList<Vector3> others)
        {
            float nearestDistance = float.MaxValue;
            foreach (Vector3 other in others)
            {
                Vector2 delta = new(point.x - other.x, point.z - other.z);
                nearestDistance = Mathf.Min(nearestDistance, delta.magnitude);
            }
            return nearestDistance;
        }

        /// <summary>스폰 직후 벽만 보지 않도록 맵 중심을 바라보게 한다.</summary>
        private static Quaternion FaceTowards(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.01f ? Quaternion.LookRotation(direction) : Quaternion.identity;
        }
    }
}
