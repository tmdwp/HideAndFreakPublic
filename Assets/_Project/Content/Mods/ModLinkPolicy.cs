using System;

namespace GuardVsMannequin.Mods
{
    /// <summary>
    /// 모드 다운로드 링크(<see cref="ModManifest.url"/>)를 열어도 되는지 판단한다.
    /// 링크는 호스트의 mod.json에서 오므로 호스트가 마음대로 바꿀 수 있다. 공개방에서는 모르는 사람이 보낸 링크와 같으므로
    /// https + 허용한 사이트만 통과시킨다. (받는 쪽이 항상 다시 검사한다. 내보내기 도구도 같은 규칙으로 미리 경고한다)
    /// </summary>
    public static class ModLinkPolicy
    {
        private const int MaxLength = 512;

        // 주소 해석기마다 다르게 읽을 수 있어 다른 사이트로 속일 때 쓰는 문자.
        private static readonly char[] ForbiddenCharacters = { '\\', '@', ' ', '"', '\'', '<', '>', '`' };

        /// <summary>허용한 사이트(정확히 일치, 하위 도메인 제외)와 그 사이트에서 허용한 경로 앞부분. 비어 있으면 모든 경로.</summary>
        private static readonly (string host, string[] pathPrefixes)[] AllowedSites =
        {
            // steamcommunity.com에는 다른 사이트로 넘기는 /linkfilter 경로가 있어 창작마당 경로만 허용한다.
            ("steamcommunity.com", new[] { "/sharedfiles/", "/workshop/" }),
            ("github.com", Array.Empty<string>())
        };

        /// <summary>
        /// 열어도 되는 링크면 정리한 주소를 돌려준다.
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
        /// 제어 문자나 속임수에 쓰이는 문자가 있는지 확인한다.
        /// </summary>
        private static bool HasForbiddenCharacter(string url)
        {
            foreach (char c in url)
            {
                if (char.IsControl(c) || c > 126 || Array.IndexOf(ForbiddenCharacters, c) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 허용한 사이트와 경로인지 확인한다.
        /// </summary>
        private static bool IsAllowedSite(Uri uri)
        {
            foreach ((string host, string[] pathPrefixes) in AllowedSites)
            {
                if (!string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (pathPrefixes.Length == 0)
                    return true;

                foreach (string prefix in pathPrefixes)
                {
                    if (uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal))
                        return true;
                }
            }
            return false;
        }
    }
}
