namespace GuardVsMannequin.Network
{
    /// <summary>
    /// Steam 로비 데이터의 키. 방을 만드는 쪽(<see cref="SteamLobbyService"/>)과 찾는 쪽(<see cref="SteamRoomBrowser"/>)이 함께 쓴다.
    /// 값은 로비 주인이 마음대로 적을 수 있으므로, 읽는 쪽이 검사한다.
    /// </summary>
    public static class SteamLobbyKeys
    {
        /// <summary>게임 표시. 테스트용 AppID 480에는 다른 게임의 로비가 섞이므로 검색에서 거른다.</summary>
        public const string Game = "Game";

        /// <summary><see cref="Game"/> 키의 값.</summary>
        public const string GameValue = "HideAndFreak";

        /// <summary>게임 버전. 다르면 들어가도 거부되므로 검색에서 거른다.</summary>
        public const string Version = "Version";

        /// <summary>암호가 있으면 "1".</summary>
        public const string HasPassword = "HasPassword";

        /// <summary>공개방이면 "1". (룸 화면 표시)</summary>
        public const string IsPublic = "IsPublic";

        /// <summary>방 이름.</summary>
        public const string Name = "Name";

        /// <summary>고른 맵의 ID.</summary>
        public const string Map = "Map";

        /// <summary>고른 맵의 최대 인원.</summary>
        public const string MaxPlayers = "MaxPlayers";

        /// <summary>호스트의 Steam 핑 위치. (받는 쪽이 예상 핑을 계산)</summary>
        public const string PingLocation = "PingLoc";
    }
}
