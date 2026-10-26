using System.Collections.Generic;
using GuardVsMannequin.Core;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 범위 안의 실제 마네킹(변신 여부 무관)을 찾는다. (서버 판정용)
    /// </summary>
    public sealed class MannequinFinder
    {
        private readonly Collider[] overlapBuffer = new Collider[64];
        private readonly RaycastHit[] lineBuffer = new RaycastHit[16];
        private readonly HashSet<PlayerIdentity> found = new();

        /// <summary>
        /// 중심에서 반경 안의 살아 있는 실제 마네킹을 중복 없이 찾는다.
        /// </summary>
        /// <param name="requireLineOfSight">true면 중심에서 대상까지 벽(환경)에 막힌 대상은 뺀다. 플레이어와 변신 사물은 막지 않는다.</param>
        public IReadOnlyCollection<PlayerIdentity> Find(Vector3 center, float radius, bool requireLineOfSight)
        {
            found.Clear();
            int count = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                PlayerIdentity identity = overlapBuffer[i].GetComponentInParent<PlayerIdentity>();
                if (identity == null || identity.Role != PlayerRole.Mannequin || found.Contains(identity))
                    continue;
                if (requireLineOfSight && IsBlocked(center, overlapBuffer[i].bounds.center))
                    continue;
                found.Add(identity);
            }
            return found;
        }

        /// <summary>
        /// 두 점 사이를 환경(벽·바닥)이 막는지 확인한다. 플레이어와 변신 가능 사물은 무시한다.
        /// </summary>
        private bool IsBlocked(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.01f)
                return false;

            int count = Physics.RaycastNonAlloc(from, delta / distance, lineBuffer, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = lineBuffer[i].collider;
                if (hit.GetComponentInParent<PlayerIdentity>() == null && hit.GetComponentInParent<TransformableProp>() == null)
                    return true;
            }
            return false;
        }
    }
}
