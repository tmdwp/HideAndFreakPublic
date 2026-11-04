namespace GuardVsMannequin.Network
{
    /// <summary>
    /// 공개방 목록의 방 하나. 문자열은 검사를 마친 값이다.
    /// </summary>
    public struct RoomListing
    {
        /// <summary>핑을 알 수 없을 때의 값.</summary>
        public const int UnknownPing = -1;

        /// <summary>Steam 로비 ID. (방 코드)</summary>
        public ulong lobbyId;

        public string name;

        /// <summary>고른 맵의 ID. 호스트가 아직 적지 않았으면 빈 문자열.</summary>
        public string mapId;

        public int playerCount;
        public int maxPlayers;
        public bool hasPassword;

        /// <summary>고른 맵을 내 PC에서 플레이할 수 있어 보이는지. (버전·파일까지의 확인은 게임을 시작할 때 한다)</summary>
        public bool hasMap;

        /// <summary>예상 핑(ms). 모르면 <see cref="UnknownPing"/>.</summary>
        public int pingMs;
    }
}
