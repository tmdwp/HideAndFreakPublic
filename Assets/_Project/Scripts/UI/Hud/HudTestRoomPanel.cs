using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Player;
using GuardVsMannequin.Room;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>테스트 방 호스트에게만 보이는 게임 중 테스트 메뉴.</summary>
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

        private RefreshTimer refreshTimer;

        /// <summary>테스트 방이 아니면 보이지 않도록 닫아 두고, 예전 프리팹에 없는 버튼은 있을 때만 연결한다.</summary>
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

        /// <summary>테스트 방은 판 중간에 생기거나 사라질 수 있어 매 프레임 확인하고 단축키는 키보드를 직접 읽는다.</summary>
        private void Update()
        {
            ITestRoomActions testRoom = GameServices.TestRoom;
            bool isAvailable = testRoom != null;
            if (openHint.activeSelf != isAvailable)
                openHint.SetActive(isAvailable);
            if (!isAvailable)
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

            // 상태 문구와 설정 줄마다 문자열을 새로 만들므로 매 프레임이 아니라 일정 간격으로만 갱신한다.
            if (!refreshTimer.Tick())
                return;

            statusText.text = Loc.Format("test.status", Loc.Get(RoleKey(testRoom)), Loc.Get(StateKey(testRoom.CurrentState)));
            swapRoleButton.interactable = testRoom.CanSwitch;
            huntButton.interactable = testRoom.CanSwitch;
            perspectiveButton.interactable = LocalPlayerEvents.Current != null;
            foreach (SettingRowView row in settingRows)
                row.Refresh(testRoom.Settings, true);
        }

        /// <summary>버튼을 누를 수 있도록 커서를 풀어 게임 입력을 막는다.</summary>
        private void Open()
        {
            panel.SetActive(true);
            GameCursor.SetLocked(false);
            // 열자마자 지난번 값이 보이지 않도록 다음 프레임에 바로 갱신한다.
            refreshTimer.Reset();
        }

        /// <summary>
        /// 관전 중이나 결과 화면에서 커서를 잠그면 버튼을 누를 수 없으므로 조종할 아바타가 있을 때만 다시 잠근다.
        /// </summary>
        private void Close()
        {
            panel.SetActive(false);
            if (LocalPlayerEvents.Current != null && GameServices.CurrentStateOrWaiting != GameState.Ended)
                GameCursor.SetLocked(true);
        }

        /// <summary>
        /// 단축키와 버튼이 같은 동작을 쓰도록 한곳에 둔다.
        /// </summary>
        private static void TogglePerspective()
        {
            PerspectiveSwitcher.Toggle(LocalPlayerEvents.Current);
        }

        /// <summary>
        /// 테스트 중 설정을 바꿔 보려고 룸으로 돌아가지 않아도 진행 중인 판에 바로 적용한다.
        /// </summary>
        private static void OnAdjustSetting(GameSettingId id, int direction)
        {
            ITestRoomActions testRoom = GameServices.TestRoom;
            testRoom?.ApplySettings(GameSettingDefinitions.Adjust(testRoom.Settings, id, direction));
        }

        /// <summary>
        /// 범위를 벗어난 값으로 판이 깨지지 않도록 보정한 뒤 적용한다.
        /// </summary>
        private static void OnSetSettingValue(GameSettingId id, float value)
        {
            ITestRoomActions testRoom = GameServices.TestRoom;
            testRoom?.ApplySettings(GameSettingDefinitions.SetClamped(testRoom.Settings, id, value));
        }

        /// <summary>
        /// 사냥 시간의 마네킹은 구울로 바뀌어 다르게 움직이므로 구울로 표시한다.
        /// </summary>
        private static string RoleKey(ITestRoomActions testRoom)
        {
            PlayerIdentity avatar = LocalPlayerEvents.Current;
            if (avatar == null)
                return "test.role.none";
            if (avatar.Role == PlayerRole.Guard)
                return "test.role.guard";
            return testRoom.CurrentState == GameState.Hunting ? "test.role.ghoul" : "test.role.mannequin";
        }

        /// <summary>
        /// 테스트 방에서 의미 있는 단계는 진행과 사냥뿐이라 나머지는 "기타"로 묶는다.
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
