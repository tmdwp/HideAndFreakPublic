namespace GuardVsMannequin.Core
{
    /// <summary>바꾸면 구조가 달라지는 고정 수치만 두고, 밸런스 수치는 <see cref="GameTuning"/>, 호스트 설정은 <see cref="GameSettings"/>에 둔다.</summary>
    public static class GameConstants
    {
        public const int MinPlayers = 2;
        public const int MaxPlayers = 10;

        public const int Damage = 1;

        /// <summary>모든 맵의 천장 높이보다 커야 하는 가드 히트스캔 사거리.</summary>
        public const float GunRange = 100f;

        /// <summary>감정 표현 개수(1~6번 키).</summary>
        public const int EmoteCount = 6;
    }
}
