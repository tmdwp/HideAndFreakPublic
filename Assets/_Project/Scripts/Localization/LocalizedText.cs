using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.Localization
{
    /// <summary>고정 문구가 언어를 바꾸면 바로 다시 표시되도록 현재 언어로 채운다.</summary>
    [RequireComponent(typeof(Text))]
    public class LocalizedText : MonoBehaviour
    {
        [Tooltip("Resources/Localization/strings.csv의 key")]
        [SerializeField] private string key;

        private Text text;

        /// <summary>에디터 도구가 빠진 키를 검사할 수 있도록 노출한다.</summary>
        public string Key => key;

        /// <summary>
        /// 꺼져 있는 동안 언어가 바뀌었을 수 있어서, 켜질 때마다 다시 표시한 뒤 언어 변경을 듣는다.
        /// </summary>
        private void OnEnable()
        {
            text = GetComponent<Text>();
            Refresh();
            Loc.LanguageChanged += Refresh;
        }

        /// <summary>
        /// 꺼진 UI까지 갱신하면 낭비이고 파괴된 뒤 불리면 예외가 나므로 끊는다.
        /// </summary>
        private void OnDisable()
        {
            Loc.LanguageChanged -= Refresh;
        }

        /// <summary>
        /// 언어 변경 이벤트와 처음 표시가 같은 경로를 쓰도록 한곳에 둔다.
        /// </summary>
        private void Refresh()
        {
            text.text = Loc.Get(key);
        }
    }
}
