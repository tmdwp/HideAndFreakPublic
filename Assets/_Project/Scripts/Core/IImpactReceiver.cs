using UnityEngine;

namespace GuardVsMannequin.Core
{
    /// <summary>총이 맞은 대상을 몰라도 충격을 전달하도록 튕겨나갈 수 있는 대상을 인터페이스로 둔다.</summary>
    public interface IImpactReceiver
    {
        /// <summary>가벼울수록 멀리 날리도록 충격량을 정하는 질량(kg).</summary>
        float ImpactMass { get; }

        /// <summary>
        /// <summary>맞은 지점에 따라 회전도 달라지도록 충격 위치를 함께 받는다.</summary>
        /// </summary>
        void ReceiveImpact(Vector3 point, Vector3 impulse);
    }

    /// <summary>서버에서 사물 번호로 충격을 주고 움직임을 동기화하는 물리 서비스.</summary>
    public interface IPropPhysics
    {
        /// <summary>사물은 네트워크 오브젝트가 아니라서 번호로 지정한다.</summary>
        void ApplyImpact(int propIndex, Vector3 point, Vector3 impulse);
    }

    /// <summary>물리는 서버만 계산하므로, 내 캐릭터가 사물에 부딪혔다는 사실만 클라이언트에서 서버로 알리는 창구.</summary>
    public interface IPropPushRequester
    {
        /// <param name="point">캐릭터가 닿은 지점.</param>
        /// <param name="velocity">부딪힐 때 캐릭터 속도(m/s).</param>
        void RequestPush(int propIndex, Vector3 point, Vector3 velocity);
    }
}
