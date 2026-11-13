namespace GuardVsMannequin.Core
{
    /// <summary>소유자 입력과 서버 검증이 같은 규칙을 쓰도록 언제 움직일 수 있는지 결정한다.</summary>
    public static class MovementRules
    {
        /// <summary>
        /// 스폰 틱에 남은 입력으로 미끄러지지 않도록 준비 단계에서만 막는다.
        /// </summary>
        public static bool CanMove(GameState state)
        {
            return state != GameState.Preparation;
        }
    }
}
