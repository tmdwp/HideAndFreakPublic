using System.Collections.Generic;
using System.Text;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Room;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 룸 화면의 참가자 목록(인원 수, 이름, 호스트/준비 상태).
    /// </summary>
    public class RoomMemberListView : MonoBehaviour
    {
        [SerializeField] private Text memberListText;

        private readonly StringBuilder builder = new();
        private readonly List<KeyValuePair<int, RoomMember>> sortedMembers = new();
        private RefreshTimer refreshTimer;

        /// <summary>
        /// 참가자 목록을 호스트 먼저, 접속 순서대로 표시한다. (문자열을 새로 만들므로 일정 간격으로만 갱신)
        /// </summary>
        private void Update()
        {
            RoomState room = RoomState.Instance;
            if (room == null || !refreshTimer.Tick())
                return;

            sortedMembers.Clear();
            sortedMembers.AddRange(room.Members);
            sortedMembers.Sort((a, b) => a.Value.isHost != b.Value.isHost ? (a.Value.isHost ? -1 : 1) : a.Key.CompareTo(b.Key));

            builder.Clear();
            builder.AppendLine(Loc.Format("room.members.count", sortedMembers.Count, GameConstants.MaxPlayers));
            foreach (KeyValuePair<int, RoomMember> pair in sortedMembers)
            {
                string name = room.GetDisplayName(pair.Key);
                string state = Loc.Get(pair.Value.isHost ? "room.member.host" : (pair.Value.isReady ? "room.member.ready" : "room.member.notReady"));
                string modWarning = pair.Value.modState == MemberModState.Missing ? "  " + Loc.Get("room.member.modsMissing") : string.Empty;
                builder.AppendLine($"{name}  —  {state}{modWarning}");
            }
            memberListText.text = builder.ToString();
        }
    }
}
