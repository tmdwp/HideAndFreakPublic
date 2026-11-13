using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>빌드 후에도 고칠 수 있는 파일에서 크레딧과 이용한 에셋 페이지를 읽어 보여 준다.</summary>
    public class CreditsView : MonoBehaviour
    {
        private const string CreditsPathPrefix = "Credits/credits.";
        private const string AssetsPath = "Credits/assets";

        [SerializeField] private Text bodyText;
        [SerializeField] private Button closeButton;
        [Tooltip("크레딧 ↔ 이용한 에셋 페이지 전환 버튼. 비우면 크레딧 페이지만 보인다.")]
        [SerializeField] private Button pageButton;
        [SerializeField] private Text pageButtonLabel;

        private bool isShowingAssets;

        /// <summary>
        /// 페이지 전환 버튼은 없어도 되는 선택 요소라 있을 때만 연결한다.
        /// </summary>
        private void Awake()
        {
            closeButton.onClick.AddListener(Close);
            if (pageButton != null)
                pageButton.onClick.AddListener(TogglePage);
        }

        /// <summary>
        /// 열려 있는 동안 언어가 바뀌면 다른 언어 파일을 읽어야 하므로 구독한다.
        /// </summary>
        private void OnEnable()
        {
            Loc.LanguageChanged += Refresh;
        }

        /// <summary>
        /// 닫힌 동안에는 다시 읽을 필요가 없으므로 끊는다.
        /// </summary>
        private void OnDisable()
        {
            Loc.LanguageChanged -= Refresh;
        }

        /// <summary>
        /// 이전에 보던 페이지가 아니라 항상 크레딧부터 보이도록 연다.
        /// </summary>
        public void Open()
        {
            gameObject.SetActive(true);
            ShowPage(false);
        }

        /// <summary>
        /// 닫기 버튼과 바깥 메뉴가 함께 쓰도록 공개한다.
        /// </summary>
        public void Close()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 버튼 하나로 두 페이지를 오가도록 현재 페이지의 반대로 바꾼다.
        /// </summary>
        private void TogglePage()
        {
            ShowPage(!isShowingAssets);
        }

        /// <summary>
        /// 이전 페이지의 스크롤 위치가 남으면 내용 중간부터 보이므로 맨 위로 되돌린다.
        /// </summary>
        private void ShowPage(bool isAssetsPage)
        {
            isShowingAssets = isAssetsPage;
            Refresh();

            ScrollRect scroll = bodyText.GetComponentInParent<ScrollRect>();
            if (scroll != null)
                scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>
        /// 버튼 문구는 "다른 페이지로 가기"를 뜻하므로 현재 페이지의 반대 이름을 표시한다.
        /// </summary>
        private void Refresh()
        {
            bodyText.text = isShowingAssets ? LoadAssetsPage() : LoadCreditsPage();
            if (pageButtonLabel != null)
                pageButtonLabel.text = Loc.Get(isShowingAssets ? "credits.showCredits" : "credits.showAssets");
        }

        /// <summary>
        /// 번역이 덜 된 언어에서도 빈 화면이 되지 않도록 첫 번째 언어 파일로 대신한다.
        /// </summary>
        private static string LoadCreditsPage()
        {
            if (TryLoadText(CreditsPathPrefix + Loc.CurrentLanguage, out string text)
                || (Loc.Languages.Count > 0 && TryLoadText(CreditsPathPrefix + Loc.Languages[0], out text)))
                return text;
            return Loc.Get("credits.missing");
        }

        /// <summary>
        /// 에셋 목록은 언어 공통 파일이라 제목만 현재 언어로 붙인다.
        /// </summary>
        private static string LoadAssetsPage()
        {
            string assetList = TryLoadText(AssetsPath, out string text) ? text : Loc.Get("credits.assetsMissing");
            return Loc.Get("credits.assetsTitle") + "\n\n" + assetList;
        }

        /// <summary>
        /// 빌드 후에도 크레딧을 고칠 수 있도록 빌드 폴더의 외부 파일을 먼저 읽고, 없거나 깨졌으면 내장 원본을 쓴다.
        /// </summary>
        private static bool TryLoadText(string resourcePath, out string text)
        {
            return DataFiles.TryLoad<string>(resourcePath, ".txt", TryParseText, out text);
        }

        /// <summary>크레딧은 가공할 필요가 없어 그대로 돌려준다.</summary>
        private static bool TryParseText(string source, out string text)
        {
            text = source;
            return true;
        }
    }
}
