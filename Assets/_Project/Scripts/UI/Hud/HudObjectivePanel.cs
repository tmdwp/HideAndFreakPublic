using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>할 일을 잊지 않도록 내 역할의 현재 목표와 남은 수를 보여 준다.</summary>
    public class HudObjectivePanel : MonoBehaviour, IHudSection
    {
        [SerializeField] private GameObject objectivePanel;
        [SerializeField] private Text goalText;
        [SerializeField] private Text detailText;

        private HudContext context;
        private RefreshTimer refreshTimer;

        /// <summary>
        /// 매 프레임 씬에서 찾지 않도록 공용 정보를 기억한다.
        /// </summary>
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
        }

        /// <summary>
        /// 네트워크 준비 전에는 역할을 몰라 목표를 보여 줄 수 없으므로 숨긴다.
        /// </summary>
        public void Hide()
        {
            objectivePanel.SetActive(false);
        }

        /// <summary>관전 중이면 할 일이 없어 숨기고, 가비지를 줄이려고 일정 간격으로만 갱신한다.</summary>
        public void Refresh(GameState state)
        {
            bool isInMatch = state is GameState.Preparation or GameState.Playing or GameState.Hunting;
            bool isVisible = isInMatch && context.LocalPlayer.Player != null;
            objectivePanel.SetActive(isVisible);
            if (!isVisible)
            {
                // 다시 보일 때 바로 최신 문구를 보여주도록 한다.
                refreshTimer.Reset();
                return;
            }
            if (!refreshTimer.Tick())
                return;

            PlayerRole role = context.LocalPlayer.Player.Role;
            bool isHunting = state == GameState.Hunting;
            goalText.text = HudText.GetRoleText(role, HudText.ObjectivePart, isHunting);
            detailText.text = GetDetail(RoleDefinitions.Get(role).GetObjectiveCounter(isHunting), state);
        }

        /// <summary>역할을 추가해도 고치지 않도록 셀 대상은 역할 정의로 고르고, 목표 0회 판은 사냥까지 남은 시간을 보여 준다.</summary>
        private string GetDetail(ObjectiveCounter counter, GameState state)
        {
            GameSettings settings = context.State.Settings;
            // 목표 0회 판: 헌트 타임 전까지는 누구에게나 남은 보호 시간을 보여준다.
            if (state != GameState.Hunting && HuntTiming.IsTimedHunt(settings))
                return GetTimedHuntText(settings, state);

            return counter switch
            {
                ObjectiveCounter.RemainingGenerators => Loc.Format("hud.objective.generatorsLeft",
                    Mathf.Max(0, context.ObjectiveTracker.RequiredCount - context.ObjectiveTracker.DestroyedCount)),
                ObjectiveCounter.RemainingGuards => Loc.Format("hud.objective.guardsLeft", context.PlayerRegistry.AliveGuardCount),
                ObjectiveCounter.RemainingMannequins => Loc.Format("hud.objective.mannequinsLeft", context.PlayerRegistry.AliveMannequinCount),
                _ => string.Empty
            };
        }

        /// <summary>
        /// 준비 단계에는 보호 시간이 아직 줄지 않으므로 설정값을, 진행 중에는 실제 남은 시간을 보여 준다.
        /// </summary>
        private string GetTimedHuntText(GameSettings settings, GameState state)
        {
            if (state == GameState.Preparation)
                return Loc.Format("hud.objective.none.preparation", settings.huntDelay.ToString("0"));

            float protectionRemaining = HuntTiming.GetProtectionRemaining(settings, context.State.RemainingTime);
            return Loc.Format("hud.objective.none.playing", Mathf.CeilToInt(Mathf.Max(0f, protectionRemaining)));
        }
    }
}
