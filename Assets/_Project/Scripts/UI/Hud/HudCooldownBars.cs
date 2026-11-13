using System.Collections.Generic;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.UI
{
    /// <summary>쿨타임 게이지가 있는 도구마다 조준점 왼쪽에 세로 바를 보여 준다.</summary>
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

        /// <summary>
        /// 견본이 목록에 섞여 보이지 않도록 처음에 꺼 둔다.
        /// </summary>
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
            barTemplate.gameObject.SetActive(false);
        }

        /// <summary>
        /// 네트워크 준비 전에는 도구가 없으므로 숨긴다.
        /// </summary>
        public void Hide()
        {
            barsPanel.SetActive(false);
        }

        /// <summary>
        /// 아바타가 바뀌면 도구 목록이 새 배열이 되므로 참조가 달라졌을 때만 바를 다시 만든다.
        /// </summary>
        public void Refresh(GameState state)
        {
            bool isInMatch = state is GameState.Preparation or GameState.Playing or GameState.Hunting;
            IReadOnlyList<ITool> tools = context.LocalPlayer.Tools;
            if (!ReferenceEquals(tools, shownTools))
                RebuildBars(tools);

            bool isVisible = isInMatch && gauges.Count > 0;
            barsPanel.SetActive(isVisible);
            if (!isVisible)
                return;

            for (int i = 0; i < gauges.Count; i++)
            {
                ICooldownGauge gauge = gauges[i];
                bars[i].Show(gauge.CooldownProgress, GetColor(gauge));
            }
        }

        /// <summary>같은 쿨타임이 두 번 보이지 않도록 고리 도구는 빼고 바 수를 도구 수에 맞춘다.</summary>
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
                HudCooldownBarView lastBar = bars[bars.Count - 1];
                bars.RemoveAt(bars.Count - 1);
                Destroy(lastBar.gameObject);
            }
        }

        /// <summary>다 찼는데 쓸 수 없는 상황을 오해하지 않도록 잠김 색을 따로 둔다.</summary>
        private Color GetColor(ICooldownGauge gauge)
        {
            if (gauge.CooldownProgress < 1f)
                return chargingColor;
            return gauge.IsReady ? readyColor : lockedColor;
        }
    }
}
