using GuardVsMannequin.Localization;
using GuardVsMannequin.Network;
using GuardVsMannequin.Room;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 룸 화면 하단: 시작/준비 버튼, 나가기 버튼, 상태 문구.
    /// 호스트는 시작 버튼(전원 준비 + 인원 조건 충족 시 활성화), 게스트는 준비 ↔ 준비 해제 버튼으로 표시한다.
    /// </summary>
    public class RoomFooterView : MonoBehaviour, IRoomNotice
    {
        /// <summary>안내 문구(<see cref="ShowNotice"/>)를 상태 문구 대신 보여주는 시간(초).</summary>
        private const float NoticeDuration = 2f;

        [SerializeField] private Button primaryButton;
        [SerializeField] private Text primaryButtonLabel;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Text statusText;

        private string notice;
        private float noticeUntil;
        private RefreshTimer refreshTimer;

        /// <summary>
        /// 버튼 동작을 연결한다.
        /// </summary>
        private void Start()
        {
            primaryButton.onClick.AddListener(OnPrimaryClicked);
            leaveButton.onClick.AddListener(() => SessionServices.Session?.Leave());
        }

        /// <summary>
        /// 방 상태에 맞게 버튼과 상태 문구를 갱신한다. (시작 불가 이유 문구를 새로 만들므로 일정 간격으로만 갱신)
        /// </summary>
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
            statusText.text = Time.unscaledTime < noticeUntil ? notice : status;
        }

        /// <inheritdoc />
        public void ShowNotice(string message)
        {
            notice = message;
            noticeUntil = Time.unscaledTime + NoticeDuration;
            statusText.text = message;
            refreshTimer.Reset();
        }

        /// <summary>
        /// 호스트: 시작 버튼. 시작할 수 없으면 그 이유를 상태 문구로 반환한다.
        /// </summary>
        private string RefreshAsHost(RoomState room)
        {
            bool canStart = room.CanStartGame(out string reason);
            primaryButtonLabel.text = Loc.Get("room.start");
            primaryButton.interactable = canStart;
            if (!canStart)
                return reason;
            // 모드가 없는 게스트가 있어도 누를 수 있다. 그동안 설치했을 수 있어 누를 때 다시 확인한다.
            return Loc.Get(room.HasMembersMissingMods ? "room.status.guestsMissingMods" : "room.status.allReady");
        }

        /// <summary>
        /// 게스트: 준비 / 준비 해제 버튼.
        /// </summary>
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
        /// 호스트는 게임을 시작하고, 게스트는 준비 상태를 바꾼다.
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
