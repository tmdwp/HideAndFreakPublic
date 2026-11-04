using System.Text;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// HUD 결과 화면: 승패와 사유, 참가자별 결과, 자동 복귀 시간, (호스트) 룸으로 이동 버튼.
    /// </summary>
    public class HudResultPanel : MonoBehaviour, IHudSection
    {
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Text resultHeadlineText;
        [SerializeField] private Text resultParticipantsText;
        [SerializeField] private Text resultCountdownText;
        [SerializeField] private Button returnToRoomButton;

        private readonly StringBuilder builder = new();
        private HudContext context;
        private RefreshTimer refreshTimer;

        /// <summary>
        /// 룸으로 이동 버튼을 연결한다. (호스트만 가능, <see cref="IMatchControl"/>)
        /// </summary>
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
            returnToRoomButton.onClick.AddListener(() => context.MatchControl.ReturnToRoom());
        }

        /// <inheritdoc />
        public void Hide()
        {
            resultPanel.SetActive(false);
        }

        /// <inheritdoc />
        public void Refresh(GameState state)
        {
            bool visible = state == GameState.Ended;
            resultPanel.SetActive(visible);
            if (!visible)
            {
                // 다시 보일 때 바로 최신 문구를 보여주도록 한다.
                refreshTimer.Reset();
                return;
            }
            // 결과·참가자 목록 문구를 새로 만드므로 일정 간격으로만 갱신한다.
            if (!refreshTimer.Tick())
                return;

            resultHeadlineText.text = GetResultHeadline();

            builder.Clear();
            foreach (ParticipantResult participant in context.MatchResult.Participants)
            {
                string status = Loc.Get(participant.survived ? "result.survived" : "result.eliminated");
                builder.AppendLine($"{DisplayTextSanitizer.Clean(participant.displayName, DisplayTextSanitizer.MaxPlayerNameLength)}  ·  {HudText.GetRoleName(participant.role)}  ·  {status}");
            }
            resultParticipantsText.text = builder.ToString();

            resultCountdownText.text = Loc.Format("result.countdown", Mathf.CeilToInt(context.State.RemainingTime));
            returnToRoomButton.gameObject.SetActive(context.MatchControl.CanControlMatch);
        }

        /// <summary>
        /// 승리 진영, 사유, 나의 승패를 한 문단으로 만든다.
        /// </summary>
        private string GetResultHeadline()
        {
            MatchResult matchResult = context.MatchResult;
            string winner = Loc.Get(matchResult.Winner switch
            {
                MatchWinner.Guard => "result.guardWin",
                MatchWinner.Mannequin => "result.mannequinWin",
                _ => "result.draw"
            });

            string reasonKey = matchResult.Reason switch
            {
                WinReason.AllMannequinsEliminated => "result.reason.allMannequins",
                WinReason.ObjectiveFailed => "result.reason.objectiveFailed",
                WinReason.GuardsSurvived => "result.reason.guardsSurvived",
                WinReason.AllGuardsEliminated => "result.reason.allGuards",
                _ => string.Empty
            };
            string reason = Loc.Get(reasonKey);

            PlayerRole localRole = context.LocalPlayer.LastRole;
            bool isWinner = RoleDefinitions.Get(localRole).IsWinner(matchResult.Winner);
            string personal = localRole == PlayerRole.None ? string.Empty : "\n" + Loc.Get(isWinner ? "result.victory" : "result.defeat");

            return $"{winner}\n{reason}{personal}";
        }
    }
}
