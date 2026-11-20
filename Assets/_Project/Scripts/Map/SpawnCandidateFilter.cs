using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace GuardVsMannequin.Map
{
    /// <summary>계산 규칙과 장면 질의를 나누려고 가드 스폰 후보의 NavMesh·물리 검사만 맡는다.</summary>
    public static class SpawnCandidateFilter
    {
        /// <summary>모두 걸러졌을 때 원인을 로그로 알 수 있도록 걸러진 이유별 개수를 모은다.</summary>
        public struct Report
        {
            public int anchorCount;
            public int unreachable;
            public int noHeadroom;

            public override string ToString()
            {
                return $"기준 지점 {anchorCount}개, 경로 없음 {unreachable}개, 머리 공간 없음 {noHeadroom}개";
            }
        }

        // 서 있을 공간 검사용 캡슐 (가드 몸 크기 기준)
        private const float BodyRadius = 0.3f;
        private const float BodyHeight = 1.8f;
        private const float FloorClearance = 0.15f;

        // 후보를 NavMesh에 붙일 때 허용하는 거리(m)
        private const float NavMeshSnapDistance = 1.5f;

        // 경로 검사가 후보 수 × 기준 수만큼 늘어나므로 기준 지점 수를 제한한다.
        private const int MaxAnchors = 3;

        // 검사마다 새로 만들지 않도록 재사용한다.
        private static readonly NavMeshPath path = new();

        /// <summary>지붕 위나 막힌 방에 스폰하지 않도록 마네킹 스폰 지점까지 걸어갈 수 있고 서 있을 공간이 있는 후보만 남긴다.</summary>
        /// <param name="anchors">경로 기준 지점(마네킹 스폰 지점).</param>
        public static List<Vector3> FilterStandable(IEnumerable<Vector3> candidates, IReadOnlyList<Vector3> anchors, out Report report)
        {
            List<Vector3> snappedAnchors = SnapAnchors(anchors);
            report = new Report { anchorCount = snappedAnchors.Count };
            var result = new List<Vector3>();
            foreach (Vector3 candidate in candidates)
            {
                if (snappedAnchors.Count > 0 && !IsReachableFromAny(candidate, snappedAnchors))
                {
                    report.unreachable++;
                    continue;
                }
                if (!HasHeadroom(candidate))
                {
                    report.noHeadroom++;
                    continue;
                }
                result.Add(candidate);
            }
            return result;
        }

        /// <summary>
        /// 기준 하나가 가구 위 같은 작은 섬에 붙으면 모든 후보가 걸러지므로, 여러 지점을 붙여 두고 그중 하나에만 닿아도 통과시킨다.
        /// </summary>
        private static List<Vector3> SnapAnchors(IReadOnlyList<Vector3> anchors)
        {
            var snapped = new List<Vector3>();
            foreach (Vector3 point in anchors)
            {
                if (snapped.Count >= MaxAnchors)
                    break;
                if (NavMesh.SamplePosition(point, out NavMeshHit hit, NavMeshSnapDistance, NavMesh.AllAreas))
                    snapped.Add(hit.position);
            }
            return snapped;
        }

        /// <summary>
        /// 경로가 중간에 끊긴 부분 경로도 성공으로 나오므로 완전한 경로인지까지 확인한다.
        /// </summary>
        private static bool IsReachableFromAny(Vector3 candidate, List<Vector3> anchors)
        {
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, NavMeshSnapDistance, NavMesh.AllAreas))
                return false;
            foreach (Vector3 anchor in anchors)
            {
                if (NavMesh.CalculatePath(hit.position, anchor, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                    return true;
            }
            return false;
        }

        /// <summary>NavMesh는 가구 밑 같은 낮은 틈에도 깔리므로 가드 몸 크기 캡슐로 비어 있는지 본다.</summary>
        private static bool HasHeadroom(Vector3 feetPosition)
        {
            Vector3 capsuleBottom = feetPosition + Vector3.up * (BodyRadius + FloorClearance);
            Vector3 capsuleTop = feetPosition + Vector3.up * (BodyHeight - BodyRadius);
            return !Physics.CheckCapsule(capsuleBottom, capsuleTop, BodyRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }
    }
}
