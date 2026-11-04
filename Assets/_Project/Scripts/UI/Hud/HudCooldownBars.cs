using System.Collections.Generic;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 조준점 왼쪽: 쿨타임 게이지가 있는 도구(<see cref="ICooldownGauge"/> — 수류탄, 마네킹 변신)마다 세로 바를 하나씩 보여준다.
    /// 조준점 고리로 보여주는 도구(<see cref="ICrosshairCooldown"/>)는 <see cref="HudCooldownRing"/>가 맡으므로 뺀다.
    /// 사용하면 바가 바닥까지 줄었다가 쿨타임 동안 다시 차오른다. 다 찼어도 지금 쓸 수 없으면(출발 보호, 단계 제한) 잠김 색으로 보인다.
    /// </summary>
    public class HudCooldownBars : MonoBehaviour, IHudSection
    {
        [SerializeField] private GameObject barsPanel;

        [Tooltip("복제할 바 견본. (꺼 둔 상태로 둔다)")]
        [SerializeField] private HudCooldownBarView barTemplate;

        [SerializeField] private Color readyColor = new(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color chargingColor = new(1f, 0.55f, 0.2f, 0.9f);
        [SerializeField] private Color lockedColor = new(0.5f, 0.5f, 0.5f, 0.7f);

        private readonly List<ICooldownGauge> gauges = new();
        private readonly List<HudCooldownBarView> bars = new();
        private HudContext context;
        private IReadOnlyList<ITool> shownTools;

        /// <inheritdoc />
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
            barTemplate.gameObject.SetActive(false);
        }

        /// <inheritdoc />
        public void Hide()
        {
            barsPanel.SetActive(false);
        }

        /// <inheritdoc />
        public void Refresh(GameState state)
        {
            bool isInMatch = state is GameState.Preparation or GameState.Playing or GameState.Hunting;
            IReadOnlyList<ITool> tools = context.LocalPlayer.Tools;
            if (!ReferenceEquals(tools, shownTools))
                RebuildBars(tools);

            bool visible = isInMatch && gauges.Count > 0;
            barsPanel.SetActive(visible);
            if (!visible)
                return;

            for (int i = 0; i < gauges.Count; i++)
            {
                ICooldownGauge gauge = gauges[i];
                bars[i].Show(gauge.CooldownProgress, GetColor(gauge));
            }
        }

        /// <summary>
        /// 아바타가 바뀌면(스폰·역할 교체) 게이지 도구를 다시 모으고 바 수를 맞춘다.
        /// </summary>
        private void RebuildBars(IReadOnlyList<ITool> tools)
        {
            shownTools = tools;
            gauges.Clear();
            foreach (ITool tool in tools)
            {
                if (tool is ICooldownGauge gauge && tool is not ICrosshairCooldown)
                    gauges.Add(gauge);
            }

            while (bars.Count < gauges.Count)
            {
                HudCooldownBarView bar = Instantiate(barTemplate, barTemplate.transform.parent);
                bar.gameObject.SetActive(true);
                bars.Add(bar);
            }
            while (bars.Count > gauges.Count)
            {
                HudCooldownBarView last = bars[bars.Count - 1];
                bars.RemoveAt(bars.Count - 1);
                Destroy(last.gameObject);
            }
        }

        /// <summary>
        /// 차오르는 중 / 다 찼지만 쓸 수 없음 / 쓸 수 있음에 따라 색을 고른다.
        /// </summary>
        private Color GetColor(ICooldownGauge gauge)
        {
            if (gauge.CooldownProgress < 1f)
                return chargingColor;
            return gauge.IsReady ? readyColor : lockedColor;
        }
    }
}
