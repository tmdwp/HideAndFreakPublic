namespace GuardVsMannequin.Core
{
    /// <summary>소유자 스태미나와 서버 이동 검증이 같은 수치로 계산하도록 달리기 규칙을 한곳에 둔다.</summary>
    public static class SprintRules
    {
        // 데이터 파일에 0초가 들어와도 0으로 나누지 않게 하는 하한
        private const float MinSeconds = 0.01f;

        /// <summary>1 미만이면 달리기가 오히려 느려지므로 1로 막는다.</summary>
        public static float SpeedMultiplier => System.Math.Max(1f, GameTuning.SprintSpeedMultiplier);

        /// <summary>
        /// 데이터에는 "몇 초 달릴 수 있는지"로 적는 편이 이해하기 쉬워서, 여기서 초당 비율(0~1)로 바꾼다.
        /// </summary>
        public static float DrainPerSecond => 1f / System.Math.Max(MinSeconds, GameTuning.StaminaSprintSeconds);

        /// <summary>
        /// 데이터에는 "몇 초에 다 차는지"로 적어서, 여기서 초당 비율(0~1)로 바꾼다.
        /// </summary>
        public static float RecoverPerSecond => 1f / System.Math.Max(MinSeconds, GameTuning.StaminaRecoverSeconds);

        /// <summary>단위는 초.</summary>
        public static float RecoverDelay => GameTuning.StaminaRecoverDelay;

        /// <summary>바닥나자마자 다시 달리면 깜빡이듯 끊기므로, 이 비율(0~1)까지 찬 뒤에야 다시 달릴 수 있다.</summary>
        public static float RestartRatio => GameTuning.StaminaRestartRatio;

        /// <summary>
        /// 서버가 달리기로 더 간 거리를 허용할 상한이 필요해서, 스태미나를 다 쓸 때까지 더 갈 수 있는 거리(m)로 계산한다.
        /// </summary>
        /// <param name="baseSpeed">달리지 않을 때의 이동 속도(m/s).</param>
        public static float GetMaxExtraDistance(float baseSpeed)
        {
            return baseSpeed * (SpeedMultiplier - 1f) * GameTuning.StaminaSprintSeconds;
        }

        /// <summary>서버 예산이 클라이언트와 따로 놀면 정상 이동이 거부되므로 같은 회복 비율로 채운다.</summary>
        /// <param name="baseSpeed">달리지 않을 때의 이동 속도(m/s).</param>
        public static float GetExtraDistanceRecoverRate(float baseSpeed)
        {
            return GetMaxExtraDistance(baseSpeed) * RecoverPerSecond;
        }
    }
}
