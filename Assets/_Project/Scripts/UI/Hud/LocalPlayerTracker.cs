using System.Collections.Generic;
using GuardVsMannequin.Core;
using GuardVsMannequin.Player;

namespace GuardVsMannequin.UI
{
    /// <summary>HUD가 아바타를 직접 찾지 않도록 로컬 플레이어의 아바타와 역할을 이벤트로 추적한다.</summary>
    public sealed class LocalPlayerTracker
    {
        private static readonly ITool[] NoTools = new ITool[0];

        /// <summary>사망·미참가면 null이므로 HUD는 이 값으로 표시 여부를 정한다.</summary>
        public PlayerIdentity Player { get; private set; }

        public PlayerHealth Health { get; private set; }

        public IStaminaSource Stamina { get; private set; }

        /// <summary>수류탄 같은 일시 효과.</summary>
        public IStatusEffects StatusEffects { get; private set; }

        /// <summary>아바타가 없을 때도 반복문이 null 검사 없이 돌도록 빈 목록을 쓴다.</summary>
        public IReadOnlyList<ITool> Tools { get; private set; } = NoTools;

        /// <summary>탄창이 있는 도구.</summary>
        public IAmmoSource Ammo { get; private set; }

        /// <summary>사망 후 안내·결과에 쓰려고 아바타가 사라져도 이번 판 역할을 유지한다.</summary>
        public PlayerRole LastRole { get; private set; } = PlayerRole.None;

        /// <summary>
        /// HUD가 켜져 있는 동안만 추적하도록 바깥에서 구독 시점을 정한다.
        /// </summary>
        public void Attach()
        {
            LocalPlayerEvents.Spawned += OnSpawned;
            LocalPlayerEvents.Despawned += OnDespawned;
        }

        /// <summary>
        /// 정적 이벤트라 끊지 않으면 파괴된 HUD가 계속 불린다.
        /// </summary>
        public void Detach()
        {
            LocalPlayerEvents.Spawned -= OnSpawned;
            LocalPlayerEvents.Despawned -= OnDespawned;
        }

        /// <summary>
        /// 다음 판 시작 전 대기 화면에 지난 판 역할 안내가 남지 않도록 지운다.
        /// </summary>
        public void ResetRole()
        {
            LastRole = PlayerRole.None;
        }

        /// <summary>
        /// HUD 영역들이 매 프레임 GetComponent를 부르지 않도록 스폰할 때 한 번 찾아 둔다.
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

        /// <summary>역할 전환 중 새 아바타가 먼저 스폰될 수 있어 지금 추적 중인 아바타일 때만 비운다.</summary>
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
