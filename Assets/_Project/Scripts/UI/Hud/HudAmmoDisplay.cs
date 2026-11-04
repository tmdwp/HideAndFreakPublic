using GuardVsMannequin.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 조준점 둘레의 원형 게이지로 남은 탄과 장전 진행을 보여준다.
    /// 게이지는 위에서 시작해 반시계 방향으로 채워지므로, 탄을 쓰면 빈 곳이 시계 방향으로 일정한 속도로 늘고
    /// 장전하면 장전 진행만큼 반시계 방향으로 다시 찬다. 사격 후 발사 간격 동안·장전 중에는 색을 바꾼다.
    /// </summary>
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
        private float reloadTotal;
        private float reloadStartFill;

        /// <inheritdoc />
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
        }

        /// <inheritdoc />
        public void Hide()
        {
            gaugePanel.SetActive(false);
        }

        /// <inheritdoc />
        public void Refresh(GameState state)
        {
            bool isInMatch = state is GameState.Preparation or GameState.Playing or GameState.Hunting;
            IAmmoSource ammo = context.LocalPlayer.Ammo;
            bool visible = isInMatch && ammo != null;
            gaugePanel.SetActive(visible);
            if (!visible)
            {
                // 다음 판·다음 아바타는 가득 찬 게이지로 시작한다.
                displayedFill = 1f;
                wasReloading = false;
                return;
            }

            int size = Mathf.Max(1, ammo.MagazineSize);
            float reloadRemaining = ammo.ReloadRemaining;
            float target = reloadRemaining > 0f ? GetReloadFill(ammo, size, reloadRemaining) : (float)ammo.Ammo / size;
            wasReloading = reloadRemaining > 0f;

            displayedFill = MoveFill(displayedFill, target, size);
            gaugeFill.fillAmount = displayedFill;
            gaugeFill.color = GetColor(ammo, reloadRemaining);
        }

        /// <summary>
        /// 장전 중 목표 채움: 장전을 시작할 때의 남은 탄 비율에서 장전 진행만큼 가득 참 쪽으로 늘린다.
        /// </summary>
        private float GetReloadFill(IAmmoSource ammo, int size, float reloadRemaining)
        {
            if (!wasReloading)
            {
                reloadTotal = reloadRemaining;
                reloadStartFill = (float)ammo.Ammo / size;
            }

            float progress = reloadTotal > 0f ? 1f - reloadRemaining / reloadTotal : 1f;
            return Mathf.Lerp(reloadStartFill, 1f, Mathf.Clamp01(progress));
        }

        /// <summary>
        /// 줄어들 때는 한 발당 <see cref="drainDurationPerRound"/> 속도로 일정하게 줄이고, 늘어날 때(장전)는 바로 따라간다.
        /// </summary>
        private float MoveFill(float current, float target, int size)
        {
            if (target >= current)
                return target;

            float drainSpeed = drainDurationPerRound > 0f ? 1f / (size * drainDurationPerRound) : float.PositiveInfinity;
            return Mathf.MoveTowards(current, target, drainSpeed * Time.deltaTime);
        }

        /// <summary>
        /// 장전 중 / 사격 후 발사 간격 중 / 쏠 수 있음에 따라 게이지 색을 고른다.
        /// </summary>
        private Color GetColor(IAmmoSource ammo, float reloadRemaining)
        {
            if (reloadRemaining > 0f)
                return reloadColor;
            return ammo.FireCooldownRemaining > 0f ? cooldownColor : readyColor;
        }
    }
}
