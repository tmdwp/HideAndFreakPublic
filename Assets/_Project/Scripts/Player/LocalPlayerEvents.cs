using System;

namespace GuardVsMannequin.Player
{
    /// <summary>카메라·HUD가 플레이어를 직접 찾지 않도록 로컬 아바타의 생성·제거를 알린다.</summary>
    public static class LocalPlayerEvents
    {
        public static event Action<PlayerIdentity> Spawned;

        /// <summary>사망·판 종료로 아바타가 사라지면 카메라가 관전으로 넘어가야 해서 알린다.</summary>
        public static event Action<PlayerIdentity> Despawned;

        /// <summary>늦게 생긴 구독자도 지금 아바타를 알도록 남겨 둔다.</summary>
        public static PlayerIdentity Current { get; private set; }

        /// <summary>
        /// 늦게 생긴 구독자를 위해 현재 아바타를 먼저 기록한 뒤 알린다.
        /// </summary>
        public static void RaiseSpawned(PlayerIdentity identity)
        {
            Current = identity;
            Spawned?.Invoke(identity);
        }

        /// <summary>
        /// 다시 스폰된 새 아바타가 먼저 등록됐을 수 있어서, 같은 아바타일 때만 현재 값을 비운다.
        /// </summary>
        public static void RaiseDespawned(PlayerIdentity identity)
        {
            if (Current == identity)
                Current = null;
            Despawned?.Invoke(identity);
        }
    }
}
