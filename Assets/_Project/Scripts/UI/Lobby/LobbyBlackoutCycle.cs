using UnityEngine;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 로비 편의점 연출: 불이 켜져 있다가 몇 초마다 더듬거리며 나가고, 깜깜한 동안에만 구울이 나타난다.
    /// 정전마다 정해진 순서대로 다음 등장 장면을 켜서 점점 가까워지는 흐름을 만들고, 끝나면 처음으로 돌아간다.
    /// </summary>
    public class LobbyBlackoutCycle : MonoBehaviour
    {
        [Tooltip("가게 전기(조명·발광면).")]
        [SerializeField] private StorePower power;

        [Tooltip("정전마다 순서대로 하나씩 켤 구울 등장 장면(자리·자세를 미리 잡아 둔 오브젝트).")]
        [SerializeField] private GameObject[] appearances;

        [Tooltip("불이 켜져 있는 시간 범위(초).")]
        [SerializeField] private Vector2 litDuration = new Vector2(5f, 9f);

        [Tooltip("깜깜한 시간 범위(초).")]
        [SerializeField] private Vector2 darkDuration = new Vector2(1.4f, 2.4f);

        [Tooltip("꺼지기 직전·켜지기 직전에 더듬거리는 시간(초).")]
        [SerializeField] private float stutterDuration = 0.5f;

        [Tooltip("더듬거릴 때 한 번 켜지거나 꺼져 있는 시간 범위(초).")]
        [SerializeField] private Vector2 stutterStep = new Vector2(0.04f, 0.12f);

        [Tooltip("정전 중 남는 밝기 비율. 0이면 완전히 꺼진다.")]
        [Range(0f, 1f)]
        [SerializeField] private float darkLevel = 0f;

        private enum Phase { Lit, StutterOff, Dark, StutterOn }

        private Phase phase = Phase.Lit;
        private float phaseRemaining;
        private float stutterRemaining;
        private bool isStutterOn;
        private int nextAppearanceIndex;
        private GameObject shownAppearance;

        /// <summary>로비에 들어오자마자 구울이 보이지 않도록 모든 등장 장면을 끄고 불을 켠 상태로 시작한다.</summary>
        private void Start()
        {
            foreach (GameObject appearance in appearances ?? System.Array.Empty<GameObject>())
            {
                if (appearance != null)
                    appearance.SetActive(false);
            }
            EnterPhase(Phase.Lit);
        }

        /// <summary>코루틴 없이 단계 시간을 재고, 더듬거리는 동안은 짧은 간격으로 켜고 끈다.</summary>
        private void Update()
        {
            phaseRemaining -= Time.deltaTime;
            if (phase == Phase.StutterOff || phase == Phase.StutterOn)
                UpdateStutter();

            if (phaseRemaining <= 0f)
                EnterPhase(NextPhase(phase));
        }

        private static Phase NextPhase(Phase current) => current switch
        {
            Phase.Lit => Phase.StutterOff,
            Phase.StutterOff => Phase.Dark,
            Phase.Dark => Phase.StutterOn,
            _ => Phase.Lit
        };

        /// <summary>단계마다 길이를 정하고, 깜깜해질 때만 다음 등장 장면을 켠다.</summary>
        private void EnterPhase(Phase nextPhase)
        {
            phase = nextPhase;
            switch (phase)
            {
                case Phase.Lit:
                    phaseRemaining = Random.Range(litDuration.x, litDuration.y);
                    ApplyLevel(1f);
                    break;
                case Phase.Dark:
                    phaseRemaining = Random.Range(darkDuration.x, darkDuration.y);
                    ApplyLevel(darkLevel);
                    ShowNextAppearance();
                    break;
                default:
                    phaseRemaining = stutterDuration;
                    stutterRemaining = 0f;
                    isStutterOn = phase == Phase.StutterOff;
                    break;
            }

            // 다시 켜지기 시작하는 순간 사라져야, 밝아졌을 때 구울이 서 있는 장면이 보이지 않는다.
            if (phase != Phase.Dark)
                HideAppearance();
        }

        /// <summary>형광등이 나갈 때처럼 불규칙한 간격으로 켜짐·꺼짐을 바꾼다.</summary>
        private void UpdateStutter()
        {
            stutterRemaining -= Time.deltaTime;
            if (stutterRemaining > 0f)
                return;
            isStutterOn = !isStutterOn;
            stutterRemaining = Random.Range(stutterStep.x, stutterStep.y);
            ApplyLevel(isStutterOn ? 1f : darkLevel);
        }

        /// <summary>순서를 지켜야 문 → 진열대 → 코앞으로 다가오는 흐름이 읽히므로, 비어 있는 칸만 건너뛰고 차례대로 켠다.</summary>
        private void ShowNextAppearance()
        {
            if (appearances == null || appearances.Length == 0)
                return;

            for (int attempt = 0; attempt < appearances.Length; attempt++)
            {
                GameObject candidate = appearances[nextAppearanceIndex];
                nextAppearanceIndex = (nextAppearanceIndex + 1) % appearances.Length;
                if (candidate == null)
                    continue;
                shownAppearance = candidate;
                shownAppearance.SetActive(true);
                return;
            }
        }

        /// <summary>지금 켜진 등장 장면만 끈다.</summary>
        private void HideAppearance()
        {
            if (shownAppearance == null)
                return;
            shownAppearance.SetActive(false);
            shownAppearance = null;
        }

        /// <summary>전기를 비워 둬도 구울 연출은 돌도록 확인한다.</summary>
        private void ApplyLevel(float level)
        {
            if (power != null)
                power.SetLevel(level);
        }
    }
}
