using FishNet.Broadcast;

namespace GuardVsMannequin.Network
{
    /// <summary>인증 전에 보내므로 인증 없이 받는 접속 직후의 게임 버전·방 암호 알림.</summary>
    public struct JoinRequestBroadcast : IBroadcast
    {
        public string version;

        /// <summary>입력한 방 암호.</summary>
        public string password;

        /// <summary>Steam이 아닌 접속에서 같은 사람을 알아보는 실행마다 만드는 임의 토큰.</summary>
        public string clientToken;
    }

    /// <summary>클라이언트가 로비에 안내로 보여 줄 접속 거부 이유.</summary>
    public struct ConnectionRejectedBroadcast : IBroadcast
    {
        public ConnectionRejectReason reason;

        /// <summary>버전이 다를 때 안내할 호스트의 게임 버전.</summary>
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

        /// <summary>정해진 시간 안에 버전을 알리지 않음.</summary>
        NoVersion,

        /// <summary>방 암호가 틀림.</summary>
        WrongPassword,

        /// <summary>암호를 여러 번 틀려 잠시 막힘.</summary>
        TooManyPasswordAttempts,

        /// <summary>방장이 내보냄.</summary>
        Kicked,

        /// <summary>이 방에서 추방된 사람.</summary>
        Banned
    }
}
