using System.Collections.Generic;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>남은 인원 숫자만으로는 누가 잡혔는지 몰라 판 흐름을 놓치므로, 탈락 소식을 오른쪽 위에 잠깐 쌓아 보여 준다.</summary>
    public class HudEliminationFeed : MonoBehaviour, IHudSection
    {
        [SerializeField] private RectTransform feedPanel;
        [SerializeField] private Text rowTemplate;

        [Tooltip("한 번에 보이는 최대 줄 수. 넘치면 오래된 줄부터 지운다.")]
        [SerializeField] private int maxRows = 4;

        [Tooltip("한 줄이 보이는 시간(초).")]
        [SerializeField] private float rowDuration = 5f;

        private readonly List<(Text text, float shownAt)> rows = new();
        private HudContext context;

        /// <summary>
        /// 소식은 Refresh 밖에서 오므로 이벤트를 듣고, 복제할 줄 원본은 숨겨 둔다.
        /// </summary>
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
            rowTemplate.gameObject.SetActive(false);
            if (context.MatchResult != null)
                context.MatchResult.Eliminated += OnEliminated;
        }

        /// <summary>
        /// 결과 오브젝트가 HUD보다 오래 살 수 있어 끊지 않으면 파괴된 HUD를 부른다.
        /// </summary>
        private void OnDestroy()
        {
            if (context?.MatchResult != null)
                context.MatchResult.Eliminated -= OnEliminated;
        }

        /// <summary>
        /// 네트워크가 끊기면 지난 소식이 의미 없으므로 모두 지운다.
        /// </summary>
        public void Hide()
        {
            ClearRows();
            feedPanel.gameObject.SetActive(false);
        }

        /// <summary>판이 아닐 때는 숨기고, 오래된 줄은 시간이 지나면 지운다.</summary>
        public void Refresh(GameState state)
        {
            bool isInMatch = state is GameState.Playing or GameState.Hunting;
            if (!isInMatch)
                ClearRows();
            feedPanel.gameObject.SetActive(isInMatch && rows.Count > 0);

            for (int i = rows.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime - rows[i].shownAt < rowDuration)
                    continue;
                Destroy(rows[i].text.gameObject);
                rows.RemoveAt(i);
            }
        }

        /// <summary>새 소식이 위에 오도록 맨 앞에 넣고, 다른 사람이 정한 이름이 화면을 깨지 않게 리치 텍스트를 끈다.</summary>
        private void OnEliminated(EliminationNotice notice)
        {
            Text row = Instantiate(rowTemplate, feedPanel);
            row.supportRichText = false;
            row.text = Describe(notice);
            row.gameObject.SetActive(true);
            row.transform.SetSiblingIndex(rowTemplate.transform.GetSiblingIndex() + 1);
            rows.Add((row, Time.unscaledTime));

            while (rows.Count > Mathf.Max(1, maxRows))
            {
                Destroy(rows[0].text.gameObject);
                rows.RemoveAt(0);
            }
            feedPanel.gameObject.SetActive(true);
        }

        /// <summary>누가 살아 있는지 새지 않도록 소식에는 역할만 있어 역할 이름으로 문구를 만든다.</summary>
        private static string Describe(EliminationNotice notice)
        {
            return Loc.Format("hud.feed.eliminated", HudText.GetRoleName(notice.victimRole));
        }

        /// <summary>
        /// 판이 바뀌면 지난 소식이 남지 않도록 복제한 줄을 모두 지운다.
        /// </summary>
        private void ClearRows()
        {
            foreach ((Text text, float _) in rows)
            {
                if (text != null)
                    Destroy(text.gameObject);
            }
            rows.Clear();
        }
    }
}
