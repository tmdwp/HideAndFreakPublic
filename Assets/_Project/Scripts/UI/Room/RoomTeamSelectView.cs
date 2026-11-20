using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Room;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>시작할 팀(가드·무작위·마네킹)을 좌우 버튼으로 고르고, 지금 희망 인원과 가드 자리 수를 보여 주는 룸 화면 영역.</summary>
    public class RoomTeamSelectView : MonoBehaviour
    {
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [Tooltip("지금 고른 팀 이름을 보여 줄 칸. 바탕 그림이 있으면 팀마다 색을 바꾼다.")]
        [SerializeField] private Text choiceText;
        [SerializeField] private Graphic choiceBackground;
        [SerializeField] private Text summaryText;

        [SerializeField] private Color guardColor = new(0.20f, 0.42f, 0.70f, 1f);
        [SerializeField] private Color mannequinColor = new(0.70f, 0.25f, 0.22f, 1f);
        [SerializeField] private Color randomColor = new(0.22f, 0.25f, 0.30f, 1f);

        // 무작위를 가운데 두어 어느 쪽 팀이든 한 번만 눌러 닿게 한다.
        private static readonly TeamPreference[] choiceOrder = { TeamPreference.Guard, TeamPreference.Random, TeamPreference.Mannequin };

        // 서버 확인이 오기 전에 갱신되면 고르기 전 값으로 잠깐 되돌아가 보이므로, 보낸 값을 이 시간(초)만큼 우선해 보여 준다.
        private const float PendingHoldSeconds = 1f;

        private RefreshTimer refreshTimer;
        private TeamPreference shownPreference = TeamPreference.Random;
        private bool hasPendingChoice;
        private float pendingUntil;

        /// <summary>좌우 버튼을 연결하되, 예전 프리팹이라 칸이 비어 있으면 매 프레임 오류가 나지 않게 끈다.</summary>
        private void Start()
        {
            if (previousButton == null || nextButton == null || choiceText == null)
            {
                Debug.LogWarning("[RoomTeamSelectView] 팀 선택 좌우 버튼이 연결되지 않았습니다. 'UI/빠진 UI 영역·키 줄 추가'를 실행하세요.");
                enabled = false;
                return;
            }
            previousButton.onClick.AddListener(() => Step(-1));
            nextButton.onClick.AddListener(() => Step(1));
        }

        /// <summary>다른 사람의 선택은 알림 없이 바뀌므로 일정 간격으로 다시 센다.</summary>
        private void Update()
        {
            RoomState room = RoomState.Instance;
            if (room == null || !refreshTimer.Tick())
                return;

            TeamPreference serverPreference = RoomClient.TryGetLocalMember(room, out RoomMember member) ? member.teamPreference : TeamPreference.Random;
            if (hasPendingChoice && (serverPreference == shownPreference || Time.unscaledTime >= pendingUntil))
                hasPendingChoice = false;
            if (!hasPendingChoice)
                shownPreference = serverPreference;
            ShowChoice();

            bool canChoose = !room.IsInGame;
            previousButton.interactable = canChoose;
            nextButton.interactable = canChoose;

            summaryText.text = BuildSummary(room);
        }

        /// <summary>끝에서 반대쪽 끝으로 넘어가게 해 버튼 하나로도 모든 팀을 고를 수 있게 한다.</summary>
        private void Step(int direction)
        {
            RoomState room = RoomState.Instance;
            if (room == null || room.IsInGame)
                return;

            int index = System.Array.IndexOf(choiceOrder, shownPreference);
            if (index < 0)
                index = System.Array.IndexOf(choiceOrder, TeamPreference.Random);
            TeamPreference next = choiceOrder[(index + direction + choiceOrder.Length) % choiceOrder.Length];

            shownPreference = next;
            hasPendingChoice = true;
            pendingUntil = Time.unscaledTime + PendingHoldSeconds;
            ShowChoice();

            room.SetTeamPreferenceServerRpc(next);
            refreshTimer.Reset();
        }

        /// <summary>언어가 바뀌어도 맞도록 매번 문구를 찾고, 고른 팀을 한눈에 알도록 색도 바꾼다.</summary>
        private void ShowChoice()
        {
            choiceText.text = Loc.Get(GetLabelKey(shownPreference));
            if (choiceBackground != null)
                choiceBackground.color = GetColor(shownPreference);
        }

        /// <summary>팀 이름은 기존 버튼 문구를 그대로 쓴다.</summary>
        private static string GetLabelKey(TeamPreference preference)
        {
            return preference switch
            {
                TeamPreference.Guard => "room.team.guard",
                TeamPreference.Mannequin => "room.team.mannequin",
                _ => "room.team.random"
            };
        }

        /// <summary>
        /// 팀마다 다른 색으로 지금 고른 팀을 구분한다.
        /// </summary>
        private Color GetColor(TeamPreference preference)
        {
            return preference switch
            {
                TeamPreference.Guard => guardColor,
                TeamPreference.Mannequin => mannequinColor,
                _ => randomColor
            };
        }

        /// <summary>
        /// 희망이 다 이뤄지지 않을 수 있음을 미리 알도록 희망 인원과 가드 자리 수를 함께 보여 준다.
        /// </summary>
        private static string BuildSummary(RoomState room)
        {
            int guardWanted = 0, mannequinWanted = 0, noPreference = 0;
            foreach (RoomMember member in room.Members.Values)
            {
                switch (member.teamPreference)
                {
                    case TeamPreference.Guard:
                        guardWanted++;
                        break;
                    case TeamPreference.Mannequin:
                        mannequinWanted++;
                        break;
                    default:
                        noPreference++;
                        break;
                }
            }

            string slots = Loc.Format("room.team.slots", room.Settings.guardCount, room.Members.Count);
            string counts = Loc.Format("room.team.counts", guardWanted, mannequinWanted, noPreference);
            return $"{slots}\n{counts}\n\n{Loc.Get("room.team.hint")}";
        }
    }
}
