using GuardVsMannequin.Localization;
using GuardVsMannequin.Network;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>Steam 방의 방 코드 표시·복사·친구 초대 버튼.</summary>
    public class RoomCodeView : MonoBehaviour
    {
        [SerializeField] private Text roomCodeText;
        [SerializeField] private Button copyCodeButton;
        [SerializeField] private Button inviteButton;

        private IRoomNotice notice;

        // 바뀔 때만 문구를 다시 만들려고 기억하는 마지막 방 코드와 언어.
        private string shownCode;
        private string shownLanguage;
        private bool shownIsPublic;

        /// <summary>
        /// 이 화면이 하단 상태 줄을 직접 몰라도 되도록 복사 안내는 같은 캔버스의 <see cref="IRoomNotice"/>로 띄운다.
        /// </summary>
        private void Start()
        {
            notice = UiLookup.FindInParent<IRoomNotice>(this);
            copyCodeButton.onClick.AddListener(CopyRoomCode);
            inviteButton.onClick.AddListener(() => SessionServices.Session?.OpenInviteDialog());
        }

        /// <summary>방 코드 변경 알림이 없어 매 프레임 확인하되, 가비지를 줄이려고 바뀔 때만 문구를 다시 만든다.</summary>
        private void Update()
        {
            ISessionService session = SessionServices.Session;
            bool isSteamRoom = session != null && session.IsSteamRoom;

            string roomCode = session?.RoomCode;
            bool isPublic = session != null && session.IsPublicRoom;
            if (roomCode != shownCode || Loc.CurrentLanguage != shownLanguage || isPublic != shownIsPublic)
            {
                shownCode = roomCode;
                shownLanguage = Loc.CurrentLanguage;
                shownIsPublic = isPublic;
                roomCodeText.text = session != null ? DescribeRoom(roomCode, isSteamRoom, isPublic) : string.Empty;
            }
            copyCodeButton.interactable = isSteamRoom;
            inviteButton.interactable = isSteamRoom;
        }

        /// <summary>
        /// 호스트가 누가 들어올 수 있는지 확인할 수 있도록 Steam 방이면 공개 범위를 함께 보여 준다.
        /// </summary>
        private static string DescribeRoom(string roomCode, bool isSteamRoom, bool isPublic)
        {
            string codeText = Loc.Format("room.code", roomCode);
            return isSteamRoom ? $"{codeText}  ({Loc.Get(isPublic ? "room.visibility.public" : "room.visibility.friends")})" : codeText;
        }

        /// <summary>
        /// 친구에게 메신저로 보내기 쉽도록 클립보드에 복사하고, 복사됐는지 알 수 있게 안내를 띄운다.
        /// </summary>
        private void CopyRoomCode()
        {
            ISessionService session = SessionServices.Session;
            if (session == null || !session.IsSteamRoom)
                return;

            GUIUtility.systemCopyBuffer = session.RoomCode;
            notice?.ShowNotice(Loc.Get("room.status.codeCopied"));
        }
    }
}
