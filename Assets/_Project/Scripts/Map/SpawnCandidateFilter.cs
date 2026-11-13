using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace GuardVsMannequin.Map
{
    /// <summary>계산 규칙과 장면 질의를 나누려고 가드 스폰 후보의 NavMesh·물리 검사만 맡는다.</summary>
    public static class SpawnCandidateFilter
    {
        // 서 있을 공간 검사용 캡슐 (가드 몸 크기 기준)
        private const float BodyRadius = 0.3f;
        private const float BodyHeight = 1.8f;
        private const float FloorClearance = 0.15f;

        // 후보를 NavMesh에 붙일 때 허용하는 거리(m)
        private const float NavMeshSnapDistance = 1.5f;

        // 검사마다 새로 만들지 않도록 재사용한다.
        private static readonly NavMeshPath path = new();

        /// <summary>지붕 위나 막힌 방에 스폰하지 않도록 마네킹 스폰 지점까지 걸어갈 수 있고 서 있을 공간이 있는 후보만 남긴다.</summary>
        /// <param name="anchors">경로 기준 지점(마네킹 스폰 지점).</param>
        public static List<Vector3> FilterStandable(IEnumerable<Vector3> candidates, IReadOnlyList<Vector3> anchors)
        {
            bool hasAnchor = TryFindAnchor(anchors, out Vector3 anchor);
            var result = new List<Vector3>();
            foreach (Vector3 candidate in candidates)
            {
                if (hasAnchor && !IsReachable(candidate, anchor))
                    continue;
                if (!HasHeadroom(candidate))
                    continue;
                result.Add(candidate);
            }
            return result;
        }

        /// <summary>
        /// 스폰 지점이 NavMesh에서 살짝 떠 있을 수 있어서 붙일 수 있는 첫 지점을 경로 기준으로 쓴다.
        /// </summary>
        private static bool TryFindAnchor(IReadOnlyList<Vector3> anchors, out Vector3 anchor)
        {
            foreach (Vector3 point in anchors)
            {
                if (NavMesh.SamplePosition(point, out NavMeshHit hit, NavMeshSnapDistance, NavMesh.AllAreas))
                {
                    anchor = hit.position;
                    return true;
                }
            }
            anchor = default;
            return false;
        }

        /// <summary>
        /// 경로가 중간에 끊긴 부분 경로도 성공으로 나오므로 완전한 경로인지까지 확인한다.
        /// </summary>
        private static bool IsReachable(Vector3 candidate, Vector3 anchor)
        {
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, NavMeshSnapDistance, NavMesh.AllAreas))
                return false;
            return NavMesh.CalculatePath(hit.position, anchor, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
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
