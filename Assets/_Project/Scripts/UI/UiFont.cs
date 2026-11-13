using UnityEngine;

namespace GuardVsMannequin.UI
{
    /// <summary>폰트 파일이 빠져도 한글이 깨지지 않도록 프로젝트 폰트가 없으면 OS 폰트를 쓰는 UI 기본 폰트.</summary>
    public static class UiFont
    {
        /// <summary>Resources.Load에 쓰므로 확장자를 뺀다.</summary>
        public const string ResourcePath = "Fonts/NeoDunggeunmoPro-Regular";

        private static readonly string[] OsFallbackFonts = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" };
        private static Font cachedFont;

        /// <summary>코드로 만드는 UI가 많아 한 번만 불러와 둔다.</summary>
        public static Font Default
        {
            get
            {
                if (cachedFont == null)
                    cachedFont = LoadProjectFont() ?? Font.CreateDynamicFontFromOSFont(OsFallbackFonts, 16);
                return cachedFont;
            }
        }

        /// <summary>에디터에서 UI를 만들 때 에셋을 넣도록 따로 공개하고, 파괴된 객체는 진짜 null로 돌려준다.</summary>
        public static Font LoadProjectFont()
        {
            Font font = Resources.Load<Font>(ResourcePath);
            return font != null ? font : null;
        }
    }
}
