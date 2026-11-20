using GuardVsMannequin.Core;
using GuardVsMannequin.Player;
using GuardVsMannequin.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>룸과 게임이 함께 쓰는 ESC 메뉴로, 열린 동안 커서를 풀어 게임 입력을 막는다.</summary>
    public class PauseMenuView : MonoBehaviour
    {
        private const string MenuActionName = "Menu";

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private SettingsPanelView settingsPanel;

        [Tooltip("메뉴를 닫을 때 커서를 다시 잠글지 여부. (게임 씬: 켬, 룸 씬: 끔)")]
        [SerializeField] private bool lockCursorOnClose = true;

        private InputActionAsset runtimeActions;
        private InputAction menuAction;

        /// <summary>아바타가 없어도 ESC가 동작하도록 자체 입력을 만들고 복제본에 저장된 키를 적용한다.</summary>
        private void Awake()
        {
            runtimeActions = Instantiate(inputActions);
            OptionsStore.ApplyBindings(runtimeActions);
            menuAction = runtimeActions.FindAction(MenuActionName, true);

            resumeButton.onClick.AddListener(CloseMenu);
            settingsButton.onClick.AddListener(() => settingsPanel.Open());
            leaveButton.onClick.AddListener(RoomClient.RequestLeave);

            menuPanel.SetActive(false);
            settingsPanel.gameObject.SetActive(false);
        }

        /// <summary>
        /// 켜져 있는 동안만 ESC를 받는다.
        /// </summary>
        private void OnEnable()
        {
            menuAction.performed += OnMenuPressed;
            menuAction.Enable();
        }

        /// <summary>
        /// 꺼진 뒤 ESC가 이 객체를 부르지 않도록 끊는다.
        /// </summary>
        private void OnDisable()
        {
            menuAction.performed -= OnMenuPressed;
            menuAction.Disable();
        }

        /// <summary>
        /// Instantiate로 만든 에셋은 씬과 함께 지워지지 않으므로 직접 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            if (runtimeActions != null)
                Destroy(runtimeActions);
        }

        /// <summary>키 설정 중 ESC는 취소라 무시하고, 설정 화면이 열려 있으면 한 단계만 닫는다.</summary>
        private void OnMenuPressed(InputAction.CallbackContext context)
        {
            if (RebindRowView.IsAnyRebinding || ConfirmDialog.ConsumeEscape())
                return;

            if (settingsPanel.IsOpen)
                settingsPanel.Close();
            else if (menuPanel.activeSelf)
                CloseMenu();
            else
                OpenMenu();
        }

        /// <summary>버튼을 누를 수 있도록 커서를 풀어 게임 입력을 막는다.</summary>
        private void OpenMenu()
        {
            menuPanel.SetActive(true);
            GameCursor.SetLocked(false);
        }

        /// <summary>
        /// 관전 중이나 결과 화면에서 커서를 잠그면 버튼을 누를 수 없으므로, 내 아바타가 있고 결과 화면이 아닐 때만 다시 잠근다.
        /// </summary>
        private void CloseMenu()
        {
            menuPanel.SetActive(false);
            settingsPanel.gameObject.SetActive(false);

            bool isPlaying = LocalPlayerEvents.Current != null && GameServices.CurrentStateOrWaiting != GameState.Ended;
            if (lockCursorOnClose && isPlaying)
                GameCursor.SetLocked(true);
        }
    }
}
