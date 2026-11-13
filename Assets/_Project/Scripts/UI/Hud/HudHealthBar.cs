using GuardVsMannequin.Core;
using GuardVsMannequin.Player;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>스프라이트 없이 앵커로 남은 체력을 보여 주는 게이지.</summary>
    public class HudHealthBar : MonoBehaviour, IHudSection
    {
        [SerializeField] private GameObject healthPanel;

        [Tooltip("채움 막대. 왼쪽 끝 기준으로 남은 비율만큼 늘어난다.")]
        [SerializeField] private RectTransform fill;

        [SerializeField] private Graphic fillGraphic;
        [SerializeField] private Color normalColor = new(0.85f, 0.25f, 0.25f, 1f);

        [Tooltip("남은 체력 비율이 이 값 이하이면 위험 색으로 바꾼다.")]
        [SerializeField, Range(0f, 1f)] private float lowThreshold = 0.34f;

        [SerializeField] private Color lowColor = new(1f, 0.1f, 0.1f, 1f);

        private HudContext context;
        private int shownHealth = -1;
        private int shownMaxHealth = -1;

        /// <summary>
        /// 로컬 아바타가 바뀌어도 따라가도록 아바타 대신 추적기를 담은 공용 정보를 기억한다.
        /// </summary>
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
        }

        /// <summary>
        /// 네트워크 준비 전에는 체력 정보가 없으므로 빈 막대가 보이지 않게 숨긴다.
        /// </summary>
        public void Hide()
        {
            healthPanel.SetActive(false);
        }

        /// <summary>체력은 맞을 때만 바뀌므로 값이 같으면 다시 그리지 않는다.</summary>
        public void Refresh(GameState state)
        {
            bool isInMatch = state is GameState.Preparation or GameState.Playing or GameState.Hunting;
            PlayerHealth health = context.LocalPlayer.Health;
            bool isVisible = isInMatch && health != null;
            healthPanel.SetActive(isVisible);
            if (!isVisible)
                return;

            int currentHealth = health.CurrentHealth;
            int maxHealth = Mathf.Max(1, health.MaxHealth);
            if (currentHealth == shownHealth && maxHealth == shownMaxHealth)
                return;

            float healthRatio = Mathf.Clamp01((float)currentHealth / maxHealth);
            fill.anchorMin = new Vector2(0f, fill.anchorMin.y);
            fill.anchorMax = new Vector2(healthRatio, fill.anchorMax.y);
            if (fillGraphic != null)
                fillGraphic.color = healthRatio <= lowThreshold ? lowColor : normalColor;

            shownHealth = currentHealth;
            shownMaxHealth = maxHealth;
        }
    }
}
