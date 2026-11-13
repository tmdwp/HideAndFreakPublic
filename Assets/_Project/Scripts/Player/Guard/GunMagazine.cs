using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>소유자 예측과 서버 검증이 같은 규칙을 쓰도록 탄창과 장전 규칙을 한곳에 둔다.</summary>
    public sealed class GunMagazine
    {
        private readonly float earlyTolerance;
        private int roundsFired;
        // 장전이 끝나는 시각(장전 중이 아니면 음수).
        private float reloadEndTime = -1f;

        /// <summary>
        /// 소유자 예측과 서버 검증이 같은 규칙을 쓰되 허용 오차만 다르게 둘 수 있도록 오차를 받는다.
        /// </summary>
        /// <param name="earlyTolerance">장전 끝을 앞당겨 볼 허용 오차(초).</param>
        public GunMagazine(float earlyTolerance)
        {
            this.earlyTolerance = earlyTolerance;
        }

        /// <summary>
        /// 장전 완료를 따로 알리는 타이머가 없어서, 읽을 때마다 끝났는지 먼저 확인한다.
        /// </summary>
        public int GetAmmo(int magazineSize, float now)
        {
            FinishReloadIfDue(now);
            return Mathf.Max(0, magazineSize - roundsFired);
        }

        /// <summary>장전 완료를 알리는 타이머가 없어 읽을 때마다 끝났는지 먼저 확인한다.</summary>
        public float GetReloadRemaining(float now)
        {
            FinishReloadIfDue(now);
            return reloadEndTime < 0f ? 0f : reloadEndTime - now;
        }

        /// <summary>마지막 발 뒤 따로 R을 누르지 않아도 되도록 탄창이 비면 바로 장전한다.</summary>
        /// <param name="reloadTimePerRound">단위는 초.</param>
        /// <param name="startedReload">장전 동작을 재생할지 정하도록 이번 발로 장전을 시작했는지 알려 준다.</param>
        public bool TryFire(int magazineSize, float reloadTimePerRound, float now, out bool startedReload)
        {
            startedReload = false;
            FinishReloadIfDue(now);
            if (reloadEndTime >= 0f || roundsFired >= magazineSize)
                return false;

            roundsFired++;
            if (roundsFired >= magazineSize)
            {
                BeginReload(reloadTimePerRound, now);
                startedReload = true;
            }
            return true;
        }

        /// <summary>쓴 탄이 없을 때 장전하면 시간만 잡아먹으므로 거부한다.</summary>
        /// <param name="reloadTimePerRound">단위는 초.</param>
        public bool TryStartReload(int magazineSize, float reloadTimePerRound, float now)
        {
            FinishReloadIfDue(now);
            if (reloadEndTime >= 0f)
                return false;

            // 테스트 방에서 탄창 크기를 줄이면 쓴 탄 수가 넘칠 수 있어서 탄창 크기만큼만 채운다.
            roundsFired = Mathf.Min(roundsFired, magazineSize);
            if (roundsFired <= 0)
                return false;

            BeginReload(reloadTimePerRound, now);
            return true;
        }

        /// <summary>조금 쏘고 장전하면 빨리 끝나도록 쓴 탄 수에 비례해 시간을 잡는다.</summary>
        private void BeginReload(float reloadTimePerRound, float now)
        {
            float reloadTime = roundsFired * reloadTimePerRound;
            reloadEndTime = now + reloadTime - Mathf.Min(earlyTolerance, reloadTime * 0.5f);
        }

        /// <summary>
        /// 서버가 요청을 거부하면 소유자 예측이 어긋나므로 서버가 알려준 상태로 맞춘다.
        /// </summary>
        public void Overwrite(int magazineSize, int ammo, float reloadRemaining, float now)
        {
            roundsFired = Mathf.Clamp(magazineSize - ammo, 0, magazineSize);
            reloadEndTime = reloadRemaining > 0f ? now + reloadRemaining : -1f;
        }

        /// <summary>
        /// 장전 완료 이벤트를 따로 두지 않고, 상태를 읽거나 바꾸기 전에 시각만 보고 채운다.
        /// </summary>
        private void FinishReloadIfDue(float now)
        {
            if (reloadEndTime < 0f || now < reloadEndTime)
                return;

            roundsFired = 0;
            reloadEndTime = -1f;
        }
    }
}
