using GuardVsMannequin.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>strings.csv에 열을 추가하면 자동으로 나타나는 언어 설정.</summary>
    public class LanguageSettingsSection : MonoBehaviour, ISettingsSection
    {
        [SerializeField] private Button previousLanguageButton;
        [SerializeField] private Button nextLanguageButton;
        [SerializeField] private Text languageValueText;

        /// <summary>
        /// 설정 화면이 처음 열릴 때 한 번만 연결해 이벤트가 중복되지 않게 한다.
        /// </summary>
        public void Initialize()
        {
            previousLanguageButton.onClick.AddListener(() => ChangeLanguage(-1));
            nextLanguageButton.onClick.AddListener(() => ChangeLanguage(1));
        }

        /// <summary>
        /// 잘못 고른 언어를 읽지 못해도 되돌아올 수 있도록 언어 이름은 그 언어 자신으로 표시한다.
        /// </summary>
        public void Refresh()
        {
            languageValueText.text = Loc.GetLanguageName(Loc.CurrentLanguage);
        }

        /// <summary>
        /// 기본값으로 되돌리면 읽을 수 없는 언어로 바뀔 수 있어 비워 둔다.
        /// </summary>
        public void ResetToDefaults()
        {
        }

        /// <summary>키 설정 중 언어가 바뀌면 입력 대기 문구가 꼬이므로 막는다.</summary>
        private void ChangeLanguage(int direction)
        {
            if (RebindRowView.IsAnyRebinding)
                return;

            var languages = Loc.Languages;
            if (languages.Count == 0)
                return;

            int languageIndex = 0;
            for (int i = 0; i < languages.Count; i++)
            {
                if (languages[i] == Loc.CurrentLanguage)
                    languageIndex = i;
            }
            languageIndex = (languageIndex + direction + languages.Count) % languages.Count;
            Loc.SetLanguage(languages[languageIndex]);
        }
    }
}
