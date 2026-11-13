using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>조작을 막도록 플레이어 체력을 서버 권한으로 관리하고 동기화한다.</summary>
    [RequireComponent(typeof(PlayerIdentity))]
    public class PlayerHealth : NetworkBehaviour, IDamageable
    {
        private readonly SyncVar<int> currentHealth = new();
        private readonly SyncVar<int> maxHealth = new();

        private PlayerIdentity identity;

        /// <summary>사망 후 처리가 공격자를 알도록 사망자와 공격자를 함께 알린다.</summary>
        public event Action<PlayerHealth, NetworkObject> ServerDied;

        /// <summary>피격 애니메이션 같은 연출이 모든 화면에서 나오도록 피어마다 한 번씩 알린다.</summary>
        public event Action Damaged;

        public int CurrentHealth => currentHealth.Value;
        public int MaxHealth => maxHealth.Value;
        public bool IsAlive => currentHealth.Value > 0;
        public PlayerRole Role => identity.Role;

        /// <summary>
        /// 피해 판정마다 GetComponent를 부르지 않도록 미리 찾아 두고, 피격 연출용 동기화 콜백을 건다.
        /// </summary>
        private void Awake()
        {
            identity = GetComponent<PlayerIdentity>();
            currentHealth.OnChange += OnCurrentHealthChanged;
        }

        /// <summary>
        /// 파괴된 뒤 콜백이 불리지 않도록 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            currentHealth.OnChange -= OnCurrentHealthChanged;
        }

        /// <summary>
        /// 호스트는 서버·클라이언트 콜백이 둘 다 오므로, 연출이 두 번 나오지 않게 클라이언트 쪽에서만 알린다.
        /// </summary>
        private void OnCurrentHealthChanged(int previousHealth, int nextHealth, bool asServer)
        {
            if (asServer && IsClientInitialized)
                return;
            if (nextHealth < previousHealth)
                Damaged?.Invoke();
        }

        /// <summary>스폰 데이터에 실려 가도록 스폰 직전에 체력을 정하고, 즉사하지 않게 최소 1로 둔다.</summary>
        public void ServerInitialize(int newMaxHealth)
        {
            maxHealth.Value = Mathf.Max(1, newMaxHealth);
            currentHealth.Value = maxHealth.Value;
        }

        /// <summary>재접속으로 체력을 회복하는 꼼수를 막도록 끊기기 전 체력으로 되돌린다.</summary>
        public void ServerRestore(int savedCurrent, int savedMax)
        {
            maxHealth.Value = Mathf.Max(1, savedMax);
            currentHealth.Value = Mathf.Clamp(savedCurrent, 1, maxHealth.Value);
        }

        /// <summary>사냥 시간 체력 배율을 적용하되 이미 맞은 만큼은 남도록 비율을 유지한다.</summary>
        /// <param name="refill">true면 가득 채우고, false면 현재 비율을 유지한다.</param>
        [Server]
        public void SetMaxHealth(int newMaxHealth, bool refill)
        {
            if (!IsAlive)
                return;

            float ratio = maxHealth.Value > 0 ? (float)currentHealth.Value / maxHealth.Value : 1f;
            maxHealth.Value = Mathf.Max(1, newMaxHealth);
            currentHealth.Value = refill ? maxHealth.Value : Mathf.Max(1, Mathf.RoundToInt(maxHealth.Value * ratio));
        }

        /// <summary>
        /// 도구 쪽이 피해 규칙을 몰라도 되도록, 같은 편 공격 등은 여기서 다시 확인해 거른다.
        /// </summary>
        [Server]
        public void TakeDamage(int amount, NetworkObject attacker)
        {
            if (!IsAlive || amount <= 0)
                return;

            PlayerRole attackerRole = attacker != null && attacker.TryGetComponent(out PlayerIdentity attackerIdentity)
                ? attackerIdentity.Role
                : PlayerRole.None;

            if (!DamageRules.CanDamage(attackerRole, Role, GameServices.CurrentStateOrWaiting))
                return;

            currentHealth.Value = Mathf.Max(0, currentHealth.Value - amount);
            if (currentHealth.Value == 0)
                ServerDied?.Invoke(this, attacker);
        }
    }
}
