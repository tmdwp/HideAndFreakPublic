using FishNet;
using GuardVsMannequin.Network;
using GuardVsMannequin.Room;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 룸 화면 코드들이 함께 쓰는 로컬 플레이어 정보.
    /// </summary>
    public static class RoomClient
    {
        // 게임 중 나가기: 나간다는 알림이 서버에 전달될 때까지 기다리는 시간(초)
        private const float LeaveNoticeSeconds = 0.25f;

        /// <summary>
        /// 게임 중 게스트는 나가면 복귀할 수 없고 방장이 나가면 모두가 나가게 되므로, 그런 경우에만 확인 창을 거친다.
        /// </summary>
        public static void RequestLeave()
        {
            RoomState room = RoomState.Instance;
            int otherMemberCount = room != null ? room.Members.Count - 1 : 0;
            string warningKey = LeaveConfirmRules.GetWarningKey(IsHost, room != null && room.IsInGame, otherMemberCount);
            if (warningKey == null)
            {
                LeaveRoom();
                return;
            }
            ConfirmDialog.Show("confirm.leave.title", warningKey, "confirm.leave.confirm", LeaveRoom);
        }

        /// <summary>
        /// 그냥 끊으면 서버가 재접속을 기다리며 자리를 남기므로, 게임 중인 게스트는 스스로 나간다고 먼저 알리고 잠시 뒤 끊는다.
        /// </summary>
        public static void LeaveRoom()
        {
            ISessionService session = SessionServices.Session;
            if (session == null)
                return;

            RoomState room = RoomState.Instance;
            if (room != null && room.IsInGame && !IsHost)
            {
                room.DeclareLeavingServerRpc();
                session.LeaveAfterDelay(LeaveNoticeSeconds);
                return;
            }
            session.Leave();
        }

        /// <summary>호스트가 곧 서버이므로 서버가 켜져 있는지로 판단한다.</summary>
        public static bool IsHost => InstanceFinder.IsServerStarted;

        /// <summary>
        /// 화면마다 접속 ID로 직접 찾지 않도록 로컬 참가자 조회를 한곳에 둔다.
        /// </summary>
        public static bool TryGetLocalMember(RoomState room, out RoomMember member)
        {
            int localClientId = InstanceFinder.ClientManager.Connection.ClientId;
            return room.Members.TryGetValue(localClientId, out member);
        }

        /// <summary>
        /// 참가자 정보가 아직 없으면 준비 전으로 보아야 버튼 문구가 맞으므로 false를 돌려준다.
        /// </summary>
        public static bool IsLocalReady(RoomState room)
        {
            return TryGetLocalMember(room, out RoomMember member) && member.isReady;
        }
    }
}
