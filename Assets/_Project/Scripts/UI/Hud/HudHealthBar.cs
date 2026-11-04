using GuardVsMannequin.Core;
using GuardVsMannequin.Player;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// HUD 하단 중앙: 내 체력 게이지(글자 없음). 채움 막대의 오른쪽 앵커를 남은 체력 비율로 줄인다.
    /// 바로 아래에는 스태미나 막대(<see cref="HudStaminaBar"/>)가 놓인다. 값이 바뀔 때만 다시 그린다.
    /// </summary>
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
        private int shownMax = -1;

        /// <inheritdoc />
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
        }

        /// <inheritdoc />
        public void Hide()
        {
            healthPanel.SetActive(false);
        }

        /// <inheritdoc />
        public void Refresh(GameState state)
        {
            bool isInMatch = state is GameState.Preparation or GameState.Playing or GameState.Hunting;
            PlayerHealth health = context.LocalPlayer.Health;
            bool visible = isInMatch && health != null;
            healthPanel.SetActive(visible);
            if (!visible)
                return;

            int current = health.CurrentHealth;
            int max = Mathf.Max(1, health.MaxHealth);
            if (current == shownHealth && max == shownMax)
                return;

            float ratio = Mathf.Clamp01((float)current / max);
            fill.anchorMin = new Vector2(0f, fill.anchorMin.y);
            fill.anchorMax = new Vector2(ratio, fill.anchorMax.y);
            if (fillGraphic != null)
                fillGraphic.color = ratio <= lowThreshold ? lowColor : normalColor;

            shownHealth = current;
            shownMax = max;
        }
    }
}
