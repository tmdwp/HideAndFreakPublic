using UnityEngine;

namespace GuardVsMannequin.Core
{
    /// <summary>
    /// 충격을 받아 튕겨나갈 수 있는 대상. (변신 사물, 변신한 마네킹)
    /// 총은 맞은 대상이 무엇인지 모르고 이 인터페이스로 충격만 전달한다. 서버에서만 호출한다.
    /// </summary>
    public interface IImpactReceiver
    {
        /// <summary>충격을 받는 몸체의 질량(kg). 총은 이 값으로 충격량을 정한다(가벼울수록 빠르게 날아감).</summary>
        float ImpactMass { get; }

        /// <summary>
        /// 월드 위치 <paramref name="point"/>에 충격량 <paramref name="impulse"/>(N·s)를 받는다.
        /// </summary>
        void ReceiveImpact(Vector3 point, Vector3 impulse);
    }

    /// <summary>
    /// 변신 사물 물리 서비스(서버). 사물 번호로 충격을 주고 움직임을 동기화한다.
    /// </summary>
    public interface IPropPhysics
    {
        /// <summary>
        /// 번호 <paramref name="propIndex"/>의 사물을 깨워 충격을 준다. 물리 대상이 아니면 무시한다.
        /// </summary>
        void ApplyImpact(int propIndex, Vector3 point, Vector3 impulse);
    }
}
