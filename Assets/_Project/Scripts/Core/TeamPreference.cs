namespace GuardVsMannequin.Core
{
    /// <summary>룸에서 고르는 시작 희망 팀. 번호가 네트워크로 오가므로 바꾸면 안 된다.</summary>
    public enum TeamPreference : byte
    {
        /// <summary>어느 쪽이든 괜찮음. 값이 없던 참가자도 이것으로 읽힌다.</summary>
        Random = 0,

        Guard = 1,

        Mannequin = 2
    }
}
