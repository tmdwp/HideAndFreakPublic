using FishNet.Object;
using FishNet.Object.Synchronizing;
using GuardVsMannequin.Core;

namespace GuardVsMannequin.Player
{
    /// <summary>매 프레임 보내지 않도록 수류탄 효과를 끝나는 서버 Tick으로 동기화한다.</summary>
    public class StatusEffects : NetworkBehaviour, IStatusEffects, IMoveSpeedModifier
    {
        private readonly SyncVar<uint> revealEndTick = new();
        private readonly SyncVar<uint> slowEndTick = new();

        /// <inheritdoc />
        public bool IsRevealed => IsActive(revealEndTick.Value);

        /// <inheritdoc />
        public bool IsSlowed => IsActive(slowEndTick.Value);

        /// <inheritdoc />
        public float SpeedMultiplier => IsSlowed ? GameTuning.GrenadeSlowMultiplier : 1f;

        /// <summary>남은 시간을 매번 보내지 않도록 끝나는 Tick만 보내고, 다시 맞으면 덮어써 중첩되지 않게 한다.</summary>
        [Server]
        public void ApplyGrenadeHit()
        {
            uint now = TimeManager.Tick;
            revealEndTick.Value = now + TimeManager.TimeToTicks(GameTuning.GrenadeRevealDuration);
            slowEndTick.Value = now + TimeManager.TimeToTicks(GameTuning.GrenadeSlowDuration);
        }

        /// <summary>
        /// 네트워크 초기화 전에는 TimeManager가 없어 예외가 나므로, 그때는 효과가 없는 것으로 본다.
        /// </summary>
        private bool IsActive(uint endTick)
        {
            if (endTick == 0 || TimeManager == null || !IsSpawned)
                return false;
            return TimeManager.Tick < endTick;
        }
    }
}
