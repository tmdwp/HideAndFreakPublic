using System.Collections.Generic;
using System.Text;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Room;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>룸 화면의 참가자 목록.</summary>
    public class RoomMemberListView : MonoBehaviour
    {
        [SerializeField] private Text memberListText;

        private readonly StringBuilder builder = new();
        private readonly List<KeyValuePair<int, RoomMember>> sortedMembers = new();
        private RefreshTimer refreshTimer;

        /// <summary>목록이 흔들리지 않도록 호스트 먼저 접속 순서대로 정렬하고 일정 간격으로만 갱신한다.</summary>
        private void Update()
        {
            RoomState room = RoomState.Instance;
            if (room == null || !refreshTimer.Tick())
                return;

            sortedMembers.Clear();
            sortedMembers.AddRange(room.Members);
            sortedMembers.Sort((first, second) => first.Value.isHost != second.Value.isHost
                ? (first.Value.isHost ? -1 : 1)
                : first.Key.CompareTo(second.Key));

            builder.Clear();
            builder.AppendLine(Loc.Format("room.members.count", sortedMembers.Count, GameConstants.MaxPlayers));
            foreach (KeyValuePair<int, RoomMember> member in sortedMembers)
            {
                string displayName = room.GetDisplayName(member.Key);
                string stateLabel = Loc.Get(member.Value.isHost ? "room.member.host" : (member.Value.isReady ? "room.member.ready" : "room.member.notReady"));
                string modWarning = member.Value.modState == MemberModState.Missing ? "  " + Loc.Get("room.member.modsMissing") : string.Empty;
                builder.AppendLine($"{displayName}  —  {stateLabel}{modWarning}");
            }
            memberListText.text = builder.ToString();
        }
    }
}
