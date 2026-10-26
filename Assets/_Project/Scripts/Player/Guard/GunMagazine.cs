using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>
    /// 총 탄창 규칙: 탄창을 다 쓰면 정해진 시간 동안 자동으로 장전한다. (시간 한 줄기 — 소유자 예측용과 서버 검증용을 따로 만든다)
    /// 서버용은 네트워크 지연으로 요청이 조금 일찍 도착할 수 있어 장전 끝을 <c>earlyTolerance</c>만큼 앞당겨 본다(<see cref="ToolCooldown"/>와 같은 방식).
    /// </summary>
    public sealed class GunMagazine
    {
        private readonly float earlyTolerance;
        private int roundsFired;
        // 장전이 끝나는 시각. 장전 중이 아니면 음수.
        private float reloadEndTime = -1f;

        /// <summary>
        /// 탄창 규칙을 만든다.
        /// </summary>
        /// <param name="earlyTolerance">장전 끝을 앞당겨 볼 시간(초). 소유자 예측은 0, 서버 검증은 허용 오차.</param>
        public GunMagazine(float earlyTolerance)
        {
            this.earlyTolerance = earlyTolerance;
        }

        /// <summary>
        /// 남은 탄 수.
        /// </summary>
        public int GetAmmo(int magazineSize, float now)
        {
            FinishReloadIfDue(now);
            return Mathf.Max(0, magazineSize - roundsFired);
        }

        /// <summary>
        /// 장전 남은 시간(초). 장전 중이 아니면 0.
        /// </summary>
        public float GetReloadRemaining(float now)
        {
            FinishReloadIfDue(now);
            return reloadEndTime < 0f ? 0f : reloadEndTime - now;
        }

        /// <summary>
        /// 한 발을 쓴다. 장전 중이거나 탄이 없으면 false. 마지막 발이면 장전을 시작한다.
        /// </summary>
        /// <param name="startedReload">이번 발로 장전을 시작했는지.</param>
        public bool TryFire(int magazineSize, float reloadTime, float now, out bool startedReload)
        {
            startedReload = false;
            FinishReloadIfDue(now);
            if (reloadEndTime >= 0f || roundsFired >= magazineSize)
                return false;

            roundsFired++;
            if (roundsFired >= magazineSize)
            {
                reloadEndTime = now + reloadTime - Mathf.Min(earlyTolerance, reloadTime * 0.5f);
                startedReload = true;
            }
            return true;
        }

        /// <summary>
        /// 다른 쪽(서버)이 알려준 상태로 맞춘다. (소유자 예측이 어긋났을 때)
        /// </summary>
        public void Overwrite(int magazineSize, int ammo, float reloadRemaining, float now)
        {
            roundsFired = Mathf.Clamp(magazineSize - ammo, 0, magazineSize);
            reloadEndTime = reloadRemaining > 0f ? now + reloadRemaining : -1f;
        }

        /// <summary>
        /// 장전 시간이 지났으면 탄창을 채운다.
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
