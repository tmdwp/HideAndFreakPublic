namespace GuardVsMannequin.Network
{
    /// <summary>
    /// 방 만들기/참가/나가기를 제공한다. 로비·룸 UI는 이 인터페이스에만 의존한다(DIP).
    /// </summary>
    public interface ISessionService
    {
        /// <summary>Steam을 사용할 수 있는지 여부.</summary>
        bool IsSteamAvailable { get; }

        /// <summary>연결 작업(방 만들기, 참가)이 진행 중인지 여부.</summary>
        bool IsBusy { get; }

        /// <summary>현재 방이 Steam 방인지 여부. (아니면 직접 접속 방)</summary>
        bool IsSteamRoom { get; }

        /// <summary>내가 만든 방이 테스트 방인지 여부. (1인 시작, 승패 판정 없음, 게임 중 테스트 메뉴)</summary>
        bool IsTestRoom { get; }

        /// <summary>다른 사람이 참가할 때 쓰는 방 코드. Steam 방이면 로비 ID, 직접 접속이면 안내 문구.</summary>
        string RoomCode { get; }

        /// <summary>로비 화면에 보여줄 마지막 안내 메시지. (연결 끊김, 실패 사유 등)</summary>
        string Notice { get; }

        /// <summary>내가 만든 방의 암호. 없으면 빈 문자열. (서버가 접속자를 확인할 때 사용)</summary>
        string RoomPassword { get; }

        /// <summary>참가할 때 호스트에 보낼 암호. (호스트 자신은 방 암호와 같음)</summary>
        string JoinPassword { get; }

        /// <summary>
        /// 초대 등으로 들어가려는 방에 암호가 필요할 때. 인자는 방 코드.
        /// 로비 화면은 방 코드를 채운 입장 화면을 열어 암호를 입력받는다.
        /// </summary>
        event System.Action<string> PasswordRequired;

        /// <summary>
        /// 모드가 달라 입장이 거부된 기록을 꺼낸다. 꺼내면 지워진다. (로비 씬을 다시 불러와도 남도록 상태로 보관)
        /// </summary>
        /// <param name="hostMods">호스트가 켠 모드 목록 설명. (없으면 빈 문자열)</param>
        /// <returns>보여줄 거부 기록이 있었는지 여부.</returns>
        bool TryTakeModRejection(out string hostMods);

        /// <summary>Steam 방을 만든다. 암호를 비우면 암호 없는 방이 된다.</summary>
        void HostSteamRoom(string password);

        /// <summary>방 코드로 Steam 방에 참가한다. 암호 없는 방이면 암호는 비워도 된다.</summary>
        void JoinSteamRoom(string roomCode, string password);

        /// <summary>직접 접속 방을 만든다. (개발/LAN용)</summary>
        void HostDirectRoom();

        /// <summary>
        /// 직접 접속 방을 테스트 방으로 만든다. 혼자 시작할 수 있고, 게임 중 테스트 메뉴로 설정·역할·시점을 바꿀 수 있다.
        /// </summary>
        void HostTestRoom();

        /// <summary>IP 주소로 직접 접속 방에 참가한다. (개발/LAN용)</summary>
        void JoinDirectRoom(string address);

        /// <summary>Steam 친구 초대 창을 연다.</summary>
        void OpenInviteDialog();

        /// <summary>새 참가자를 받을지 정한다. (게임 진행 중에는 막음, 호스트 전용)</summary>
        void SetRoomJoinable(bool joinable);

        /// <summary>방에서 나간다. 호스트가 나가면 방이 닫힌다.</summary>
        void Leave();

        /// <summary>안내 메시지를 지운다.</summary>
        void ClearNotice();
    }

    /// <summary>
    /// 씬이 바뀌어도 유지되는 세션 서비스를 조회한다.
    /// 로비 씬을 다시 불러올 때 생기는 중복 인스턴스는 등록되지 않는다(먼저 등록된 것이 유지).
    /// </summary>
    public static class SessionServices
    {
        /// <summary>현재 세션 서비스. 없으면 null.</summary>
        public static ISessionService Session { get; private set; }

        /// <summary>
        /// 아직 등록된 서비스가 없을 때만 등록한다.
        /// </summary>
        /// <returns>등록되었는지 여부.</returns>
        public static bool TryRegister(ISessionService session)
        {
            if (Session != null)
                return false;

            Session = session;
            return true;
        }

        /// <summary>
        /// 등록된 서비스와 같을 때만 해제한다.
        /// </summary>
        public static void Unregister(ISessionService session)
        {
            if (Session == session)
                Session = null;
        }
    }
}
