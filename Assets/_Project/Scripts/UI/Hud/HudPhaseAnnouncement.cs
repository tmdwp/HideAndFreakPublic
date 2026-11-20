using GuardVsMannequin.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>판 시작과 헌트 타임처럼 할 일이 바뀌는 순간을 놓치지 않도록 화면 가운데에 잠깐 크게 알린다.</summary>
    public class HudPhaseAnnouncement : MonoBehaviour, IHudSection
    {
        [SerializeField] private GameObject announcementPanel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text titleText;
        [SerializeField] private Text subtitleText;

        [Tooltip("알림이 보이는 시간(초). 끝부분은 서서히 사라진다.")]
        [SerializeField] private float showDuration = 3.2f;

        [Tooltip("사라지는 데 걸리는 시간(초).")]
        [SerializeField] private float fadeDuration = 0.6f;

        [Tooltip("단계가 바뀐 뒤 내 아바타 역할을 기다리는 최대 시간(초). 넘으면 역할 문구 없이 띄운다.")]
        [SerializeField] private float roleWaitTimeout = 1f;

        private HudContext context;
        private GameState observedState = GameState.Waiting;
        private GameState pendingState;
        private bool isPending;
        private float pendingSince;
        private float shownAt = float.NegativeInfinity;

        /// <summary>
        /// 매 프레임 씬에서 찾지 않도록 공용 정보를 기억한다.
        /// </summary>
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
        }

        /// <summary>
        /// 네트워크 준비 전에는 단계를 알 수 없으므로 숨기고, 다시 연결되면 처음부터 지켜본다.
        /// </summary>
        public void Hide()
        {
            announcementPanel.SetActive(false);
            observedState = GameState.Waiting;
            isPending = false;
        }

        /// <summary>단계 전환 알림 신호가 따로 없어 매 프레임 상태를 비교하고, 역할 스폰이 늦으면 잠깐 기다렸다가 띄운다.</summary>
        public void Refresh(GameState state)
        {
            if (state != observedState)
            {
                if (PhaseAnnouncementRules.ShouldAnnounce(observedState, state))
                {
                    pendingState = state;
                    isPending = true;
                    pendingSince = Time.unscaledTime;
                }
                else
                {
                    // 결과 화면 등으로 넘어가면 지난 알림이 남지 않게 지운다.
                    isPending = false;
                    shownAt = float.NegativeInfinity;
                }
                observedState = state;
            }

            if (isPending && (context.LocalPlayer.Player != null || Time.unscaledTime - pendingSince >= roleWaitTimeout))
                ShowNow();

            UpdateFade();
        }

        /// <summary>역할마다 할 일이 달라서 내 역할 문구를 함께 보여 준다.</summary>
        private void ShowNow()
        {
            isPending = false;
            shownAt = Time.unscaledTime;
            bool isHunting = pendingState == GameState.Hunting;
            PlayerRole role = context.LocalPlayer.Player != null ? context.LocalPlayer.Player.Role : PlayerRole.None;

            titleText.text = HudText.GetPhaseName(pendingState);
            string subtitle = role == PlayerRole.None ? string.Empty : HudText.GetRoleText(role, HudText.AnnouncePart, isHunting);
            subtitleText.text = subtitle;
            subtitleText.gameObject.SetActive(subtitle.Length > 0);
        }

        /// <summary>
        /// 갑자기 사라지면 깜빡인 것처럼 보여 끝부분만 서서히 투명하게 한다.
        /// </summary>
        private void UpdateFade()
        {
            float elapsed = Time.unscaledTime - shownAt;
            bool isVisible = elapsed >= 0f && elapsed < showDuration;
            announcementPanel.SetActive(isVisible);
            if (!isVisible || canvasGroup == null)
                return;

            float fadeStart = showDuration - fadeDuration;
            canvasGroup.alpha = elapsed <= fadeStart ? 1f : Mathf.Clamp01((showDuration - elapsed) / Mathf.Max(0.01f, fadeDuration));
        }
    }
}
