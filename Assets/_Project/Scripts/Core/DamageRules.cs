namespace GuardVsMannequin.Core
{
    /// <summary>도구·체력 코드가 규칙을 중복 구현하지 않도록 피해 규칙을 한곳에 모은다.</summary>
    public static class DamageRules
    {
        /// <summary>
        /// 같은 진영끼리 맞히거나 마네킹이 탐색 중에 공격하는 일을 막기 위해, 피해를 줄 때마다 이것으로 거른다.
        /// </summary>
        public static bool CanDamage(PlayerRole attackerRole, PlayerRole targetRole, GameState state)
        {
            if (attackerRole == PlayerRole.Guard && targetRole == PlayerRole.Mannequin)
                return state == GameState.Playing || state == GameState.Hunting;

            // 마네킹이 탐색 중에도 공격할 수 있으면 숨는 쪽이 일방적으로 유리해진다.
            if (attackerRole == PlayerRole.Mannequin && targetRole == PlayerRole.Guard)
                return state == GameState.Hunting;

            return false;
        }

        /// <summary>
        /// 판 밖에서 쏘거나 던지지 않도록 탐색·사냥 시간에만 허용한다.
        /// </summary>
        public static bool CanUseGuardTools(IGameStateProvider provider)
        {
            if (provider == null)
                return false;
            GameState state = provider.CurrentState;
            return state == GameState.Playing || state == GameState.Hunting;
        }
    }
}
