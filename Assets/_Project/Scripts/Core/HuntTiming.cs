namespace GuardVsMannequin.Core
{
    /// <summary>목표가 0회일 때 가드 보호 시간 뒤 사냥 시간에 들어가는 시점을 계산한다.</summary>
    public static class HuntTiming
    {
        /// <summary>
        /// 목표가 없으면 사냥 진입 조건이 목표 달성에서 시간 경과로 바뀌므로, 그 분기를 한곳에서 판단한다.
        /// </summary>
        public static bool IsTimedHunt(GameSettings settings)
        {
            return settings.objectiveCount <= 0;
        }

        /// <summary>별도 타이머가 탐색 타이머와 어긋나지 않도록 탐색 타이머에서 거꾸로 계산한다.</summary>
        public static float GetProtectionRemaining(GameSettings settings, float remainingPlayTime)
        {
            float elapsed = settings.playTime - remainingPlayTime;
            return settings.huntDelay - elapsed;
        }
    }
}
