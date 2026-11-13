namespace GuardVsMannequin.Network
{
    /// <summary>검사를 마친 값만 담은 공개방 목록의 방 하나.</summary>
    public struct RoomListing
    {
        /// <summary>핑을 알 수 없을 때의 값.</summary>
        public const int UnknownPing = -1;

        /// <summary>방 코드인 Steam 로비 ID.</summary>
        public ulong lobbyId;

        public string name;

        /// <summary>고른 맵의 ID.</summary>
        public string mapId;

        public int playerCount;
        public int maxPlayers;
        public bool hasPassword;

        /// <summary>고른 맵을 내 PC에서 플레이할 수 있어 보이는지.</summary>
        public bool hasMap;

        /// <summary>예상 핑(ms).</summary>
        public int pingMs;
    }
}
