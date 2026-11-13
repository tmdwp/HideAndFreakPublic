using Steamworks;

namespace GuardVsMannequin.Network
{
    /// <summary>이 PC 플레이어의 표시 이름.</summary>
    public static class LocalProfile
    {
        /// <summary>이름 입력 화면을 따로 두지 않으려고 Steam 닉네임을 쓴다.</summary>
        public static string DisplayName => SteamBootstrap.IsInitialized ? SteamFriends.GetPersonaName() : string.Empty;
    }
}
