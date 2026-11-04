using GuardVsMannequin.Localization;
using GuardVsMannequin.Network;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 룸 화면의 방 코드 표시, 코드 복사, 친구 초대 버튼. (Steam 방에서만 사용 가능)
    /// </summary>
    public class RoomCodeView : MonoBehaviour
    {
        [SerializeField] private Text roomCodeText;
        [SerializeField] private Button copyCodeButton;
        [SerializeField] private Button inviteButton;

        private IRoomNotice notice;

        // 마지막으로 표시한 방 코드와 언어. 바뀔 때만 문구를 다시 만든다.
        private string shownCode;
        private string shownLanguage;
        private bool shownIsPublic;

        /// <summary>
        /// 버튼 동작을 연결한다. 복사 안내는 같은 캔버스의 <see cref="IRoomNotice"/>로 띄운다.
        /// </summary>
        private void Start()
        {
            notice = UiLookup.FindInParent<IRoomNotice>(this);
            copyCodeButton.onClick.AddListener(CopyRoomCode);
            inviteButton.onClick.AddListener(() => SessionServices.Session?.OpenInviteDialog());
        }

        /// <summary>
        /// 방 코드와 버튼 상태를 갱신한다.
        /// </summary>
        private void Update()
        {
            ISessionService session = SessionServices.Session;
            bool isSteamRoom = session != null && session.IsSteamRoom;

            string code = session?.RoomCode;
            bool isPublic = session != null && session.IsPublicRoom;
            if (code != shownCode || Loc.CurrentLanguage != shownLanguage || isPublic != shownIsPublic)
            {
                shownCode = code;
                shownLanguage = Loc.CurrentLanguage;
                shownIsPublic = isPublic;
                roomCodeText.text = session != null ? DescribeRoom(code, isSteamRoom, isPublic) : string.Empty;
            }
            copyCodeButton.interactable = isSteamRoom;
            inviteButton.interactable = isSteamRoom;
        }

        /// <summary>
        /// 방 코드 문구. Steam 방이면 공개 범위를 함께 보여 준다.
        /// </summary>
        private static string DescribeRoom(string code, bool isSteamRoom, bool isPublic)
        {
            string text = Loc.Format("room.code", code);
            return isSteamRoom ? $"{text}  ({Loc.Get(isPublic ? "room.visibility.public" : "room.visibility.friends")})" : text;
        }

        /// <summary>
        /// 방 코드를 클립보드에 복사한다.
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
