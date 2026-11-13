using GuardVsMannequin.Core;
using GuardVsMannequin.Player;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>스프라이트 없이 앵커로 남은 스태미나를 보여 주고, 다시 달릴 수 없는 동안은 색을 바꾼다.</summary>
    public class HudStaminaBar : MonoBehaviour, IHudSection
    {
        [SerializeField] private GameObject staminaPanel;

        [Tooltip("채움 막대. 왼쪽 끝 기준으로 남은 비율만큼 늘어난다.")]
        [SerializeField] private RectTransform fill;

        [SerializeField] private Graphic fillGraphic;
        [SerializeField] private Color normalColor = new(0.95f, 0.8f, 0.3f, 1f);
        [SerializeField] private Color exhaustedColor = new(0.85f, 0.3f, 0.25f, 1f);

        private HudContext context;

        /// <summary>
        /// 로컬 아바타가 바뀌어도 따라가도록 아바타 대신 추적기를 담은 공용 정보를 기억한다.
        /// </summary>
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
        }

        /// <summary>
        /// 네트워크 준비 전에는 스태미나가 없으므로 빈 막대가 보이지 않게 숨긴다.
        /// </summary>
        public void Hide()
        {
            staminaPanel.SetActive(false);
        }

        /// <summary>판 진행 중이고 스태미나가 있을 때만 보인다.</summary>
        public void Refresh(GameState state)
        {
            bool isInMatch = state is GameState.Preparation or GameState.Playing or GameState.Hunting;
            IStaminaSource stamina = context.LocalPlayer.Stamina;
            bool isVisible = isInMatch && stamina != null;
            staminaPanel.SetActive(isVisible);
            if (!isVisible)
                return;

            fill.anchorMin = new Vector2(0f, fill.anchorMin.y);
            fill.anchorMax = new Vector2(Mathf.Clamp01(stamina.Stamina), fill.anchorMax.y);
            if (fillGraphic != null)
                fillGraphic.color = stamina.IsExhausted ? exhaustedColor : normalColor;
        }
    }
}
