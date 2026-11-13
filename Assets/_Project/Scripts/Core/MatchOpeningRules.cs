using UnityEngine;

namespace GuardVsMannequin.Core
{
    /// <summary>시야 축소·장치 잠금이 같은 기준을 쓰도록 판 시작 직후 구간(<see cref="GameTuning.MatchOpeningTime"/>)을 판정한다.</summary>
    public static class MatchOpeningRules
    {
        /// <summary>
        /// 탐색 타이머로 재므로 서버와 클라이언트가 같은 값을 얻고, 탐색이 아니면 0이다.
        /// </summary>
        public static float GetRemaining(IGameStateProvider provider)
        {
            if (provider == null || provider.CurrentState != GameState.Playing)
                return 0f;

            // 타이머가 아직 동기화되지 않은 클라이언트는 남은 시간이 0이라, 그때는 시작 구간으로 치지 않는다.
            if (provider.RemainingTime <= 0f)
                return 0f;

            float elapsed = provider.Settings.playTime - provider.RemainingTime;
            return Mathf.Max(0f, GameTuning.MatchOpeningTime - elapsed);
        }

        /// <summary>호출하는 쪽이 0과 비교하지 않도록 따로 둔다.</summary>
        public static bool IsActive(IGameStateProvider provider) => GetRemaining(provider) > 0f;
    }
}
