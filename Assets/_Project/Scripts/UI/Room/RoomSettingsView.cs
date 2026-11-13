using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Room;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>모두에게 값이 보이고 호스트만 조정하는 룸 게임 설정 목록.</summary>
    public class RoomSettingsView : MonoBehaviour
    {
        [SerializeField] private Text settingsHintText;
        [SerializeField] private SettingRowView[] settingRows;

        private RefreshTimer refreshTimer;

        /// <summary>
        /// 예전에 만든 프리팹에는 지금은 숨기는 항목의 줄이 남아 있을 수 있어 끄고, 갱신 대상에서도 뺀다.
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

        /// <summary>변경 알림이 없어 주기적으로 다시 그리되 가비지를 줄이려고 일정 간격으로만 갱신한다.</summary>
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

        /// <summary>게스트가 바꾸지 못하도록 한 번 더 확인한 뒤 호스트가 직접 호출한다.</summary>
        private static void OnAdjustSetting(GameSettingId id, int direction)
        {
            if (RoomClient.IsHost && RoomState.Instance != null)
                RoomState.Instance.AdjustSetting(id, direction);
        }

        /// <summary>
        /// 범위·인원 조건은 서버가 한곳에서 보정하므로 여기서는 입력값을 그대로 넘긴다.
        /// </summary>
        private static void OnSetSettingValue(GameSettingId id, float value)
        {
            if (RoomClient.IsHost && RoomState.Instance != null)
                RoomState.Instance.SetSettingValue(id, value);
        }
    }
}
