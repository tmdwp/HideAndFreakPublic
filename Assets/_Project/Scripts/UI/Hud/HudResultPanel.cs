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

        /// <summary>화면이 네트워크를 몰라도 되도록 룸 복귀는 인터페이스에 맡긴다.</summary>
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
            returnToRoomButton.onClick.AddListener(() => context.MatchControl.ReturnToRoom());
        }

        /// <summary>
        /// 네트워크 준비 전에는 결과가 없으므로 숨긴다.
        /// </summary>
        public void Hide()
        {
            resultPanel.SetActive(false);
        }

        /// <summary>가비지를 줄이려고 일정 간격으로만 갱신하고, 다른 사람이 정한 이름은 정리해서 쓴다.</summary>
        public void Refresh(GameState state)
        {
            bool isVisible = state == GameState.Ended;
            resultPanel.SetActive(isVisible);
            if (!isVisible)
            {
                // 다시 보일 때 바로 최신 문구를 보여주도록 한다.
                refreshTimer.Reset();
                return;
            }
            if (!refreshTimer.Tick())
                return;

            resultHeadlineText.text = GetResultHeadline();

            builder.Clear();
            foreach (ParticipantResult participant in context.MatchResult.Participants)
            {
                string statusLabel = Loc.Get(participant.survived ? "result.survived" : "result.eliminated");
                string participantName = DisplayTextSanitizer.Clean(participant.displayName, DisplayTextSanitizer.MaxPlayerNameLength);
                builder.AppendLine($"{participantName}  ·  {HudText.GetRoleName(participant.role)}  ·  {statusLabel}");
            }
            resultParticipantsText.text = builder.ToString();

            resultCountdownText.text = Loc.Format("result.countdown", Mathf.CeilToInt(context.State.RemainingTime));
            returnToRoomButton.gameObject.SetActive(context.MatchControl.CanControlMatch);
        }

        /// <summary>진영 승패만으로는 내가 이겼는지 알기 어려워 나의 승패를 덧붙인다.</summary>
        private string GetResultHeadline()
        {
            MatchResult matchResult = context.MatchResult;
            string winnerLabel = Loc.Get(matchResult.Winner switch
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
            string reasonLabel = Loc.Get(reasonKey);

            PlayerRole localRole = context.LocalPlayer.LastRole;
            bool isWinner = RoleDefinitions.Get(localRole).IsWinner(matchResult.Winner);
            string personalResult = localRole == PlayerRole.None ? string.Empty : "\n" + Loc.Get(isWinner ? "result.victory" : "result.defeat");

            return $"{winnerLabel}\n{reasonLabel}{personalResult}";
        }
    }
}
