using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// HUD 중앙 우측: 내 역할의 현재 목표와 그 아래 남은 수.
    /// - 가드: 포획(가드 타임) / 생존(헌트 타임)
    /// - 마네킹: 전원 장치 파괴 + 남은 전원 장치(가드 타임) / 사냥 + 남은 가드(헌트 타임)
    /// 무엇을 셀지는 <see cref="RoleDefinition.GetObjectiveCounter"/>가 정한다. (역할로 분기하지 않음)
    /// 목표 횟수가 0인 판은 남은 수 대신 헌트 타임까지 남은 시간을 보여준다.
    /// </summary>
    public class HudObjectivePanel : MonoBehaviour, IHudSection
    {
        [SerializeField] private GameObject objectivePanel;
        [SerializeField] private Text goalText;
        [SerializeField] private Text detailText;

        private HudContext context;
        private RefreshTimer refreshTimer;

        /// <inheritdoc />
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
        }

        /// <inheritdoc />
        public void Hide()
        {
            objectivePanel.SetActive(false);
        }

        /// <inheritdoc />
        public void Refresh(GameState state)
        {
            bool isInMatch = state is GameState.Preparation or GameState.Playing or GameState.Hunting;
            bool visible = isInMatch && context.LocalPlayer.Player != null;
            objectivePanel.SetActive(visible);
            if (!visible)
            {
                // 다시 보일 때 바로 최신 문구를 보여주도록 한다.
                refreshTimer.Reset();
                return;
            }
            // 남은 수·시간 문구를 새로 만드므로 일정 간격으로만 갱신한다.
            if (!refreshTimer.Tick())
                return;

            PlayerRole role = context.LocalPlayer.Player.Role;
            bool isHunting = state == GameState.Hunting;
            goalText.text = HudText.GetRoleText(role, HudText.ObjectivePart, isHunting);
            detailText.text = GetDetail(RoleDefinitions.Get(role).GetObjectiveCounter(isHunting), state);
        }

        /// <summary>
        /// 목표 아래 줄 문구를 만든다.
        /// </summary>
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
        /// 목표 0회 판에서 헌트 타임 시작까지의 안내 문구.
        /// </summary>
        private string GetTimedHuntText(GameSettings settings, GameState state)
        {
            if (state == GameState.Preparation)
                return Loc.Format("hud.objective.none.preparation", settings.huntDelay.ToString("0"));

            float remaining = HuntTiming.GetProtectionRemaining(settings, context.State.RemainingTime);
            return Loc.Format("hud.objective.none.playing", Mathf.CeilToInt(Mathf.Max(0f, remaining)));
        }
    }
}
