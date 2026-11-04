using GuardVsMannequin.Core;
using GuardVsMannequin.Player;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 체력 바 위: 수류탄(섬광)에 맞아 위치가 드러난 동안(<see cref="IStatusEffects.IsRevealed"/>) 눈 아이콘을 깜빡이며 보여준다.
    /// </summary>
    public class HudRevealIndicator : MonoBehaviour, IHudSection
    {
        [SerializeField] private GameObject indicator;
        [SerializeField] private Graphic icon;
        [SerializeField] private Color color = new(1f, 0.35f, 0.3f, 1f);

        [Tooltip("깜빡임 속도(초당 횟수). 0이면 깜빡이지 않는다.")]
        [SerializeField] private float blinkRate = 2f;

        [Tooltip("깜빡일 때 가장 흐린 투명도.")]
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.35f;

        private HudContext context;

        /// <inheritdoc />
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
        }

        /// <inheritdoc />
        public void Hide()
        {
            indicator.SetActive(false);
        }

        /// <inheritdoc />
        public void Refresh(GameState state)
        {
            IStatusEffects effects = context.LocalPlayer.StatusEffects;
            bool visible = state != GameState.Waiting && context.LocalPlayer.Player != null && effects != null && effects.IsRevealed;
            indicator.SetActive(visible);
            if (!visible)
                return;

            float pulse = blinkRate > 0f ? 0.5f + 0.5f * Mathf.Cos(Time.time * blinkRate * Mathf.PI * 2f) : 1f;
            Color current = color;
            current.a = color.a * Mathf.Lerp(minAlpha, 1f, pulse);
            icon.color = current;
        }
    }
}
