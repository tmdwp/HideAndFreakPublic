using FishNet.Object;
using GuardVsMannequin.Core;
using GuardVsMannequin.PropPhysics;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>변신한 마네킹이 진짜 사물과 같은 물리로 날아가도록 서버에서만 쓰는 대리 물체.</summary>
    public class KnockbackProxy : MonoBehaviour, IDamageable, IImpactReceiver
    {
        private IDamageable damageTarget;
        private IImpactReceiver impactTarget;

        public Rigidbody Body { get; private set; }

        /// <inheritdoc />
        public float ImpactMass => Body.mass;

        /// <inheritdoc />
        public bool IsAlive => damageTarget.IsAlive;

        /// <inheritdoc />
        public PlayerRole Role => damageTarget.Role;

        /// <summary>진짜 사물과 같은 세기로 날아가도록 외형 경계로 상자와 질량을 정하고 마네킹 충돌체와는 부딪히지 않게 한다.</summary>
        /// <param name="pose">변신 외형 기준 공간의 월드 자세.</param>
        /// <param name="bounds">변신 외형의 경계.</param>
        /// <param name="damageTarget">날아가는 동안 맞아도 피해가 마네킹에게 가도록 넘길 대상.</param>
        /// <param name="impactTarget">날아가는 동안 맞아도 충격이 더해지도록 넘길 대상.</param>
        /// <param name="ownColliders">부딪히지 않을 마네킹 자신의 충돌체.</param>
        public static KnockbackProxy Create(Pose pose, Bounds bounds, IDamageable damageTarget, IImpactReceiver impactTarget, Collider[] ownColliders)
        {
            var proxyObject = new GameObject("KnockbackProxy");
            proxyObject.transform.SetPositionAndRotation(pose.position, pose.rotation);

            BoxCollider box = proxyObject.AddComponent<BoxCollider>();
            box.center = bounds.center;
            // 납작한 사물은 두께가 0에 가까워 바닥을 뚫으므로 최소 두께를 둔다.
            box.size = Vector3.Max(bounds.size, Vector3.one * 0.05f);
            foreach (Collider ownCollider in ownColliders)
            {
                if (ownCollider != null)
                    Physics.IgnoreCollision(box, ownCollider);
            }

            Rigidbody body = proxyObject.AddComponent<Rigidbody>();
            body.mass = PropPhysicsRules.GetMass(bounds);
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            KnockbackProxy proxy = proxyObject.AddComponent<KnockbackProxy>();
            proxy.Body = body;
            proxy.damageTarget = damageTarget;
            proxy.impactTarget = impactTarget;
            return proxy;
        }

        /// <summary>날아가는 동안 맞은 총알은 이 물체에 닿으므로 마네킹 체력으로 넘긴다.</summary>
        public void TakeDamage(int amount, NetworkObject attacker)
        {
            damageTarget.TakeDamage(amount, attacker);
        }

        /// <summary>날아가는 동안 맞은 충격도 반영되도록 날아가기 처리로 넘긴다.</summary>
        public void ReceiveImpact(Vector3 point, Vector3 impulse)
        {
            impactTarget.ReceiveImpact(point, impulse);
        }

        /// <summary>
        /// 날아가며 부딪힌 멈춘 사물·다른 변신한 마네킹도 진짜 사물처럼 함께 밀려나도록 깨운다.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            ImpactRelay.Relay(collision, impactTarget);
        }
    }
}
