namespace GuardVsMannequin.Network
{
    /// <summary>방을 만드는 쪽과 찾는 쪽이 같은 키를 쓰도록 모은 Steam 로비 데이터 키.</summary>
    public static class SteamLobbyKeys
    {
        /// <summary>테스트용 AppID에 섞이는 다른 게임 로비를 거르는 게임 표시.</summary>
        public const string Game = "Game";

        /// <summary><see cref="Game"/> 키의 값.</summary>
        public const string GameValue = "HideAndFreak";

        /// <summary>들어가도 거부될 방을 검색에서 거르는 게임 버전.</summary>
        public const string Version = "Version";

        /// <summary>판이 진행 중인 방을 목록에서 숨기는 방 상태.</summary>
        public const string State = "State";

        /// <summary><see cref="State"/> 값: 룸에서 기다리는 중.</summary>
        public const string StateRoom = "Room";

        /// <summary><see cref="State"/> 값: 판 진행 중.</summary>
        public const string StateInGame = "InGame";

        /// <summary>암호가 있으면 "1".</summary>
        public const string HasPassword = "HasPassword";

        /// <summary>룸 화면에 표시할 공개방 여부.</summary>
        public const string IsPublic = "IsPublic";

        /// <summary>방 이름.</summary>
        public const string Name = "Name";

        /// <summary>고른 맵의 ID.</summary>
        public const string Map = "Map";

        /// <summary>고른 맵의 최대 인원.</summary>
        public const string MaxPlayers = "MaxPlayers";

        /// <summary>받는 쪽이 예상 핑을 계산하는 호스트의 Steam 핑 위치.</summary>
        public const string PingLocation = "PingLoc";
    }
}
