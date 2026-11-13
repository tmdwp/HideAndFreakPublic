using System;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>3인칭 카메라 광선이 자기 몸에 먼저 맞지 않도록 자기 충돌체를 무시하는 레이캐스트.</summary>
    public static class AimRaycaster
    {
        /// <summary>3인칭 카메라 거리를 허용하되 멀리서 쏜 것처럼 조작하지 못하게 하는 상한(m).</summary>
        public const float MaxOriginOffset = 3f;

        // 레이캐스트마다 배열을 새로 만들지 않도록 재사용한다.
        private static readonly RaycastHit[] hitBuffer = new RaycastHit[32];
        private static readonly System.Collections.Generic.List<IHitShapeOverride> overrideBuffer = new();

        /// <summary>NaN 값이 모든 클라이언트에 퍼지지 않도록 서버 RPC 입구에서 먼저 거른다.</summary>
        public static bool IsValidAim(Vector3 origin, Vector3 direction)
        {
            return IsFinite(origin) && IsFinite(direction) && direction.sqrMagnitude > 1e-6f;
        }

        /// <summary>
        /// Vector3에는 유한성 검사가 없어서 성분별로 확인한다.
        /// </summary>
        public static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        /// <summary>3인칭 사격은 허용하면서 원점 조작은 막도록 먼 원점을 같은 조준선 위 눈 가까운 점으로 당긴다.</summary>
        /// <param name="owner">아바타 트랜스폼.</param>
        /// <param name="eyeHeight">단위는 m.</param>
        public static Vector3 SanitizeOrigin(Vector3 origin, Vector3 direction, Transform owner, float eyeHeight)
        {
            float maxSqrDistance = MaxOriginOffset * MaxOriginOffset;
            if ((origin - owner.position).sqrMagnitude <= maxSqrDistance)
                return origin;

            Vector3 eyePosition = owner.position + Vector3.up * eyeHeight;
            if (direction.sqrMagnitude > 1e-6f)
            {
                Vector3 forward = direction.normalized;
                // 뒤로 당기면 카메라와 몸 사이 물체를 맞히므로 앞쪽으로만 당긴다.
                float alongDistance = Vector3.Dot(eyePosition - origin, forward);
                Vector3 onAimLine = origin + forward * Mathf.Max(0f, alongDistance);
                if ((onAimLine - owner.position).sqrMagnitude <= maxSqrDistance)
                    return onAimLine;
            }
            return eyePosition;
        }

        /// <summary>
        /// 3인칭 카메라 광선이 자기 몸에 먼저 맞지 않도록 ignoreRoot 아래 충돌체를 건너뛰고 가장 가까운 충돌을 찾는다.
        /// </summary>
        /// <param name="ignoreRoot">보통 자기 아바타.</param>
        public static bool TryRaycast(Ray ray, float maxDistance, Transform ignoreRoot, out RaycastHit hit)
        {
            int count = Physics.RaycastNonAlloc(ray, hitBuffer, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            // NonAlloc 결과는 거리순이 아니어서 정렬해야 가장 가까운 것을 고를 수 있다.
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

        /// <summary>이동 캡슐은 몸보다 커서 빈 곳을 쏴도 맞으므로 몸에 맞춘 판정 캡슐로 맞힌다.</summary>
        public static bool TryRaycastHittable(Ray ray, float maxDistance, Transform ignoreRoot, out RaycastHit hit)
        {
            int count = Physics.RaycastNonAlloc(ray, hitBuffer, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            Array.Sort(hitBuffer, 0, count, HitDistanceComparer.Instance);

            for (int i = 0; i < count; i++)
            {
                Collider targetCollider = hitBuffer[i].collider;
                if (ignoreRoot != null && hitBuffer[i].transform.IsChildOf(ignoreRoot))
                    continue;
                if (targetCollider.isTrigger ? !SkinHitboxes.IsHitbox(targetCollider) : IsReplacedCollider(targetCollider))
                    continue;

                hit = hitBuffer[i];
                return true;
            }

            hit = default;
            return false;
        }

        /// <summary>
        /// 몸 판정 캡슐·변신 사물 모양처럼 대신 맞힐 모양이 있으면 이동 캡슐을 건너뛰고, 그런 모양이 여럿이라 모두 묻는다.
        /// </summary>
        private static bool IsReplacedCollider(Collider collider)
        {
            collider.GetComponents(overrideBuffer);
            foreach (IHitShapeOverride hitShapeOverride in overrideBuffer)
            {
                if (hitShapeOverride.ReplacesCollider(collider))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 정렬할 때마다 람다를 만들지 않도록 비교기를 하나만 둔다.
        /// </summary>
        private sealed class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new();

            /// <summary>가까운 충돌이 앞에 오도록 거리 오름차순으로 비교한다.</summary>
            public int Compare(RaycastHit first, RaycastHit second) => first.distance.CompareTo(second.distance);
        }
    }
}
