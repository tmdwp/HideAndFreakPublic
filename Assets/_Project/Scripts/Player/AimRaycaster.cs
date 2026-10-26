using System;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 자기 자신의 충돌체를 무시하는 레이캐스트.
    /// 3인칭 카메라 광선이 자기 몸에 먼저 맞는 문제를 막는다.
    /// </summary>
    public static class AimRaycaster
    {
        /// <summary>클라이언트가 보낸 조준 원점이 아바타에서 이보다 멀면 비정상으로 본다(m). (3인칭 카메라 거리 포함)</summary>
        public const float MaxOriginOffset = 3f;

        private static readonly RaycastHit[] hitBuffer = new RaycastHit[32];

        /// <summary>
        /// 클라이언트가 보낸 조준 값이 쓸 수 있는 값인지 확인한다. (서버 RPC 입구에서 사용)
        /// 원점·방향에 NaN·무한대가 있거나 방향 길이가 0이면 false. 이런 값으로 판정하면 궤적·위치가 NaN이 되어 모든 클라이언트에 퍼진다.
        /// </summary>
        public static bool IsValidAim(Vector3 origin, Vector3 direction)
        {
            return IsFinite(origin) && IsFinite(direction) && direction.sqrMagnitude > 1e-6f;
        }

        /// <summary>
        /// 벡터의 모든 성분이 유한한 수인지 확인한다.
        /// </summary>
        public static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        /// <summary>
        /// 서버에서 클라이언트가 보낸 조준 원점을 검증한다.
        /// 원점이 아바타에서 너무 멀면(3인칭 카메라가 멀리 있거나 조작) 같은 조준선 위에서 눈에 가장 가까운 점으로 당긴다.
        /// 조준선이 그대로라 3인칭에서도 화면 가운데를 맞힌다. 조준선이 몸 근처를 지나지 않으면(조작 의심) 눈 위치로 대체한다.
        /// </summary>
        /// <param name="origin">클라이언트가 보낸 원점.</param>
        /// <param name="direction">클라이언트가 보낸 조준 방향.</param>
        /// <param name="owner">아바타 트랜스폼.</param>
        /// <param name="eyeHeight">눈 높이(m).</param>
        public static Vector3 SanitizeOrigin(Vector3 origin, Vector3 direction, Transform owner, float eyeHeight)
        {
            float maxSqr = MaxOriginOffset * MaxOriginOffset;
            if ((origin - owner.position).sqrMagnitude <= maxSqr)
                return origin;

            Vector3 eye = owner.position + Vector3.up * eyeHeight;
            if (direction.sqrMagnitude > 1e-6f)
            {
                Vector3 forward = direction.normalized;
                // 원점에서 앞쪽으로만 당긴다. (카메라와 몸 사이의 물체는 맞히지 않음)
                float along = Vector3.Dot(eye - origin, forward);
                Vector3 onAimLine = origin + forward * Mathf.Max(0f, along);
                if ((onAimLine - owner.position).sqrMagnitude <= maxSqr)
                    return onAimLine;
            }
            return eye;
        }

        /// <summary>
        /// ignoreRoot 하위의 충돌체와 트리거를 제외하고 가장 가까운 충돌을 찾는다.
        /// </summary>
        /// <param name="ray">검사할 광선.</param>
        /// <param name="maxDistance">최대 거리.</param>
        /// <param name="ignoreRoot">무시할 루트 트랜스폼(보통 자기 아바타).</param>
        /// <param name="hit">가장 가까운 충돌 정보.</param>
        /// <returns>충돌이 있는지 여부.</returns>
        public static bool TryRaycast(Ray ray, float maxDistance, Transform ignoreRoot, out RaycastHit hit)
        {
            int count = Physics.RaycastNonAlloc(ray, hitBuffer, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(hitBuffer, 0, count, HitDistanceComparer.Instance);

            for (int i = 0; i < count; i++)
            {
                if (ignoreRoot != null && hitBuffer[i].transform.IsChildOf(ignoreRoot))
                    continue;

                hit = hitBuffer[i];
                return true;
            }

            hit = default;
            return false;
        }

        /// <summary>
        /// 충돌 거리 오름차순 비교기.
        /// </summary>
        private sealed class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new();

            /// <summary>거리로 비교한다.</summary>
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
