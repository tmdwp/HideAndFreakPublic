using System;
using GuardVsMannequin.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>실제 설정은 하위 설정 묶음에 맡기고 탭 전환·열고 닫기·기본값 복원만 맡는 설정 화면 틀.</summary>
    public class SettingsPanelView : MonoBehaviour
    {
        [Header("탭 (버튼과 페이지는 같은 순서)")]
        [SerializeField] private Button[] tabButtons;
        [SerializeField] private GameObject[] pages;
        [Tooltip("선택된 탭 버튼 색. 탭 버튼에 이미지를 넣었다면 흰색으로 두면 이미지 색 그대로 보인다.")]
        [SerializeField] private Color selectedTabColor = new Color(0.85f, 0.55f, 0.20f, 1f);
        [SerializeField] private Color normalTabColor = new Color(0.22f, 0.25f, 0.30f, 1f);

        [Header("버튼")]
        [SerializeField] private Button resetAllButton;
        [SerializeField] private Button closeButton;

        private ISettingsSection[] sections = Array.Empty<ISettingsSection>();
        private int currentPage;
        private bool isInitialized;

        /// <summary>설정 화면을 연 쪽이 원래 화면으로 돌아갈 수 있도록 닫힘을 알린다.</summary>
        public event Action Closed;

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>꺼진 화면은 Awake가 늦게 불리므로 처음 열릴 때 비활성 페이지까지 설정 묶음을 찾는다.</summary>
        private void Initialize()
        {
            if (isInitialized)
                return;
            isInitialized = true;

            sections = GetComponentsInChildren<ISettingsSection>(true);
            foreach (ISettingsSection section in sections)
                section.Initialize();

            for (int i = 0; i < tabButtons.Length; i++)
            {
                // 람다가 반복 변수를 잡으면 모든 버튼이 마지막 값을 쓰므로 복사해 둔다.
                int pageIndex = i;
                tabButtons[i].onClick.AddListener(() => ShowPage(pageIndex));
            }

            resetAllButton.onClick.AddListener(ResetAll);
            closeButton.onClick.AddListener(Close);
            Loc.LanguageChanged += OnLanguageChanged;
        }

        /// <summary>
        /// 파괴된 뒤 언어 변경이 이 객체를 부르지 않도록 끊는다.
        /// </summary>
        private void OnDestroy()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
        }

        /// <summary>
        /// 다른 곳에서 바뀐 값이 있을 수 있어 열 때마다 저장된 값을 다시 표시하고, 보던 페이지를 이어서 연다.
        /// </summary>
        public void Open()
        {
            gameObject.SetActive(true);
            Initialize();
            RefreshAll();
            ShowPage(currentPage);
        }

        /// <summary>
        /// 키 설정 중에 닫으면 입력 대기가 남으므로 그때는 닫지 않는다.
        /// </summary>
        public void Close()
        {
            if (RebindRowView.IsAnyRebinding)
                return;

            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        /// <summary>키 설정 중 페이지를 바꾸면 입력 대기가 숨겨진 채 남으므로 그때는 바꾸지 않는다.</summary>
        private void ShowPage(int pageIndex)
        {
            if (RebindRowView.IsAnyRebinding || pages.Length == 0)
                return;

            currentPage = Mathf.Clamp(pageIndex, 0, pages.Length - 1);
            for (int i = 0; i < pages.Length; i++)
                pages[i].SetActive(i == currentPage);

            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i].targetGraphic != null)
                    tabButtons[i].targetGraphic.color = i == currentPage ? selectedTabColor : normalTabColor;
            }
        }

        /// <summary>
        /// 묶음마다 표시 방법이 달라서 각 묶음에 다시 그리기를 맡긴다.
        /// </summary>
        private void RefreshAll()
        {
            foreach (ISettingsSection section in sections)
                section.Refresh();
        }

        /// <summary>
        /// 언어·화면처럼 되돌리면 곤란한 설정은 각 묶음이 알아서 유지하므로, 여기서는 모든 묶음에 기본값을 요청하기만 한다.
        /// </summary>
        private void ResetAll()
        {
            if (RebindRowView.IsAnyRebinding)
                return;

            foreach (ISettingsSection section in sections)
                section.ResetToDefaults();
            RefreshAll();
        }

        /// <summary>코드로 채운 문장은 LocalizedText가 아니라 언어가 바뀌면 직접 다시 표시한다.</summary>
        private void OnLanguageChanged()
        {
            if (IsOpen)
                RefreshAll();
        }
    }
}
