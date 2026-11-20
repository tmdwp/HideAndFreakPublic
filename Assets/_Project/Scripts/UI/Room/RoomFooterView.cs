using GuardVsMannequin.Localization;
using GuardVsMannequin.Network;
using GuardVsMannequin.Room;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>호스트에게는 시작, 게스트에게는 준비 버튼을 보여 주는 룸 화면 하단.</summary>
    public class RoomFooterView : MonoBehaviour, IRoomNotice
    {
        /// <summary>안내 문구가 상태 문구를 대신하는 시간(초).</summary>
        private const float NoticeDuration = 2f;

        [SerializeField] private Button primaryButton;
        [SerializeField] private Text primaryButtonLabel;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Text statusText;

        private string notice;
        private float noticeEndTime;
        private RefreshTimer refreshTimer;

        /// <summary>
        /// 버튼 하나를 호스트와 게스트가 다르게 쓰므로, 누를 때 역할을 보고 동작을 고르는 함수에 연결한다.
        /// </summary>
        private void Start()
        {
            primaryButton.onClick.AddListener(OnPrimaryClicked);
            leaveButton.onClick.AddListener(RoomClient.RequestLeave);
        }

        /// <summary>호스트 알림은 놓치지 않도록 매 프레임 꺼내고, 문구는 가비지를 줄이려고 일정 간격으로만 갱신한다.</summary>
        private void Update()
        {
            RoomState room = RoomState.Instance;
            if (room != null && room.TryTakeHostNotice(out string hostNotice))
                ShowNotice(hostNotice);

            if (!refreshTimer.Tick())
                return;

            if (room == null)
            {
                statusText.text = Loc.Get("room.status.loading");
                primaryButton.interactable = false;
                return;
            }

            string status = RoomClient.IsHost ? RefreshAsHost(room) : RefreshAsGuest(room);
            statusText.text = Time.unscaledTime < noticeEndTime ? notice : status;
        }

        /// <summary>
        /// 다음 갱신까지 기다리지 않고 바로 보이도록 즉시 표시하고 타이머를 초기화한다.
        /// </summary>
        public void ShowNotice(string message)
        {
            notice = message;
            noticeEndTime = Time.unscaledTime + NoticeDuration;
            statusText.text = message;
            refreshTimer.Reset();
        }

        /// <summary>
        /// 호스트가 왜 시작할 수 없는지 알 수 있도록 시작 불가 이유를 상태 문구로 돌려준다.
        /// </summary>
        private string RefreshAsHost(RoomState room)
        {
            bool canStart = room.CanStartGame(out string reason);
            primaryButtonLabel.text = Loc.Get("room.start");
            primaryButton.interactable = canStart;
            if (!canStart)
                return reason;
            // 그동안 설치했을 수 있어 모드가 없는 게스트가 있어도 누를 수 있고 누를 때 다시 확인한다.
            return Loc.Get(room.HasMembersMissingMods ? "room.status.guestsMissingMods" : "room.status.allReady");
        }

        /// <summary>준비를 되돌릴 수 있도록 버튼을 항상 켜 두고, 모드가 빠졌으면 그 안내를 먼저 보여 준다.</summary>
        private string RefreshAsGuest(RoomState room)
        {
            bool isReady = RoomClient.IsLocalReady(room);
            primaryButtonLabel.text = Loc.Get(isReady ? "room.unready" : "room.ready");
            primaryButton.interactable = true;
            if (room.LocalMissingMods.Count > 0)
                return Loc.Get("room.status.localMissingMods");
            return Loc.Get(isReady ? "room.status.waitingHost" : "room.status.pressReady");
        }

        /// <summary>
        /// 버튼을 누른 사이 조건이 바뀌었을 수 있어 호스트는 시작할 때 다시 확인하고, 게스트는 바뀐 준비 상태가 바로 보이게 타이머를 초기화한다.
        /// </summary>
        private void OnPrimaryClicked()
        {
            RoomState room = RoomState.Instance;
            if (room == null)
                return;

            if (RoomClient.IsHost)
            {
                if (!room.TryStartGame(out string reason))
                    ShowNotice(reason);
                return;
            }

            room.SetReadyServerRpc(!RoomClient.IsLocalReady(room));
            refreshTimer.Reset();
        }
    }
}
