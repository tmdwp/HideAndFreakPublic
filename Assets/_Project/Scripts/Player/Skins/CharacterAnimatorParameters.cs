namespace GuardVsMannequin.Player
{
    /// <summary>게임 코드가 이 이름으로만 애니메이션을 요청하도록 애니메이터 파라미터 이름을 모은다.</summary>
    public static class CharacterAnimatorParameters
    {
        public const string Speed = "Speed";
        public const string MoveX = "MoveX";
        public const string MoveZ = "MoveZ";
        public const string Attack = "Attack";
        public const string Shriek = "Shriek";
        public const string Shoot = "Shoot";
        public const string Throw = "Throw";
        public const string Reload = "Reload";
        public const string Hit = "Hit";
        public const string Emote = "Emote";
        public const string EmoteId = "EmoteId";
        public const string Interact = "Interact";

        /// <summary>번호마다 상수를 따로 두지 않도록 이름 규칙으로 만든다.</summary>
        public static string EmoteTrigger(int emoteId) => Emote + emoteId;
    }
}
