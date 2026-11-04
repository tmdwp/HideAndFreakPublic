using System.Text;

namespace GuardVsMannequin.Core
{
    /// <summary>
    /// 다른 플레이어가 보낸 문자열을 화면에 보여 주기 전에 정리한다.
    /// (이름에 &lt;size=200&gt; 같은 리치 텍스트 태그를 넣어 다른 사람의 화면을 깨뜨리는 것을 막는다)
    /// </summary>
    public static class DisplayTextSanitizer
    {
        /// <summary>플레이어 이름의 최대 길이(글자 수).</summary>
        public const int MaxPlayerNameLength = 24;

        /// <summary>
        /// 꺾쇠와 제어 문자를 지우고 앞뒤 공백을 없앤 뒤 최대 길이로 자른다.
        /// </summary>
        /// <param name="text">정리할 문자열. null이어도 된다.</param>
        /// <param name="maxLength">최대 길이(글자 수).</param>
        public static string Clean(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var builder = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == '<' || c == '>' || char.IsControl(c))
                    continue;
                builder.Append(c);
            }

            string cleaned = builder.ToString().Trim();
            return cleaned.Length > maxLength ? cleaned.Substring(0, maxLength) : cleaned;
        }
    }
}
