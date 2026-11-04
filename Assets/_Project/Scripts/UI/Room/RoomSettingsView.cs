using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Room;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 룸 화면의 게임 설정 목록. 모두에게 값이 보이고, 호스트만 -/+ 버튼과 키보드 입력으로 조정한다.
    /// </summary>
    public class RoomSettingsView : MonoBehaviour
    {
        [SerializeField] private Text settingsHintText;
        [SerializeField] private SettingRowView[] settingRows;

        private RefreshTimer refreshTimer;

        /// <summary>
        /// 룸에서 숨기는 항목의 줄을 끄고(이전에 만든 프리팹 대비), 남은 줄의 조정 동작을 연결한다.
        /// </summary>
        private void Start()
        {
            foreach (SettingRowView row in settingRows)
            {
                if (!GameSettingDefinitions.IsShownInRoom(row.SettingId))
                    row.gameObject.SetActive(false);
            }
            settingRows = System.Array.FindAll(settingRows, row => row.gameObject.activeSelf);

            foreach (SettingRowView row in settingRows)
                row.Bind(OnAdjustSetting, OnSetSettingValue);
        }

        /// <summary>
        /// 설정 값을 표시하고 호스트에게만 조정 버튼을 켠다. (값 문구를 새로 만들므로 일정 간격으로만 갱신)
        /// </summary>
        private void Update()
        {
            RoomState room = RoomState.Instance;
            if (room == null || !refreshTimer.Tick())
                return;

            bool isHost = RoomClient.IsHost;
            settingsHintText.text = Loc.Get(isHost ? "room.settings.host" : "room.settings.guest");
            foreach (SettingRowView row in settingRows)
                row.Refresh(room.Settings, isHost);
        }

        /// <summary>
        /// 호스트가 설정을 조정한다. (호스트는 서버이므로 직접 호출)
        /// </summary>
        private static void OnAdjustSetting(GameSettingId id, int direction)
        {
            if (RoomClient.IsHost && RoomState.Instance != null)
                RoomState.Instance.AdjustSetting(id, direction);
        }

        /// <summary>
        /// 호스트가 키보드로 입력한 값을 적용한다. (범위·인원 조건은 서버가 보정)
        /// </summary>
        private static void OnSetSettingValue(GameSettingId id, float value)
        {
            if (RoomClient.IsHost && RoomState.Instance != null)
                RoomState.Instance.SetSettingValue(id, value);
        }
    }
}
