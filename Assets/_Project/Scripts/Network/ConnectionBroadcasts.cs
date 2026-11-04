using FishNet.Broadcast;

namespace GuardVsMannequin.Network
{
    /// <summary>
    /// 클라이언트 → 서버: 접속 직후 게임 버전과 방 암호를 알린다. (인증 전에 보내므로 인증 없이 받는다)
    /// </summary>
    public struct JoinRequestBroadcast : IBroadcast
    {
        public string version;

        /// <summary>입력한 방 암호. 암호 없는 방이면 무시된다.</summary>
        public string password;
    }

    /// <summary>
    /// 서버 → 클라이언트: 접속을 거부한 이유. 클라이언트는 로비에 안내 문구로 보여준다.
    /// </summary>
    public struct ConnectionRejectedBroadcast : IBroadcast
    {
        public ConnectionRejectReason reason;

        /// <summary>호스트의 게임 버전. (버전이 다를 때 안내용)</summary>
        public string hostVersion;
    }

    /// <summary>
    /// 접속 거부 이유.
    /// </summary>
    public enum ConnectionRejectReason : byte
    {
        /// <summary>호스트와 게임 버전이 다름.</summary>
        VersionMismatch,

        /// <summary>방 인원이 가득 참.</summary>
        RoomFull,

        /// <summary>게임이 진행 중.</summary>
        InGame,

        /// <summary>Steam 방인데 로비를 거치지 않은 접속.</summary>
        NotLobbyMember,

        /// <summary>정해진 시간 안에 버전을 알리지 않음. (버전 확인이 없는 옛 빌드)</summary>
        NoVersion,

        /// <summary>방 암호가 틀림.</summary>
        WrongPassword,

        /// <summary>암호를 여러 번 틀려 잠시 막힘.</summary>
        TooManyPasswordAttempts
    }
}
