using GuardVsMannequin.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>시선을 떼지 않고 탄을 보도록 조준점 둘레 게이지로 남은 탄과 장전을 보여 준다.</summary>
    public class HudAmmoDisplay : MonoBehaviour, IHudSection
    {
        [SerializeField] private GameObject gaugePanel;

        [Tooltip("채움 게이지. (Filled · Radial 360 · 위에서 시작 · 반시계)")]
        [SerializeField] private Image gaugeFill;

        [Tooltip("탄 한 발이 줄어드는 데 걸리는 시간(초). 이 속도로 일정하게 줄어든다.")]
        [SerializeField] private float drainDurationPerRound = 0.15f;

        [SerializeField] private Color readyColor = new(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color cooldownColor = new(1f, 0.55f, 0.2f, 0.9f);
        [SerializeField] private Color reloadColor = new(0.45f, 0.75f, 1f, 0.9f);

        private HudContext context;
        private float displayedFill = 1f;
        private bool wasReloading;
        private float reloadTotalTime;
        private float reloadStartFill;

        /// <summary>
        /// 로컬 아바타가 바뀌어도 따라가도록 아바타 대신 추적기를 담은 공용 정보를 기억한다.
        /// </summary>
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
        }

        /// <summary>
        /// 네트워크 준비 전에는 탄 정보가 없으므로 빈 게이지가 보이지 않게 숨긴다.
        /// </summary>
        public void Hide()
        {
            gaugePanel.SetActive(false);
        }

        /// <summary>탄 정보가 있을 때만 보이고, 다음 아바타가 빈 게이지로 시작하지 않게 숨길 때 채운다.</summary>
        public void Refresh(GameState state)
        {
            bool isInMatch = state is GameState.Preparation or GameState.Playing or GameState.Hunting;
            IAmmoSource ammo = context.LocalPlayer.Ammo;
            bool isVisible = isInMatch && ammo != null;
            gaugePanel.SetActive(isVisible);
            if (!isVisible)
            {
                displayedFill = 1f;
                wasReloading = false;
                return;
            }

            int magazineSize = Mathf.Max(1, ammo.MagazineSize);
            float reloadRemaining = ammo.ReloadRemaining;
            float targetFill = reloadRemaining > 0f ? GetReloadFill(ammo, magazineSize, reloadRemaining) : (float)ammo.Ammo / magazineSize;
            wasReloading = reloadRemaining > 0f;

            displayedFill = MoveFill(displayedFill, targetFill, magazineSize);
            gaugeFill.fillAmount = displayedFill;
            gaugeFill.color = GetColor(ammo, reloadRemaining);
        }

        /// <summary>
        /// 장전 진행 비율은 남은 시간만 알려 주므로, 장전이 시작된 순간의 전체 시간과 채움을 기억해 그 사이를 보간한다.
        /// </summary>
        private float GetReloadFill(IAmmoSource ammo, int magazineSize, float reloadRemaining)
        {
            if (!wasReloading)
            {
                reloadTotalTime = reloadRemaining;
                reloadStartFill = (float)ammo.Ammo / magazineSize;
            }

            float reloadProgress = reloadTotalTime > 0f ? 1f - reloadRemaining / reloadTotalTime : 1f;
            return Mathf.Lerp(reloadStartFill, 1f, Mathf.Clamp01(reloadProgress));
        }

        /// <summary>연사할 때 게이지가 뚝뚝 끊기지 않도록 일정한 속도로 줄인다.</summary>
        private float MoveFill(float currentFill, float targetFill, int magazineSize)
        {
            if (targetFill >= currentFill)
                return targetFill;

            float drainSpeed = drainDurationPerRound > 0f ? 1f / (magazineSize * drainDurationPerRound) : float.PositiveInfinity;
            return Mathf.MoveTowards(currentFill, targetFill, drainSpeed * Time.deltaTime);
        }

        /// <summary>지금 쏠 수 있는지 바로 알도록 상태별로 색을 다르게 한다.</summary>
        private Color GetColor(IAmmoSource ammo, float reloadRemaining)
        {
            if (reloadRemaining > 0f)
                return reloadColor;
            return ammo.FireCooldownRemaining > 0f ? cooldownColor : readyColor;
        }
    }
}
