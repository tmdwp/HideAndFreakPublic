namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 캐릭터 애니메이터 컨트롤러가 가질 수 있는 파라미터 이름. 게임 코드는 이 이름으로만 애니메이션을 요청한다.
    /// 컨트롤러에 없는 파라미터는 무시되므로, 필요한 것만 만들면 된다. (애니메이션이 없으면 아무 일도 일어나지 않음)
    ///
    /// | 이름 | 형식 | 설정 시점 |
    /// |---|---|---|
    /// | Speed | Float | 매 프레임, 수평 이동 속도(m/s) |
    /// | MoveX, MoveZ | Float | 매 프레임, 캐릭터 기준 좌우/앞뒤 속도(m/s) |
    /// | Attack | Trigger | 마네킹 근접 공격 |
    /// | Shoot | Trigger | 가드 사격 |
    /// | Throw | Trigger | 가드 수류탄 투척 |
    /// | Reload | Trigger | 가드 총 장전 (탄창을 다 쓴 순간) |
    /// | Hit | Trigger | 피격(체력 감소) |
    /// | Emote1 ~ Emote6 | Trigger | 감정 표현 (번호별 트리거) |
    /// | Emote + EmoteId | Trigger + Int | 감정 표현 (트리거 하나 + 번호, 번호별 트리거가 없을 때) |
    /// | Interact | Bool | 전원 장치 등과 상호작용하는 동안 켜짐 |
    ///
    /// 감정 표현 트리거가 하나도 없으면 절차적 움직임(ProceduralEmoteView)으로 대신한다.
    /// </summary>
    public static class CharacterAnimatorParameters
    {
        public const string Speed = "Speed";
        public const string MoveX = "MoveX";
        public const string MoveZ = "MoveZ";
        public const string Attack = "Attack";
        public const string Shoot = "Shoot";
        public const string Throw = "Throw";
        public const string Reload = "Reload";
        public const string Hit = "Hit";
        public const string Emote = "Emote";
        public const string EmoteId = "EmoteId";
        public const string Interact = "Interact";

        /// <summary>
        /// 번호별 감정 표현 트리거 이름. (예: Emote3)
        /// </summary>
        public static string EmoteTrigger(int emoteId) => Emote + emoteId;
    }
}
