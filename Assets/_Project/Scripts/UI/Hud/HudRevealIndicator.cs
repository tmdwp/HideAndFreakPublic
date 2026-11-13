using GuardVsMannequin.Core;
using GuardVsMannequin.Player;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>수류탄에 맞아 위치가 드러난 동안 눈 아이콘을 깜빡여 알린다.</summary>
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

        /// <summary>
        /// 로컬 아바타가 바뀌어도 따라가도록 아바타 대신 추적기를 담은 공용 정보를 기억한다.
        /// </summary>
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
        }

        /// <summary>
        /// 네트워크 준비 전에는 효과 정보가 없으므로 숨긴다.
        /// </summary>
        public void Hide()
        {
            indicator.SetActive(false);
        }

        /// <summary>
        /// 들킨 상태는 놓치면 위험하므로 정지 아이콘보다 눈에 띄게 투명도를 깜빡인다.
        /// </summary>
        public void Refresh(GameState state)
        {
            IStatusEffects effects = context.LocalPlayer.StatusEffects;
            bool isVisible = state != GameState.Waiting && context.LocalPlayer.Player != null && effects != null && effects.IsRevealed;
            indicator.SetActive(isVisible);
            if (!isVisible)
                return;

            float pulse = blinkRate > 0f ? 0.5f + 0.5f * Mathf.Cos(Time.time * blinkRate * Mathf.PI * 2f) : 1f;
            Color blinkColor = color;
            blinkColor.a = color.a * Mathf.Lerp(minAlpha, 1f, pulse);
            icon.color = blinkColor;
        }
    }
}
