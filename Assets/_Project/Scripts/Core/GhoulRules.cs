namespace GuardVsMannequin.Core
{
    /// <summary>소유자 이동·서버 검증·소리가 같은 수치를 쓰도록 사냥 시간 구울 규칙을 한곳에 둔다.</summary>
    public static class GhoulRules
    {
        // 지연·물리 오차로 도약이 조금 더 가도 되돌려지지 않게 서버가 더 허용하는 거리(m)
        private const float LungeSlack = 0.5f;

        /// <summary>도약 거리를 시간 안에 정확히 가도록 속도로 바꾼다.</summary>
        public static float GetLungeSpeed(float distance, float duration)
        {
            if (distance <= 0f || duration <= 0f)
                return 0f;
            return distance / duration;
        }

        /// <summary>
        /// 캐릭터 중력과 상관없이 정한 체공 시간 안에 정한 높이까지 솟았다 같은 높이로 내려오도록,
        /// 처음 위로 차오르는 속도와 도약 동안 쓸 중력을 구한다. 높이·시간이 0 이하면 둘 다 0(곧은 도약)이다.
        /// </summary>
        /// <param name="height">최고 높이(m).</param>
        /// <param name="duration">체공 시간(초).</param>
        public static void GetLungeArc(float height, float duration, out float launchSpeed, out float arcGravity)
        {
            if (height <= 0f || duration <= 0f)
            {
                launchSpeed = 0f;
                arcGravity = 0f;
                return;
            }
            launchSpeed = 4f * height / duration;
            arcGravity = 8f * height / (duration * duration);
        }

        /// <summary>스태미나 비율(0~1)을 초당 얼마나 쓰는지로 바꿔, 가득 찬 상태에서 정한 시간만큼 은신하게 한다.</summary>
        /// <param name="fullStaminaSeconds">가득 찬 스태미나로 은신할 수 있는 시간(초).</param>
        public static float GetStealthDrainPerSecond(float fullStaminaSeconds)
        {
            return fullStaminaSeconds <= 0f ? 0f : 1f / fullStaminaSeconds;
        }

        /// <summary>도약은 은신 중(이동 속도 0)에 일어나 평소 예산으로는 되돌려지므로, 서버가 따로 허용할 거리(m)를 준다.</summary>
        public static float GetLungeAllowance(float distance)
        {
            return distance <= 0f ? 0f : distance + LungeSlack;
        }

        /// <summary>은신 여부에 따라 몸소리 크기 배율을 고른다.</summary>
        public static float GetBodySoundMultiplier(bool isStealthed, float stealthMultiplier)
        {
            if (!isStealthed)
                return 1f;
            return stealthMultiplier < 0f ? 0f : stealthMultiplier > 1f ? 1f : stealthMultiplier;
        }
    }
}
