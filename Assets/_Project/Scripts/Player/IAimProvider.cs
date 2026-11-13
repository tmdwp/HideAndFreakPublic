using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>도구가 카메라 구현을 몰라도 조준하도록 화면 중앙 광선을 제공한다.</summary>
    public interface IAimProvider
    {
        /// <summary>
        /// 카메라가 매 프레임 움직이므로 캐시하지 않고 쏘는 순간에 계산한다.
        /// </summary>
        Ray GetAimRay();
    }
}
