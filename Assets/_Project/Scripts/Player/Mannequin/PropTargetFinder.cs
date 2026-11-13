using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>변신 요청과 외곽선이 같은 기준을 쓰도록 조준 광선으로 변신할 수 있는 사물을 찾는다.</summary>
    public sealed class PropTargetFinder
    {
        private readonly Transform owner;
        private readonly IAimProvider aimProvider;
        private readonly float range;

        /// <summary>변신 요청과 외곽선 표시가 같은 조건으로 판정하도록 한 객체를 함께 쓴다.</summary>
        /// <param name="owner">자기 몸은 무시하고, 사거리는 카메라가 아닌 몸에서 잰다.</param>
        /// <param name="range">몸에서 사물까지의 최대 거리(m).</param>
        public PropTargetFinder(Transform owner, IAimProvider aimProvider, float range)
        {
            this.owner = owner;
            this.aimProvider = aimProvider;
            this.range = range;
        }

        /// <summary>
        /// 3인칭에서 카메라가 멀리 있어도 같은 거리에서 변신할 수 있도록, 거리 판정은 카메라가 아닌 몸 기준으로 한다.
        /// </summary>
        public bool TryFind(out TransformableProp prop)
        {
            prop = null;
            Ray aim = aimProvider.GetAimRay();

            // 3인칭 카메라가 몸 뒤에 있으므로 카메라-몸 거리만큼 광선을 늘린 뒤, 맞은 지점은 몸에서 다시 잰다.
            float cameraDistance = Vector3.Distance(aim.origin, owner.position);
            if (!AimRaycaster.TryRaycast(aim, range + cameraDistance, owner, out RaycastHit hit))
                return false;
            if (Vector3.Distance(hit.point, owner.position) > range)
                return false;

            TransformableProp candidate = hit.collider.GetComponentInParent<TransformableProp>();
            if (candidate == null || !candidate.CanDisguiseAs)
                return false;

            prop = candidate;
            return true;
        }
    }
}
