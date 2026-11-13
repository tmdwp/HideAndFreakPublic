using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>1인칭·3인칭 구현이 달라도 입력 드라이버가 같은 방식으로 시점을 넘기도록 인터페이스로 둔다.</summary>
    public interface ILookControllable
    {
        /// <summary>감도는 입력 쪽 설정이라 이미 적용된 각도(도)를 받는다.</summary>
        void AddLookInput(Vector2 lookDelta);
    }
}
