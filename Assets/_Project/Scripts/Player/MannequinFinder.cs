using System.Collections.Generic;
using GuardVsMannequin.Core;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>서버 판정이 변신 여부와 상관없이 범위 안의 실제 마네킹을 찾도록 한다.</summary>
    public sealed class MannequinFinder
    {
        // 판정마다 배열을 새로 만들지 않도록 버퍼를 재사용한다.
        private readonly Collider[] overlapBuffer = new Collider[64];
        private readonly RaycastHit[] lineBuffer = new RaycastHit[16];
        private readonly HashSet<PlayerIdentity> found = new();

        /// <summary>
        /// 한 마네킹이 여러 콜라이더를 가지므로, 효과가 두 번 걸리지 않게 중복을 빼고 모은다.
        /// </summary>
        /// <param name="requireLineOfSight">벽 너머까지 효과가 닿지 않게 하려면 true.</param>
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
        /// 다른 플레이어나 사물 뒤에 숨는 것만으로 효과를 피하면 안 되므로, 환경(벽·바닥)만 시야를 막는 것으로 본다.
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
                Collider hitCollider = lineBuffer[i].collider;
                if (hitCollider.GetComponentInParent<PlayerIdentity>() == null && hitCollider.GetComponentInParent<TransformableProp>() == null)
                    return true;
            }
            return false;
        }
    }
}
