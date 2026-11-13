using System;

namespace GuardVsMannequin.Mods
{
    /// <summary>호스트가 바꿀 수 있는 다운로드 링크로 피해를 입지 않도록 https와 허용한 사이트만 통과시킨다.</summary>
    public static class ModLinkPolicy
    {
        private const int MaxLength = 512;

        // 주소 해석기마다 다르게 읽을 수 있어 다른 사이트로 속일 때 쓰는 문자.
        private static readonly char[] ForbiddenCharacters = { '\\', '@', ' ', '"', '\'', '<', '>', '`' };

        private const string SteamHost = "steamcommunity.com";
        private const string GitHubHost = "github.com";

        // steamcommunity.com에는 다른 사이트로 넘기는 /linkfilter 경로가 있어 창작마당 경로만 허용한다.
        private static readonly string[] SteamPathPrefixes = { "/sharedfiles/", "/workshop/" };

        // 로그인·권한 승인 화면으로 보낼 수 있는 GitHub 기능 경로.
        private static readonly string[] GitHubReservedOwners = { "login", "logout", "sessions", "session", "settings", "apps", "marketplace", "account", "orgs", "organizations", "users" };

        /// <summary>
        /// 호스트가 보낸 원문 대신 Uri가 정리한 주소를 돌려줘, 검사한 주소와 실제로 여는 주소가 달라지지 않게 한다.
        /// </summary>
        /// <param name="url">검사할 주소.</param>
        /// <param name="safeUrl">통과하면 정리한 주소, 아니면 빈 문자열.</param>
        /// <returns>열어도 되는지 여부.</returns>
        public static bool TryGetSafeUrl(string url, out string safeUrl)
        {
            safeUrl = string.Empty;
            if (string.IsNullOrEmpty(url) || url.Length > MaxLength || HasForbiddenCharacter(url) || url.Contains(".."))
                return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                return false;
            if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo))
                return false;
            // 경로의 퍼센트 인코딩(..%2F 등)은 브라우저·서버가 다르게 풀어 허용 경로를 벗어날 수 있다.
            if (uri.AbsolutePath.IndexOf('%') >= 0 || !IsAllowedSite(uri))
                return false;

            safeUrl = uri.AbsoluteUri;
            return true;
        }

        /// <summary>
        /// 비슷하게 생긴 유니코드 글자나 제어 문자로 다른 사이트처럼 보이게 속일 수 있어, ASCII 출력 문자가 아니면 거부한다.
        /// </summary>
        private static bool HasForbiddenCharacter(string url)
        {
            foreach (char character in url)
            {
                if (char.IsControl(character) || character > 126 || Array.IndexOf(ForbiddenCharacters, character) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 하위 도메인은 누구나 만들 수 있는 곳(예: *.github.io)이 있어 호스트가 정확히 일치할 때만 허용한다.
        /// </summary>
        private static bool IsAllowedSite(Uri uri)
        {
            if (string.Equals(uri.Host, GitHubHost, StringComparison.OrdinalIgnoreCase))
                return IsGitHubProjectPage(uri);
            if (!string.Equals(uri.Host, SteamHost, StringComparison.OrdinalIgnoreCase))
                return false;

            foreach (string prefix in SteamPathPrefixes)
            {
                if (uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>링크를 열어도 파일이 바로 받아지지 않도록 저장소 첫 화면과 릴리스 화면만 허용한다.</summary>
        private static bool IsGitHubProjectPage(Uri uri)
        {
            if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                return false;

            string[] segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2 || Array.IndexOf(GitHubReservedOwners, segments[0].ToLowerInvariant()) >= 0)
                return false;
            if (segments.Length == 2)
                return true;
            return segments[2] == "releases" && (segments.Length == 3 || segments[3] != "download");
        }
    }
}
