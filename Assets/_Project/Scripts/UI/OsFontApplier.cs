using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>Unity 기본 폰트는 한글이 없어 기본 폰트를 쓰는 Text에만 게임 UI 폰트를 적용한다.</summary>
    public class OsFontApplier : MonoBehaviour
    {
        private const string BuiltInFontName = "LegacyRuntime";

        [Tooltip("비워 두면 게임 UI 기본 폰트를 쓴다. 폰트 에셋을 넣으면 기본 폰트를 쓰는 Text를 이 폰트로 바꾼다.")]
        [SerializeField] private Font replacementFont;

        /// <summary>
        /// 나중에 켜지는 패널도 한글이 깨지지 않도록 비활성 Text까지 처음에 한꺼번에 바꾼다.
        /// </summary>
        private void Awake()
        {
            Font font = replacementFont != null ? replacementFont : UiFont.Default;
            foreach (Text text in GetComponentsInChildren<Text>(true))
            {
                if (text.font == null || text.font.name == BuiltInFontName)
                    text.font = font;
            }
        }
    }
}
