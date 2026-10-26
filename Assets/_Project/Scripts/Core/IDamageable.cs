using FishNet.Object;

namespace GuardVsMannequin.Core
{
    /// <summary>
    /// 피해를 받을 수 있는 대상. 모든 호출은 서버에서만 이루어진다.
    /// (플레이어 등)
    /// </summary>
    public interface IDamageable
    {
        /// <summary>살아 있는지 여부.</summary>
        bool IsAlive { get; }

        /// <summary>이 대상의 역할. 피해 규칙 판정에 사용한다.</summary>
        PlayerRole Role { get; }

        /// <summary>
        /// 피해를 적용한다. (서버 전용)
        /// </summary>
        /// <param name="amount">피해량.</param>
        /// <param name="attacker">공격한 아바타. 없으면 null.</param>
        void TakeDamage(int amount, NetworkObject attacker);
    }
}
