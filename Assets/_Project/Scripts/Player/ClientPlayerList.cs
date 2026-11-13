using System.Collections.Generic;

namespace GuardVsMannequin.Player
{
    /// <summary>관전 대상을 고르도록 이 클라이언트에서 살아 있는 플레이어 아바타를 모은다.</summary>
    public static class ClientPlayerList
    {
        private static readonly List<PlayerIdentity> players = new();

        /// <summary>관전 대상을 넘길 때 순서가 매번 같도록 스폰 순서를 유지한다.</summary>
        public static IReadOnlyList<PlayerIdentity> Players => players;

        /// <summary>
        /// 같은 아바타가 두 번 들어가면 관전 순서에 두 번 나오므로 중복을 막는다.
        /// </summary>
        public static void Add(PlayerIdentity identity)
        {
            if (!players.Contains(identity))
                players.Add(identity);
        }

        /// <summary>
        /// 죽은 아바타를 관전 대상으로 고르지 않도록 제거될 때 뺀다.
        /// </summary>
        public static void Remove(PlayerIdentity identity)
        {
            players.Remove(identity);
        }
    }
}
