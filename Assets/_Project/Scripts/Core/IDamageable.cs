using FishNet.Object;

namespace GuardVsMannequin.Core
{
    /// <summary>서버에서만 호출되는 피해를 받을 수 있는 대상.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }

        /// <summary>같은 진영끼리 피해를 막는 등 피해 규칙이 역할로 갈리기 때문에 노출한다.</summary>
        PlayerRole Role { get; }

        /// <summary>
        /// 체력은 서버 값만 믿어야 조작을 막을 수 있으므로 서버에서만 호출한다.
        /// </summary>
        /// <param name="attacker">환경 피해처럼 공격자가 없으면 null.</param>
        /// <returns>피해가 실제로 들어갔으면 true. 공격자에게 명중을 알릴지 정하는 데 쓴다.</returns>
        bool TakeDamage(int amount, NetworkObject attacker);
    }
}
