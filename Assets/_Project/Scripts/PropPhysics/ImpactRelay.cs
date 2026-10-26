using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.PropPhysics
{
    /// <summary>
    /// 날아가는 몸체(사물, 변신한 마네킹의 대리 물체)가 멈춰 있는 대상에 부딪히면 같은 세기의 충격을 넘긴다. (서버)
    /// 멈춘 사물은 키네마틱이라 물리만으로는 밀리지 않으므로, 충분히 세게 부딪힌 경우에만 깨워 연쇄를 제한한다.
    /// </summary>
    public static class ImpactRelay
    {
        /// <summary>
        /// 충돌 상대가 충격을 받을 수 있고 충격이 <see cref="GameTuning.PropWakeImpulse"/> 이상이면 넘긴다.
        /// </summary>
        /// <param name="collision">날아가는 몸체 쪽에서 받은 충돌 정보.</param>
        /// <param name="self">날아가는 몸체 자신. (자기에게 되돌려 주지 않도록)</param>
        public static void Relay(Collision collision, IImpactReceiver self)
        {
            float strength = collision.impulse.magnitude;
            if (strength < GameTuning.PropWakeImpulse || collision.contactCount == 0)
                return;

            // 이미 움직이는 상대는 물리 엔진이 서로 밀어낸다.
            Rigidbody other = collision.rigidbody;
            if (other != null && !other.isKinematic)
                return;

            IImpactReceiver receiver = collision.collider.GetComponentInParent<IImpactReceiver>();
            if (receiver == null || ReferenceEquals(receiver, self))
                return;

            // 접촉 법선은 상대 표면에서 이쪽을 향하므로, 상대는 반대 방향으로 민다.
            ContactPoint contact = collision.GetContact(0);
            receiver.ReceiveImpact(contact.point, -contact.normal * strength);
        }
    }
}
