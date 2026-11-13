using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>HUD 상단에 단계·남은 시간·남은 마네킹을 보여 준다.</summary>
    public class HudTopPanel : MonoBehaviour, IHudSection
    {
        [SerializeField] private GameObject topPanel;
        [SerializeField] private Text topText;

        private HudContext context;

        // 바뀔 때만 문구를 다시 만들려고 기억하는 마지막 값.
        private GameState shownState = GameState.Waiting;
        private int shownSeconds = -1;
        private int shownAliveCount = -1;
        private int shownTotalCount = -1;
        private string shownLanguage;

        /// <summary>
        /// 매 프레임 씬에서 찾지 않도록 공용 정보를 기억한다.
        /// </summary>
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
        }

        /// <summary>
        /// 네트워크 준비 전에는 시간·인원 정보가 없으므로 빈 패널이 보이지 않게 숨긴다.
        /// </summary>
        public void Hide()
        {
            topPanel.SetActive(false);
        }

        /// <summary>대기 중에는 숨기고, 가비지를 줄이려고 값이 바뀔 때만 문구를 다시 만든다.</summary>
        public void Refresh(GameState state)
        {
            bool isVisible = state != GameState.Waiting;
            topPanel.SetActive(isVisible);
            if (!isVisible)
                return;

            int aliveCount = context.PlayerRegistry.AliveMannequinCount;
            // 참가 인원이 아직 동기화되지 않았으면 생존 인원을 대신 쓴다.
            int totalCount = Mathf.Max(aliveCount, context.PlayerRegistry.MatchMannequinCount);
            float remainingTime = context.State.RemainingTime;
            int remainingSeconds = Mathf.CeilToInt(Mathf.Max(0f, remainingTime));
            if (state == shownState && remainingSeconds == shownSeconds && aliveCount == shownAliveCount
                && totalCount == shownTotalCount && Loc.CurrentLanguage == shownLanguage)
                return;

            shownState = state;
            shownSeconds = remainingSeconds;
            shownAliveCount = aliveCount;
            shownTotalCount = totalCount;
            shownLanguage = Loc.CurrentLanguage;
            topText.text = $"{HudText.GetPhaseName(state)}  |  {HudText.FormatTime(remainingTime)}\n" +
                           Loc.Format("hud.remainingMannequins", aliveCount, totalCount);
        }
    }
}
