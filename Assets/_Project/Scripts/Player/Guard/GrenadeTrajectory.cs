using System.Collections.Generic;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>
    /// 튕겨 다니는 경로 하나의 구간. 물리 상태가 피어마다 다를 수 있어 서버가 계산한 구간을 그대로 보내고, 받는 쪽은 시간만으로 위치를 구한다.
    /// </summary>
    public struct GrenadeSegment
    {
        public Vector3 start;
        public Vector3 velocity;
        /// <summary>던진 순간부터 이 구간이 시작되기까지의 시간(초).</summary>
        public float startTime;
        /// <summary>멈춘 구간은 중력을 받지 않고 그 자리에 머문다.</summary>
        public bool isResting;

        public GrenadeSegment(Vector3 start, Vector3 velocity, float startTime, bool isResting)
        {
            this.start = start;
            this.velocity = velocity;
            this.startTime = startTime;
            this.isResting = isResting;
        }
    }

    /// <summary>서버가 한 번만 경로를 정하고 모든 피어가 같은 수류탄을 그리도록, 튕김을 포함한 포물선 구간을 계산한다.</summary>
    public static class GrenadeTrajectory
    {
        /// <summary>얇은 벽을 건너뛰지 않을 만큼 촘촘하게 둔 충돌 검사 간격(초).</summary>
        private const float StepTime = 0.02f;

        /// <summary>단위는 m.</summary>
        public const float Radius = 0.1f;

        /// <summary>구석에 끼어 끝없이 튕기는 경로가 RPC를 키우지 않도록 구간 수를 제한한다.</summary>
        private const int MaxSegments = 12;

        /// <summary>바닥에서 이보다 느리게 튀어 오르면 구르는 대신 멈춘 것으로 본다(m/s).</summary>
        private const float RestSpeed = 1.2f;

        /// <summary>이 값보다 위를 향한 면만 바닥으로 봐서 벽에서는 멈추지 않게 한다.</summary>
        private const float FloorNormalY = 0.6f;

        /// <summary>면을 따라 미끄러지는 속도를 닿을 때마다 줄여 계속 굴러가지 않게 한다.</summary>
        private const float SurfaceFriction = 0.7f;

        /// <summary>
        /// 튕긴 자리를 면에 딱 붙여 두면 다음 검사가 "처음부터 겹침"으로 잡혀 면 쪽으로 되튕기므로 조금 더 띄운다(m).
        /// </summary>
        private const float SurfaceGap = 0.02f;

        /// <summary>던진 순간부터 <paramref name="fuseTime"/>까지 튕기며 날아가는 구간을 구한다.</summary>
        /// <param name="ignoreRoot">던진 가드 자신에 맞아 튕기지 않도록 무시한다.</param>
        public static GrenadeSegment[] Simulate(Vector3 start, Vector3 velocity, float fuseTime, Transform ignoreRoot)
        {
            var segments = new List<GrenadeSegment> { new(start, velocity, 0f, false) };
            var hits = new RaycastHit[8];
            float bounciness = GameTuning.GrenadeBounciness;
            Collider bouncedFrom = null;

            while (true)
            {
                GrenadeSegment segment = segments[segments.Count - 1];
                if (!TryFindHit(segment, fuseTime, ignoreRoot, bouncedFrom, hits, out Vector3 hitPosition, out Vector3 hitNormal, out float hitTime, out bouncedFrom))
                    break;

                Vector3 arrivalVelocity = segment.velocity + Physics.gravity * (hitTime - segment.startTime);
                Vector3 bouncedVelocity = Bounce(arrivalVelocity, hitNormal, bounciness);
                bool shouldRest = segments.Count + 1 >= MaxSegments
                                  || (hitNormal.y > FloorNormalY && Vector3.Dot(bouncedVelocity, hitNormal) < RestSpeed);
                segments.Add(new GrenadeSegment(hitPosition, shouldRest ? Vector3.zero : bouncedVelocity, hitTime, shouldRest));
                if (shouldRest)
                    break;
            }
            return segments.ToArray();
        }

        /// <summary>위치를 동기화하지 않고 모든 피어가 같은 위치를 그리도록 구간과 시간만으로 위치를 구한다.</summary>
        public static Vector3 GetPosition(GrenadeSegment[] segments, float time)
        {
            GrenadeSegment segment = segments[0];
            for (int i = 1; i < segments.Length && segments[i].startTime <= time; i++)
                segment = segments[i];

            return segment.isResting ? segment.start : GetSegmentPosition(segment, time - segment.startTime);
        }

        /// <summary>지금 시간이 멈춘 구간이면 굴러가는 연출을 끌 수 있도록 알려 준다.</summary>
        public static bool IsResting(GrenadeSegment[] segments, float time)
        {
            GrenadeSegment last = segments[segments.Length - 1];
            return last.isResting && time >= last.startTime;
        }

        private static Vector3 GetSegmentPosition(GrenadeSegment segment, float localTime)
        {
            return segment.start + segment.velocity * localTime + 0.5f * Physics.gravity * (localTime * localTime);
        }

        /// <summary>
        /// 면에 수직인 속도는 탄성만큼 되튀고 면을 따라가는 속도는 마찰만큼 줄어야 실제 공처럼 잦아든다.
        /// </summary>
        private static Vector3 Bounce(Vector3 velocity, Vector3 normal, float bounciness)
        {
            Vector3 normalVelocity = Vector3.Dot(velocity, normal) * normal;
            Vector3 tangentVelocity = velocity - normalVelocity;
            return tangentVelocity * SurfaceFriction - normalVelocity * bounciness;
        }

        /// <summary>구간을 따라가며 처음 닿는 면을 찾는다.</summary>
        /// <param name="hitPosition">면 안쪽에서 다음 구간이 시작되지 않도록 닿은 면에서 반지름만큼 띄운 곳.</param>
        /// <param name="hitTime">던진 순간부터의 시간(초).</param>
        /// <param name="bouncedFrom">방금 튕긴 면. 거기서 떠나는 중에 닿아 있는 것은 새 충돌이 아니다.</param>
        private static bool TryFindHit(GrenadeSegment segment, float fuseTime, Transform ignoreRoot, Collider bouncedFrom, RaycastHit[] hits,
            out Vector3 hitPosition, out Vector3 hitNormal, out float hitTime, out Collider hitCollider)
        {
            Vector3 previousPosition = segment.start;
            for (float localTime = StepTime; segment.startTime + localTime <= fuseTime + StepTime; localTime += StepTime)
            {
                Vector3 nextPosition = GetSegmentPosition(segment, localTime);
                Vector3 delta = nextPosition - previousPosition;
                float distance = delta.magnitude;
                if (distance > 0.0001f)
                {
                    int count = Physics.SphereCastNonAlloc(previousPosition, Radius, delta / distance, hits, distance,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    if (TryNearestHit(hits, count, ignoreRoot, bouncedFrom, out RaycastHit hit))
                    {
                        // 시작부터 겹쳐 있으면 point가 0이라 직전 위치에서 날아온 반대쪽으로 튕긴다.
                        bool startedInside = IsStartOverlap(hit);
                        hitNormal = startedInside ? -delta / distance : hit.normal;
                        hitPosition = startedInside ? previousPosition : hit.point + hit.normal * (Radius + SurfaceGap);
                        hitTime = segment.startTime + localTime - StepTime + StepTime * (hit.distance / distance);
                        hitCollider = hit.collider;
                        return true;
                    }
                }
                previousPosition = nextPosition;
            }

            hitPosition = default;
            hitNormal = default;
            hitTime = 0f;
            hitCollider = null;
            return false;
        }

        /// <summary>SphereCast는 시작부터 겹친 대상을 거리 0, 위치 0으로 돌려준다.</summary>
        private static bool IsStartOverlap(RaycastHit hit)
        {
            return hit.distance <= 0f && hit.point == Vector3.zero;
        }

        /// <summary>
        /// SphereCastNonAlloc 결과는 거리순이 아니어서 무시할 대상을 빼고 가장 가까운 것을 직접 고른다.
        /// 방금 튕긴 면과 처음부터 닿아 있는 것은 빼야 그 면 쪽으로 되튕기며 제자리에 멈추지 않는다.
        /// </summary>
        private static bool TryNearestHit(RaycastHit[] hits, int count, Transform ignoreRoot, Collider bouncedFrom, out RaycastHit nearest)
        {
            nearest = default;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                if (ignoreRoot != null && hits[i].transform.IsChildOf(ignoreRoot))
                    continue;
                if (bouncedFrom != null && hits[i].collider == bouncedFrom && IsStartOverlap(hits[i]))
                    continue;
                if (!found || hits[i].distance < nearest.distance)
                {
                    nearest = hits[i];
                    found = true;
                }
            }
            return found;
        }
    }
}
