using System;

namespace GuardVsMannequin.Player
{
    /// <summary>HUD가 도구 구현을 몰라도 되도록, 내 공격이 맞았다는 서버 확인을 이 PC 안에서 알린다.</summary>
    public static class LocalCombatEvents
    {
        /// <summary>인자는 이번 공격으로 대상이 탈락했는지 여부.</summary>
        public static event Action<bool> HitConfirmed;

        /// <summary>
        /// 판정은 서버가 했으므로 받은 결과만 그대로 전한다.
        /// </summary>
        public static void RaiseHitConfirmed(bool isEliminated)
        {
            HitConfirmed?.Invoke(isEliminated);
        }
    }
}
