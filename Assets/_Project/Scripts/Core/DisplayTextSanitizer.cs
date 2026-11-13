using System.Text;

namespace GuardVsMannequin.Core
{
    /// <summary>리치 텍스트 태그로 다른 사람 화면을 깨뜨리지 못하도록 받은 문자열을 정리한다.</summary>
    public static class DisplayTextSanitizer
    {
        /// <summary>긴 이름이 HUD·목록 칸을 넘치지 않게 하는 글자 수 상한.</summary>
        public const int MaxPlayerNameLength = 24;

        /// <summary>
        /// 꺾쇠만 지우면 태그가 성립하지 않아 리치 텍스트를 막을 수 있고, 제어 문자는 줄바꿈으로 화면을 밀어낼 수 있어 함께 지운다.
        /// </summary>
        /// <param name="text">null이어도 된다.</param>
        /// <param name="maxLength">글자 수 상한.</param>
        public static string Clean(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var builder = new StringBuilder(text.Length);
            foreach (char character in text)
            {
                if (character == '<' || character == '>' || char.IsControl(character))
                    continue;
                builder.Append(character);
            }

            string cleaned = builder.ToString().Trim();
            return cleaned.Length > maxLength ? cleaned.Substring(0, maxLength) : cleaned;
        }
    }
}
