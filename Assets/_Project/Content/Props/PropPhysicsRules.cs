using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Props
{
    /// <summary>
    /// 변신 사물 물리(총에 맞으면 튕겨나감)의 대상·질량 규칙. 값만 계산하고 상태를 바꾸지 않는다.
    /// 게임(실행 중 물리 몸체 부착)과 에디터·모드 SDK(충돌체 준비·맵 검사)가 같은 규칙을 쓴다.
    /// </summary>
    public static class PropPhysicsRules
    {
        // 멈춤으로 볼 회전 속도(rad/s). 아주 느리게 구르는 것은 멈춘 것으로 본다.
        private const float SettleAngularSpeed = 0.5f;

        /// <summary>
        /// 크기·구조상 물리 대상이 될 수 있는 사물인지. (충돌체 모양은 보지 않음 — 에디터가 충돌체를 준비할 대상)
        /// 벽 조각, 너무 큰 사물, 다른 변신 사물을 품거나 그 안에 든 사물은 움직이지 않는다.
        /// </summary>
        public static bool IsCandidate(TransformableProp prop)
        {
            if (prop.IsWall)
                return false;
            PropAppearance appearance = prop.Appearance;
            if (appearance.Parts.Count == 0 || appearance.LargestSize > GameTuning.PropPhysicsMaxSize)
                return false;
            return !HasNestedProp(prop) && !HasChildRigidbody(prop);
        }

        /// <summary>
        /// 실행 중에 물리로 움직일 수 있는 사물인지. 대상 조건 + 이 사물의 충돌체가 모두 움직일 수 있는 모양이어야 한다.
        /// (볼록하지 않은 MeshCollider는 움직이는 물체에 쓸 수 없다 — 에디터에서 볼록으로 바꿔 둔다)
        /// </summary>
        public static bool CanSimulate(TransformableProp prop)
        {
            if (!GameTuning.PropPhysicsEnabled || !IsCandidate(prop))
                return false;

            bool hasSolid = false;
            foreach (Collider collider in prop.GetComponentsInChildren<Collider>(true))
            {
                if (collider.isTrigger)
                    continue;
                if (!IsSimulatable(collider))
                    return false;
                hasSolid = true;
            }
            return hasSolid;
        }

        /// <summary>
        /// 움직이는 물체(다이나믹 Rigidbody)에 쓸 수 있는 충돌체인지. 기본 도형과 볼록 MeshCollider만 된다.
        /// </summary>
        public static bool IsSimulatable(Collider collider)
        {
            return collider is not MeshCollider meshCollider || meshCollider.convex;
        }

        /// <summary>
        /// 멈춰 있는(키네마틱) 변신 사물 몸체인지. 이런 사물은 맵 지형처럼 다룬다. (벽타기 가능, 이동 검증의 벽 통과 검사 대상)
        /// 다른 플레이어·날아가는 사물처럼 움직이는 몸체는 false.
        /// </summary>
        public static bool IsRestingProp(Rigidbody body)
        {
            return body != null && body.isKinematic && body.GetComponent<TransformableProp>() != null;
        }

        /// <summary>
        /// 움직이는 몸체가 거의 멈췄는지. (잠들었거나 느린 상태 — 멈춤 판정은 이 상태가 <see cref="GameTuning.PropSettleTime"/> 이어져야 한다)
        /// </summary>
        public static bool IsSlow(Rigidbody body)
        {
            return body.IsSleeping()
                   || (body.linearVelocity.magnitude <= GameTuning.PropSettleSpeed && body.angularVelocity.magnitude <= SettleAngularSpeed);
        }

        /// <summary>
        /// 겉모습 경계 크기로 질량(kg)을 정한다. (부피 × 밀도, 최소·최대로 자름)
        /// </summary>
        public static float GetMass(Bounds bounds)
        {
            Vector3 size = bounds.size;
            return Mathf.Clamp(size.x * size.y * size.z * GameTuning.PropDensity, GameTuning.PropMassMin, GameTuning.PropMassMax);
        }

        /// <summary>
        /// 총 한 발의 충격량(N·s). 질량의 제곱근에 비례하므로 날아가는 속도는 <see cref="GameTuning.GunKnockback"/> ÷ √질량이다.
        /// (가벼운 사물은 빠르게, 무거운 사물도 눈에 띄게 밀린다. 충격량이 질량과 무관하면 무거운 가구가 거의 움직이지 않는다)
        /// </summary>
        public static float GetGunImpulse(float mass)
        {
            return GameTuning.GunKnockback * Mathf.Sqrt(Mathf.Max(mass, 0.01f));
        }

        /// <summary>
        /// 다른 변신 사물을 자식으로 품거나 다른 변신 사물의 자식인지. (Rigidbody가 겹치면 함께 움직여 동기화가 어긋난다)
        /// </summary>
        private static bool HasNestedProp(TransformableProp prop)
        {
            Transform parent = prop.transform.parent;
            if (parent != null && parent.GetComponentInParent<TransformableProp>(true) != null)
                return true;
            return prop.GetComponentsInChildren<TransformableProp>(true).Length > 1;
        }

        /// <summary>
        /// 사물 자신이 아닌 자식 조각에 Rigidbody가 있는지. (에셋 팩 모델에 붙어 온 경우 — 자식 충돌체가 사물 몸체에 묶이지 않는다)
        /// </summary>
        private static bool HasChildRigidbody(TransformableProp prop)
        {
            foreach (Rigidbody body in prop.GetComponentsInChildren<Rigidbody>(true))
            {
                if (body.gameObject != prop.gameObject)
                    return true;
            }
            return false;
        }
    }
}
