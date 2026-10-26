using FishNet.Object;
using GuardVsMannequin.Core;
using GuardVsMannequin.PropPhysics;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>
    /// 변신한 마네킹이 날아가는 동안 서버에서만 쓰는 대리 물체. 변신 외형과 같은 경계·질량 규칙의 상자 몸체라
    /// 진짜 사물(<see cref="PropBody"/>)과 같은 물리로 날아간다.
    /// 날아가는 동안 맞는 판정도 이 물체가 받아 마네킹에게 넘긴다(피해·충격).
    /// </summary>
    public class KnockbackProxy : MonoBehaviour, IDamageable, IImpactReceiver
    {
        private IDamageable damageTarget;
        private IImpactReceiver impactTarget;

        /// <summary>물리 몸체.</summary>
        public Rigidbody Body { get; private set; }

        /// <inheritdoc />
        public float ImpactMass => Body.mass;

        /// <inheritdoc />
        public bool IsAlive => damageTarget.IsAlive;

        /// <inheritdoc />
        public PlayerRole Role => damageTarget.Role;

        /// <summary>
        /// 대리 물체를 만든다. 마네킹 자신의 충돌체와는 부딪히지 않는다.
        /// </summary>
        /// <param name="pose">변신 외형 기준 공간의 월드 자세.</param>
        /// <param name="bounds">변신 외형의 경계. (외형 기준 공간)</param>
        /// <param name="damageTarget">맞으면 피해를 넘길 대상(마네킹 체력).</param>
        /// <param name="impactTarget">맞으면 충격을 넘길 대상(마네킹의 날아가기 처리).</param>
        /// <param name="ownColliders">부딪히지 않을 마네킹 자신의 충돌체.</param>
        public static KnockbackProxy Create(Pose pose, Bounds bounds, IDamageable damageTarget, IImpactReceiver impactTarget, Collider[] ownColliders)
        {
            var proxyObject = new GameObject("KnockbackProxy");
            proxyObject.transform.SetPositionAndRotation(pose.position, pose.rotation);

            BoxCollider box = proxyObject.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = Vector3.Max(bounds.size, Vector3.one * 0.05f);
            foreach (Collider own in ownColliders)
            {
                if (own != null)
                    Physics.IgnoreCollision(box, own);
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

        /// <summary>
        /// 맞은 피해를 마네킹에게 넘긴다. (서버)
        /// </summary>
        public void TakeDamage(int amount, NetworkObject attacker)
        {
            damageTarget.TakeDamage(amount, attacker);
        }

        /// <summary>
        /// 맞은 충격을 마네킹의 날아가기 처리에 넘긴다. (서버)
        /// </summary>
        public void ReceiveImpact(Vector3 point, Vector3 impulse)
        {
            impactTarget.ReceiveImpact(point, impulse);
        }

        /// <summary>
        /// 날아가며 부딪힌 멈춘 사물·다른 변신한 마네킹을 깨운다.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            ImpactRelay.Relay(collision, impactTarget);
        }
    }
}
