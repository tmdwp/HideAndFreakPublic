namespace GuardVsMannequin.Room
{
    /// <summary>참가자가 고른 맵의 모드를 가지고 있는지.</summary>
    public enum MemberModState : byte
    {
        /// <summary>아직 답하지 않음.</summary>
        Unknown,

        /// <summary>필요한 모드를 모두 가지고 있음.</summary>
        Ready,

        /// <summary>없거나 버전·파일이 다른 모드가 있음.</summary>
        Missing
    }

    /// <summary>
    /// 룸 참가자 한 명의 정보. 네트워크로 동기화되므로 값 타입 필드만 사용한다.
    /// </summary>
    public struct RoomMember
    {
        public string displayName;
        public bool isReady;
        public bool isHost;
        public MemberModState modState;
    }
}
