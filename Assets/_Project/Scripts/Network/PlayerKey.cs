namespace GuardVsMannequin.Network
{
    /// <summary>연결 번호는 접속마다 바뀌므로 다시 접속해도 같은 사람을 알아보는 키를 만든다.</summary>
    public static class PlayerKey
    {
        // 메모리에 기억하는 값이라 제한하는 임의 토큰 최대 길이.
        private const int MaxTokenLength = 64;

        /// <summary>위조할 수 없는 SteamID를 먼저 쓰고, 종류가 다른 키끼리 같아지지 않게 접두사를 붙인다.</summary>
        /// <param name="address">Steam 접속이면 SteamID 숫자, 아니면 IP.</param>
        /// <param name="token">클라이언트가 보낸 임의 토큰.</param>
        /// <param name="clientId">토큰이 없을 때 이 연결에서만 쓰는 키를 만들 연결 번호.</param>
        public static string Resolve(string address, string token, int clientId)
        {
            if (ulong.TryParse(address, out ulong steamId))
                return $"steam:{steamId}";
            if (!string.IsNullOrEmpty(token) && token.Length <= MaxTokenLength)
                return $"token:{token}";
            return $"connection:{clientId}";
        }
    }
}
