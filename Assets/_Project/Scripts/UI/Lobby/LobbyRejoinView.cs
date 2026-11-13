using GuardVsMannequin.Localization;
using GuardVsMannequin.Network;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 게임 중 끊겼을 때 로비에 보이는 "다시 참가" 버튼과 남은 시간, 포기 버튼.
    /// </summary>
    public class LobbyRejoinView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button rejoinButton;
        [SerializeField] private Text rejoinLabel;
        [SerializeField] private Button discardButton;

        private RefreshTimer refreshTimer;

        /// <summary>
        /// 다시 참가할 방이 없을 때 버튼이 잠깐이라도 보이지 않도록 숨긴 채 시작한다.
        /// </summary>
        private void Start()
        {
            rejoinButton.onClick.AddListener(() => SessionServices.Session?.Rejoin());
            discardButton.onClick.AddListener(() => SessionServices.Session?.DiscardRejoin());
            root.SetActive(false);
        }

        /// <summary>남은 시간은 초 단위로만 바뀌므로 가비지를 줄이려고 일정 간격으로만 갱신한다.</summary>
        private void Update()
        {
            if (!refreshTimer.Tick())
                return;

            ISessionService session = SessionServices.Session;
            float secondsLeft = session?.RejoinSecondsLeft ?? 0f;
            root.SetActive(secondsLeft > 0f);
            if (secondsLeft <= 0f)
                return;

            rejoinButton.interactable = !session.IsBusy;
            rejoinLabel.text = Loc.Format("lobby.rejoin.button", Mathf.CeilToInt(secondsLeft));
        }
    }
}
