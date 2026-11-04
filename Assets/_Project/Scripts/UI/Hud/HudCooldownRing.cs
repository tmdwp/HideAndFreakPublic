using System.Collections.Generic;
using GuardVsMannequin.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 조준점 둘레 원형 고리: 조준점 쿨타임 도구(<see cref="ICrosshairCooldown"/> — 구울 공격)의 쿨타임.
    /// 총 탄 게이지(<see cref="HudAmmoDisplay"/>)와 같은 모양으로, 사용하면 비었다가 쿨타임 동안 반시계 방향으로 다시 찬다.
    /// 다 찼어도 지금 쓸 수 없으면(사냥 시간 아님, 변신 중) 숨긴다.
    /// </summary>
    public class HudCooldownRing : MonoBehaviour, IHudSection
    {
        [SerializeField] private GameObject ringPanel;

        [Tooltip("채움 고리. (Filled · Radial 360 · 위에서 시작 · 반시계)")]
        [SerializeField] private Image ringFill;

        [SerializeField] private Color readyColor = new(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color chargingColor = new(1f, 0.55f, 0.2f, 0.9f);

        private HudContext context;

        /// <inheritdoc />
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
        }

        /// <inheritdoc />
        public void Hide()
        {
            ringPanel.SetActive(false);
        }

        /// <inheritdoc />
        public void Refresh(GameState state)
        {
            bool isInMatch = state is GameState.Preparation or GameState.Playing or GameState.Hunting;
            ICrosshairCooldown tool = FindTool(context.LocalPlayer.Tools);
            float progress = tool != null ? tool.CooldownProgress : 1f;
            bool isCharging = progress < 1f;
            bool visible = isInMatch && tool != null && (isCharging || tool.IsReady);
            ringPanel.SetActive(visible);
            if (!visible)
                return;

            ringFill.fillAmount = progress;
            ringFill.color = isCharging ? chargingColor : readyColor;
        }

        /// <summary>
        /// 로컬 아바타의 조준점 쿨타임 도구를 찾는다. 없으면 null.
        /// </summary>
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
