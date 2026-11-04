using System.Collections.Generic;
using GuardVsMannequin.Core;
using GuardVsMannequin.Player;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 로컬 플레이어의 아바타(체력, 도구, 스태미나)와 이번 판 역할을 이벤트로 추적한다.
    /// </summary>
    public sealed class LocalPlayerTracker
    {
        private static readonly ITool[] NoTools = new ITool[0];

        /// <summary>살아 있는 로컬 아바타. 사망·미참가면 null.</summary>
        public PlayerIdentity Player { get; private set; }

        /// <summary>로컬 아바타의 체력. 없으면 null.</summary>
        public PlayerHealth Health { get; private set; }

        /// <summary>로컬 아바타의 스태미나. 없으면 null.</summary>
        public IStaminaSource Stamina { get; private set; }

        /// <summary>로컬 아바타의 일시 효과(수류탄 등). 없으면 null.</summary>
        public IStatusEffects StatusEffects { get; private set; }

        /// <summary>로컬 아바타의 도구 목록.</summary>
        public IReadOnlyList<ITool> Tools { get; private set; } = NoTools;

        /// <summary>로컬 아바타의 탄창 있는 도구(가드 총). 없으면 null.</summary>
        public IAmmoSource Ammo { get; private set; }

        /// <summary>이번 판의 로컬 역할. 사망 후 안내·결과에 쓰려고 아바타가 사라져도 유지한다.</summary>
        public PlayerRole LastRole { get; private set; } = PlayerRole.None;

        /// <summary>
        /// 로컬 플레이어 이벤트를 구독한다.
        /// </summary>
        public void Attach()
        {
            LocalPlayerEvents.Spawned += OnSpawned;
            LocalPlayerEvents.Despawned += OnDespawned;
        }

        /// <summary>
        /// 이벤트 구독을 해제한다.
        /// </summary>
        public void Detach()
        {
            LocalPlayerEvents.Spawned -= OnSpawned;
            LocalPlayerEvents.Despawned -= OnDespawned;
        }

        /// <summary>
        /// 판이 끝나 대기 상태가 되면 지난 판의 역할을 지운다.
        /// </summary>
        public void ResetRole()
        {
            LastRole = PlayerRole.None;
        }

        /// <summary>
        /// 로컬 아바타의 체력, 스태미나, 도구를 캐시한다.
        /// </summary>
        private void OnSpawned(PlayerIdentity identity)
        {
            Player = identity;
            Health = identity.GetComponent<PlayerHealth>();
            Stamina = identity.GetComponent<IStaminaSource>();
            StatusEffects = identity.GetComponent<IStatusEffects>();
            Tools = identity.GetComponents<ITool>();
            Ammo = identity.GetComponent<IAmmoSource>();
            LastRole = identity.Role;
        }

        /// <summary>
        /// 로컬 아바타 참조를 비운다. 역할은 사망 후 안내를 위해 유지한다.
        /// </summary>
        private void OnDespawned(PlayerIdentity identity)
        {
            if (Player != identity)
                return;

            Player = null;
            Health = null;
            Stamina = null;
            StatusEffects = null;
            Tools = NoTools;
            Ammo = null;
        }
    }
}
