using GuardVsMannequin.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>맵·게임 설정을 화면 가운데 큰 창으로 보이고, 하단의 On/Off 버튼으로 켜고 끈다.</summary>
    public class RoomSettingsWindow : MonoBehaviour
    {
        [Tooltip("켜고 끌 설정 창.")]
        [SerializeField] private GameObject window;

        [Tooltip("창 뒤를 어둡게 덮는 바탕. 누르면 창을 닫는다. 비워도 된다.")]
        [SerializeField] private Button dimBackground;

        [SerializeField] private Button toggleButton;
        [SerializeField] private Text toggleLabel;

        [Tooltip("창이 열려 있는 동안 누를 수 없게 할 버튼(나가기). 설정을 보다가 실수로 방을 나가지 않게 한다.")]
        [SerializeField] private Button[] blockedWhileOpen = System.Array.Empty<Button>();

        [SerializeField] private Color onColor = new(0.85f, 0.55f, 0.20f, 1f);
        [SerializeField] private Color offColor = new(0.22f, 0.25f, 0.30f, 1f);

        // 판이 끝나 룸 씬을 다시 불러와도 마지막으로 고른 상태를 이어가도록 씬 밖에 기억한다.
        private static bool isOpenRemembered;

        /// <summary>
        /// 버튼을 연결하고, 지난번 상태로 창을 연다.
        /// </summary>
        private void Start()
        {
            toggleButton.onClick.AddListener(Toggle);
            if (dimBackground != null)
                dimBackground.onClick.AddListener(() => SetOpen(false));
            Loc.LanguageChanged += RefreshLabel;
            SetOpen(isOpenRemembered);
        }

        /// <summary>
        /// 정적 이벤트라 끊지 않으면 파괴된 화면을 부른다.
        /// </summary>
        private void OnDestroy()
        {
            Loc.LanguageChanged -= RefreshLabel;
        }

        /// <summary>
        /// 버튼 하나로 켜고 끈다.
        /// </summary>
        private void Toggle()
        {
            SetOpen(!window.activeSelf);
        }

        /// <summary>창과 어두운 바탕을 함께 켜고 끄고, 버튼에 지금 상태를 보인다.</summary>
        private void SetOpen(bool isOpen)
        {
            isOpenRemembered = isOpen;
            window.SetActive(isOpen);
            if (dimBackground != null)
                dimBackground.gameObject.SetActive(isOpen);
            foreach (Button button in blockedWhileOpen)
            {
                if (button != null)
                    button.interactable = !isOpen;
            }
            RefreshLabel();
        }

        /// <summary>
        /// 켜졌는지 한눈에 알도록 문구와 색을 함께 바꾼다.
        /// </summary>
        private void RefreshLabel()
        {
            bool isOpen = window.activeSelf;
            if (toggleLabel != null)
                toggleLabel.text = Loc.Get(isOpen ? "room.settingsWindow.on" : "room.settingsWindow.off");
            if (toggleButton.targetGraphic != null)
                toggleButton.targetGraphic.color = isOpen ? onColor : offColor;
        }
    }
}
