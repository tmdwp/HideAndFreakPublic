using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Player;
using GuardVsMannequin.Room;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 테스트 방의 게임 중 테스트 메뉴. 테스트 방 호스트에게만 보인다. (<see cref="GameServices.TestRoom"/>이 있을 때)
    /// - F2: 메뉴 열기/닫기 (열려 있는 동안 커서를 풀어 게임 입력을 막는다)
    /// - F3: 1인칭 ↔ 3인칭 (메뉴 버튼으로도 가능)
    /// - 가드 ↔ 마네킹, 마네킹 ↔ 구울(사냥 시간 켜고 끄기), 게임 설정 변경
    /// - 룸으로 돌아가기 (테스트 방은 승패가 나지 않으므로)
    /// </summary>
    public class HudTestRoomPanel : MonoBehaviour
    {
        [Tooltip("테스트 방이면 항상 보이는 안내 (\"F2 테스트 메뉴\").")]
        [SerializeField] private GameObject openHint;

        [SerializeField] private GameObject panel;
        [SerializeField] private Text statusText;
        [SerializeField] private Button swapRoleButton;
        [SerializeField] private Button huntButton;
        [SerializeField] private Button perspectiveButton;
        [SerializeField] private Button closeButton;

        [Tooltip("판을 끝내고 룸으로 돌아가는 버튼. (맵을 바꿀 때)")]
        [SerializeField] private Button returnToRoomButton;

        [Tooltip("게임 설정 줄. 진행 중인 판의 설정을 바꾼다.")]
        [SerializeField] private SettingRowView[] settingRows;

        /// <summary>
        /// 버튼과 설정 줄을 연결하고 메뉴를 닫아 둔다.
        /// </summary>
        private void Start()
        {
            swapRoleButton.onClick.AddListener(() => GameServices.TestRoom?.SwapLocalRole());
            huntButton.onClick.AddListener(() => GameServices.TestRoom?.ToggleHunt());
            perspectiveButton.onClick.AddListener(TogglePerspective);
            closeButton.onClick.AddListener(Close);
            if (returnToRoomButton != null)
                returnToRoomButton.onClick.AddListener(() => GameServices.TestRoom?.ReturnToRoom());
            foreach (SettingRowView row in settingRows)
                row.Bind(OnAdjustSetting, OnSetSettingValue);

            panel.SetActive(false);
            openHint.SetActive(false);
        }

        /// <summary>
        /// 테스트 방일 때만 단축키를 받고, 열려 있으면 상태와 설정 값을 갱신한다.
        /// </summary>
        private void Update()
        {
            ITestRoomActions test = GameServices.TestRoom;
            bool available = test != null;
            if (openHint.activeSelf != available)
                openHint.SetActive(available);
            if (!available)
            {
                if (panel.activeSelf)
                    panel.SetActive(false);
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f2Key.wasPressedThisFrame)
            {
                if (panel.activeSelf)
                    Close();
                else
                    Open();
            }
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
                TogglePerspective();

            if (!panel.activeSelf)
                return;

            // 아바타를 다시 스폰하면 입력 컴포넌트가 커서를 잠그므로, 메뉴가 열려 있는 동안 다시 푼다.
            if (GameCursor.IsLocked)
                GameCursor.SetLocked(false);

            statusText.text = Loc.Format("test.status", Loc.Get(RoleKey(test)), Loc.Get(StateKey(test.CurrentState)));
            swapRoleButton.interactable = test.CanSwitch;
            huntButton.interactable = test.CanSwitch;
            perspectiveButton.interactable = LocalPlayerEvents.Current != null;
            foreach (SettingRowView row in settingRows)
                row.Refresh(test.Settings, true);
        }

        /// <summary>
        /// 메뉴를 열고 커서를 푼다.
        /// </summary>
        private void Open()
        {
            panel.SetActive(true);
            GameCursor.SetLocked(false);
        }

        /// <summary>
        /// 메뉴를 닫고, 조종할 아바타가 있으면 커서를 다시 잠근다.
        /// </summary>
        private void Close()
        {
            panel.SetActive(false);
            if (LocalPlayerEvents.Current != null && GameServices.CurrentStateOrWaiting != GameState.Ended)
                GameCursor.SetLocked(true);
        }

        /// <summary>
        /// 내 아바타의 1인칭 ↔ 3인칭을 바꾼다.
        /// </summary>
        private static void TogglePerspective()
        {
            PerspectiveSwitcher.Toggle(LocalPlayerEvents.Current);
        }

        /// <summary>
        /// 설정 항목을 한 단계 바꿔 진행 중인 판에 적용한다.
        /// </summary>
        private static void OnAdjustSetting(GameSettingId id, int direction)
        {
            ITestRoomActions test = GameServices.TestRoom;
            test?.ApplySettings(GameSettingDefinitions.Adjust(test.Settings, id, direction));
        }

        /// <summary>
        /// 키보드로 입력한 설정 값을 진행 중인 판에 적용한다.
        /// </summary>
        private static void OnSetSettingValue(GameSettingId id, float value)
        {
            ITestRoomActions test = GameServices.TestRoom;
            test?.ApplySettings(GameSettingDefinitions.SetClamped(test.Settings, id, value));
        }

        /// <summary>
        /// 내 모습의 문구 key. 사냥 시간의 마네킹은 구울로 표시한다.
        /// </summary>
        private static string RoleKey(ITestRoomActions test)
        {
            PlayerIdentity avatar = LocalPlayerEvents.Current;
            if (avatar == null)
                return "test.role.none";
            if (avatar.Role == PlayerRole.Guard)
                return "test.role.guard";
            return test.CurrentState == GameState.Hunting ? "test.role.ghoul" : "test.role.mannequin";
        }

        /// <summary>
        /// 단계 문구 key.
        /// </summary>
        private static string StateKey(GameState state)
        {
            return state switch
            {
                GameState.Playing => "test.state.playing",
                GameState.Hunting => "test.state.hunting",
                _ => "test.state.other"
            };
        }
    }
}
