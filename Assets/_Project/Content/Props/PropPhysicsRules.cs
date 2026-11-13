using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Props
{
    /// <summary>게임·에디터·모드 SDK가 같은 물리 대상 규칙을 쓰도록 값만 계산한다.</summary>
    public static class PropPhysicsRules
    {
        // 아주 느리게 구르는 것은 멈춘 것으로 보는 회전 속도(rad/s).
        private const float SettleAngularSpeed = 0.5f;

        /// <summary>벽·너무 큰 사물·다른 사물을 품은 사물은 날아가면 맵이 망가지므로 물리 대상에서 뺀다.</summary>
        public static bool IsCandidate(TransformableProp prop)
        {
            if (prop.IsWall)
                return false;
            PropAppearance appearance = prop.Appearance;
            if (appearance.Parts.Count == 0 || appearance.LargestSize > GameTuning.PropPhysicsMaxSize)
                return false;
            return !HasNestedProp(prop) && !HasChildRigidbody(prop);
        }

        /// <summary>볼록하지 않은 MeshCollider는 움직일 수 없으므로 충돌체 모양까지 확인한다.</summary>
        public static bool CanSimulate(TransformableProp prop)
        {
            if (!GameTuning.PropPhysicsEnabled || !IsCandidate(prop))
                return false;

            bool hasSolidCollider = false;
            foreach (Collider collider in prop.GetComponentsInChildren<Collider>(true))
            {
                if (collider.isTrigger)
                    continue;
                if (!IsSimulatable(collider))
                    return false;
                hasSolidCollider = true;
            }
            return hasSolidCollider;
        }

        /// <summary>
        /// PhysX는 볼록하지 않은 MeshCollider를 다이나믹 Rigidbody에 붙일 수 없으므로 기본 도형과 볼록 MeshCollider만 통과시킨다.
        /// </summary>
        public static bool IsSimulatable(Collider collider)
        {
            return collider is not MeshCollider meshCollider || meshCollider.convex;
        }

        /// <summary>멈춰 있는 변신 사물은 벽타기·벽 통과 검사에서 지형처럼 다루기 위해 구별한다.</summary>
        public static bool IsRestingProp(Rigidbody body)
        {
            return body != null && body.isKinematic && body.GetComponent<TransformableProp>() != null;
        }

        /// <summary>사물이 자리를 잡았는지 판단하기 위해 거의 멈췄는지 확인한다.</summary>
        public static bool IsSlow(Rigidbody body)
        {
            return body.IsSleeping()
                   || (body.linearVelocity.magnitude <= GameTuning.PropSettleSpeed && body.angularVelocity.magnitude <= SettleAngularSpeed);
        }

        /// <summary>사물마다 질량을 손으로 정하지 않도록 부피 × 밀도로 정하고 최소·최대로 자른다.</summary>
        public static float GetMass(Bounds bounds)
        {
            Vector3 size = bounds.size;
            return Mathf.Clamp(size.x * size.y * size.z * GameTuning.PropDensity, GameTuning.PropMassMin, GameTuning.PropMassMax);
        }

        /// <summary>무거운 가구도 눈에 띄게 밀리도록 충격량을 질량의 제곱근에 비례하게 한다.</summary>
        public static float GetGunImpulse(float mass)
        {
            return GameTuning.GunKnockback * Mathf.Sqrt(Mathf.Max(mass, 0.01f));
        }

        /// <summary>
        /// 무거운 가구는 거의 안 밀리고 가벼운 물건은 쉽게 넘어가도록 총처럼 √질량에 비례시키되, 부딪힌 속도만큼 세게 민다.
        /// 위아래로 밀면 사물이 튀어 오르므로 수평 성분만 쓴다.
        /// </summary>
        public static Vector3 GetPushImpulse(Vector3 characterVelocity, float mass)
        {
            Vector3 horizontal = Vector3.ProjectOnPlane(characterVelocity, Vector3.up);
            float speed = Mathf.Min(horizontal.magnitude, GameTuning.PlayerPushMaxSpeed);
            if (speed <= 0.0001f)
                return Vector3.zero;
            return horizontal.normalized * (GameTuning.PlayerPushStrength * speed * Mathf.Sqrt(Mathf.Max(mass, 0.01f)));
        }

        /// <summary>
        /// 캐릭터는 사물 아래쪽에 닿는 경우가 많아 그 자리를 밀면 미끄러지기만 하므로, 위쪽을 밀어 키 큰 사물이 넘어지게 한다.
        /// </summary>
        public static Vector3 GetPushPoint(Bounds propBounds, Vector3 contactPoint)
        {
            Vector3 point = propBounds.ClosestPoint(contactPoint);
            point.y = Mathf.Max(point.y, Mathf.Lerp(propBounds.min.y, propBounds.max.y, PushHeightRatio));
            return point;
        }

        /// <summary>조작된 클라이언트가 멀리 있는 사물을 밀지 못하도록 아바타와 닿은 지점이 사물 가까이에 있는지 본다.</summary>
        public static bool IsPushPlausible(Bounds propBounds, Vector3 avatarPosition, Vector3 contactPoint)
        {
            return propBounds.SqrDistance(contactPoint) <= PushContactTolerance * PushContactTolerance
                   && propBounds.SqrDistance(avatarPosition) <= PushAvatarReach * PushAvatarReach;
        }

        // 사물 높이에서 미는 지점의 비율(0 = 바닥, 1 = 꼭대기).
        private const float PushHeightRatio = 0.75f;
        // 네트워크 지연으로 위치가 어긋나도 받아 주는 거리(m).
        private const float PushContactTolerance = 0.6f;
        // 아바타 기준점(발)에서 탁자 위 사물까지 닿을 수 있는 거리(m).
        private const float PushAvatarReach = 2f;

        /// <summary>Rigidbody가 겹치면 함께 움직여 동기화가 어긋나므로 다른 변신 사물과 부모·자식 관계인지 확인한다.</summary>
        private static bool HasNestedProp(TransformableProp prop)
        {
            Transform parent = prop.transform.parent;
            if (parent != null && parent.GetComponentInParent<TransformableProp>(true) != null)
                return true;
            return prop.GetComponentsInChildren<TransformableProp>(true).Length > 1;
        }

        /// <summary>자식 Rigidbody가 있으면 자식 충돌체가 사물 몸체에 묶이지 않으므로 확인한다.</summary>
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
