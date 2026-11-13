using System.Collections.Generic;
using GuardVsMannequin.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>조준과 함께 쓰는 도구의 쿨타임을 조준점 둘레 고리로 보여 준다.</summary>
    public class HudCooldownRing : MonoBehaviour, IHudSection
    {
        [SerializeField] private GameObject ringPanel;

        [Tooltip("채움 고리. (Filled · Radial 360 · 위에서 시작 · 반시계)")]
        [SerializeField] private Image ringFill;

        [SerializeField] private Color readyColor = new(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color chargingColor = new(1f, 0.55f, 0.2f, 0.9f);

        private HudContext context;

        /// <summary>
        /// 로컬 아바타가 바뀌어도 따라가도록 아바타 대신 추적기를 담은 공용 정보를 기억한다.
        /// </summary>
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
        }

        /// <summary>
        /// 네트워크 준비 전에는 도구가 없으므로 숨긴다.
        /// </summary>
        public void Hide()
        {
            ringPanel.SetActive(false);
        }

        /// <summary>
        /// 쓸 수 없는 단계에 가득 찬 고리가 보이면 공격할 수 있다고 오해하므로, 차는 중이거나 지금 쓸 수 있을 때만 보인다.
        /// </summary>
        public void Refresh(GameState state)
        {
            bool isInMatch = state is GameState.Preparation or GameState.Playing or GameState.Hunting;
            ICrosshairCooldown tool = FindTool(context.LocalPlayer.Tools);
            float cooldownProgress = tool != null ? tool.CooldownProgress : 1f;
            bool isCharging = cooldownProgress < 1f;
            bool isVisible = isInMatch && tool != null && (isCharging || tool.IsReady);
            ringPanel.SetActive(isVisible);
            if (!isVisible)
                return;

            ringFill.fillAmount = cooldownProgress;
            ringFill.color = isCharging ? chargingColor : readyColor;
        }

        /// <summary>역할마다 도구 구성이 달라 인터페이스로 찾고 첫 도구만 쓴다.</summary>
        private static ICrosshairCooldown FindTool(IReadOnlyList<ITool> tools)
        {
            foreach (ITool tool in tools)
            {
                if (tool is ICrosshairCooldown crosshairTool)
                    return crosshairTool;
            }
            return null;
        }
    }
}
