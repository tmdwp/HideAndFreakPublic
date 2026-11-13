namespace GuardVsMannequin.Network
{
    /// <summary>
    /// Steam 방을 만들 때 고르는 값.
    /// </summary>
    public struct SteamRoomOptions
    {
        /// <summary>방 이름의 최대 길이(글자 수).</summary>
        public const int MaxNameLength = 24;

        /// <summary>방 암호(비우면 암호 없음).</summary>
        public string password;

        /// <summary>true면 공개방(목록에 나옴), false면 친구 전용.</summary>
        public bool isPublic;

        /// <summary>방 이름(비우면 Steam 이름으로 정함).</summary>
        public string roomName;
    }
}
