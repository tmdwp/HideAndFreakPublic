namespace GuardVsMannequin.Core
{
    /// <summary>대기 → 준비 → 탐색 → (사냥) → 종료 순으로 순환하는 한 판의 진행 단계.</summary>
    public enum GameState : byte
    {
        /// <summary>아바타가 아직 스폰되지 않은 접속 대기.</summary>
        Waiting = 0,

        /// <summary>아바타·장치를 스폰하는 한 틱짜리 판 시작 단계.</summary>
        Preparation = 1,

        /// <summary>제한 시간 동안의 탐색 단계.</summary>
        Playing = 2,

        /// <summary>조명이 꺼지고 마네킹이 가드를 공격할 수 있는 사냥 시간.</summary>
        Hunting = 3,

        /// <summary>승패가 결정되어 결과를 보여주는 단계.</summary>
        Ended = 4
    }
}
