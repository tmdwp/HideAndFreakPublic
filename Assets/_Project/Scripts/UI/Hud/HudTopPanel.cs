using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// HUD 상단 중앙: "단계 | 남은 시간(분:초)", 그 아래 "남은 마네킹 / 이번 판 마네킹".
    /// 단계 이름: 시작 준비 / 가드 타임(탐색) / 헌트 타임(사냥) / 종료.
    /// </summary>
    public class HudTopPanel : MonoBehaviour, IHudSection
    {
        [SerializeField] private GameObject topPanel;
        [SerializeField] private Text topText;

        private HudContext context;

        // 마지막으로 표시한 값. 하나라도 바뀔 때만 문구를 다시 만든다. (시간은 초 단위라 1초에 한 번)
        private GameState shownState = GameState.Waiting;
        private int shownSeconds = -1;
        private int shownAlive = -1;
        private int shownTotal = -1;
        private string shownLanguage;

        /// <inheritdoc />
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
        }

        /// <inheritdoc />
        public void Hide()
        {
            topPanel.SetActive(false);
        }

        /// <inheritdoc />
        public void Refresh(GameState state)
        {
            bool visible = state != GameState.Waiting;
            topPanel.SetActive(visible);
            if (!visible)
                return;

            int alive = context.PlayerRegistry.AliveMannequinCount;
            // 참가 인원이 아직 동기화되지 않았으면 생존 인원을 대신 쓴다.
            int total = Mathf.Max(alive, context.PlayerRegistry.MatchMannequinCount);
            float remaining = context.State.RemainingTime;
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, remaining));
            if (state == shownState && seconds == shownSeconds && alive == shownAlive && total == shownTotal && Loc.CurrentLanguage == shownLanguage)
                return;

            shownState = state;
            shownSeconds = seconds;
            shownAlive = alive;
            shownTotal = total;
            shownLanguage = Loc.CurrentLanguage;
            topText.text = $"{HudText.GetPhaseName(state)}  |  {HudText.FormatTime(remaining)}\n" +
                           Loc.Format("hud.remainingMannequins", alive, total);
        }
    }
}
