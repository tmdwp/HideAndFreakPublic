namespace GuardVsMannequin.Network
{
    /// <summary>로비·룸 UI가 구현에 의존하지 않도록 방 만들기·참가·나가기를 인터페이스로 둔다.</summary>
    public interface ISessionService
    {
        /// <summary>Steam이 꺼져 있으면 Steam 방 버튼을 숨기려고 쓴다.</summary>
        bool IsSteamAvailable { get; }

        /// <summary>연결 중에 버튼을 또 눌러 요청이 겹치지 않도록 막는 데 쓴다.</summary>
        bool IsBusy { get; }

        /// <summary>초대·방 코드가 Steam 방에서만 의미가 있어 구분하는 Steam 방 여부.</summary>
        bool IsSteamRoom { get; }

        /// <summary>1인 시작·승패 판정 생략·게임 중 테스트 메뉴를 켜는 기준.</summary>
        bool IsTestRoom { get; }

        /// <summary>Steam 방이면 로비 ID, 직접 접속이면 안내 문구.</summary>
        string RoomCode { get; }

        /// <summary>연결이 끊기거나 실패했을 때 로비로 돌아온 뒤에도 이유를 보여 주려고 남겨 둔다.</summary>
        string Notice { get; }

        /// <summary>서버가 접속자를 확인할 때 쓰는 값.</summary>
        string RoomPassword { get; }

        /// <summary>호스트 자신도 같은 검증을 거치므로 호스트에서는 방 암호와 같다.</summary>
        string JoinPassword { get; }

        /// <summary>암호가 있는 방은 바로 접속할 수 없어 로비가 입장 화면을 열도록 알린다.</summary>
        event System.Action<string> PasswordRequired;

        /// <summary>Steam 방이 아니면 공개 목록이 없으므로 false.</summary>
        bool IsPublicRoom { get; }

        /// <summary>Steam을 쓸 수 없으면 null.</summary>
        IRoomBrowser RoomBrowser { get; }

        /// <summary>Steam 로비를 거쳐 들어오게 해 NAT와 초대를 Steam에 맡긴다.</summary>
        void HostSteamRoom(SteamRoomOptions options);

        /// <summary>방 목록에서 맵과 인원 상한이 보이도록 알린다.</summary>
        void UpdateRoomListing(string mapId, int maxPlayers);

        /// <summary>방 코드만 알면 친구가 아니어도 들어올 수 있게 한다.</summary>
        void JoinSteamRoom(string roomCode, string password);

        /// <summary>Steam 없이 개발·LAN에서 시험할 수 있도록 둔다.</summary>
        void HostDirectRoom();

        /// <summary>
        /// 혼자서 기능을 확인할 수 있도록, 혼자 시작할 수 있고 게임 중 테스트 메뉴로 설정·역할·시점을 바꿀 수 있는 방을 만든다.
        /// </summary>
        void HostTestRoom();

        /// <summary>Steam 없이 개발·LAN에서 시험할 수 있도록 IP로 참가한다.</summary>
        void JoinDirectRoom(string address);

        void OpenInviteDialog();

        /// <summary>판 중에는 목록에서 숨기되 끊긴 사람이 돌아오도록 로비 입장은 열어 둔다.</summary>
        void SetRoomInGame(bool inGame);

        /// <summary>지난 방의 추방·재접속 기록이 섞이지 않도록 방마다 새로 만드는 기록.</summary>
        RoomMembership Membership { get; }

        /// <summary>Steam이 아닌 접속에서도 재접속한 사람을 알아보려고 이번 실행에서 만든 임의 토큰.</summary>
        string ClientToken { get; }

        /// <summary>로비가 다시 참가 버튼에 보여 줄 남은 시간(초).</summary>
        float RejoinSecondsLeft { get; }

        void Rejoin();

        /// <summary>돌아갈 생각이 없는데 대기 자리를 붙잡아 두면 판이 끝나지 않으므로 포기를 알린다.</summary>
        void DiscardRejoin();

        /// <summary>서버에 보낸 메시지(나가기 선언 등)가 연결이 끊기기 전에 전달되도록 잠시 기다렸다가 나간다.</summary>
        void LeaveAfterDelay(float seconds);

        /// <summary>호스트가 나가면 방이 닫힌다.</summary>
        void Leave();

        /// <summary>문제가 생겨 더 있을 수 없을 때, 로비로 돌아간 사용자가 이유를 알 수 있도록 안내를 남기고 나간다.</summary>
        /// <param name="noticeKey">strings.csv의 key.</param>
        void LeaveWithNotice(string noticeKey);

        /// <summary>읽은 안내가 다음에 로비를 열 때 또 뜨지 않도록 지운다.</summary>
        void ClearNotice();
    }

    /// <summary>씬이 바뀌어도 유지되는 세션 서비스를 조회하며, 중복 인스턴스는 등록하지 않는다.</summary>
    public static class SessionServices
    {
        /// <summary>없으면 null.</summary>
        public static ISessionService Session { get; private set; }

        /// <summary>
        /// 로비 씬을 다시 불러오면 새 인스턴스가 생기는데, 연결 상태를 가진 기존 서비스를 지키려고 비어 있을 때만 등록한다.
        /// </summary>
        /// <returns>중복 인스턴스가 스스로 사라질 수 있도록 등록되었는지 알려 준다.</returns>
        public static bool TryRegister(ISessionService session)
        {
            if (Session != null)
                return false;

            Session = session;
            return true;
        }

        /// <summary>
        /// 중복 인스턴스가 파괴될 때 진짜 서비스를 지우지 않도록 자기 자신일 때만 해제한다.
        /// </summary>
        public static void Unregister(ISessionService session)
        {
            if (Session == session)
                Session = null;
        }
    }
}
