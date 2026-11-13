using System.Collections.Generic;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Room;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>룸과 ESC 메뉴가 함께 쓰는 호스트의 플레이어 추방 창.</summary>
    public class HostPlayerPanel : MonoBehaviour
    {
        [Tooltip("창을 여는 버튼들. 호스트에게만 보인다.")]
        [SerializeField] private Button[] openButtons;
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private Button rowTemplate;
        [SerializeField] private Button closeButton;

        [Header("내보내기 확인")]
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private Text confirmText;
        [SerializeField] private Button confirmYesButton;
        [SerializeField] private Button confirmNoButton;

        private readonly List<GameObject> rows = new();
        private RefreshTimer refreshTimer;
        private string shownSignature;
        private int pendingClientId;

        /// <summary>여는 버튼이 여러 곳이라 모두 같은 창에 연결하고, 켜진 채 저장된 창은 닫아 둔다.</summary>
        private void Start()
        {
            foreach (Button openButton in openButtons)
                openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            confirmYesButton.onClick.AddListener(KickPending);
            confirmNoButton.onClick.AddListener(() => confirmPanel.SetActive(false));
            rowTemplate.gameObject.SetActive(false);
            panel.SetActive(false);
            confirmPanel.SetActive(false);
        }

        /// <summary>게스트가 누르면 서버가 거부하므로 호스트에게만 버튼을 보이고, 참가자가 바뀔 때만 다시 만든다.</summary>
        private void Update()
        {
            if (!refreshTimer.Tick())
                return;

            bool isHost = RoomClient.IsHost;
            foreach (Button openButton in openButtons)
                openButton.gameObject.SetActive(isHost);

            if (panel.activeSelf && BuildSignature() != shownSignature)
                Rebuild();
        }

        /// <summary>다른 UI에 가려지지 않도록 맨 앞으로 가져오고 남은 확인 창은 닫는다.</summary>
        private void Open()
        {
            Rebuild();
            confirmPanel.SetActive(false);
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 확인 창만 남아 있으면 엉뚱한 사람을 내보낼 수 있어 함께 닫는다.
        /// </summary>
        private void Close()
        {
            confirmPanel.SetActive(false);
            panel.SetActive(false);
        }

        /// <summary>
        /// 참가자 목록이 바뀌었는지 싸게 비교하려고 ID와 이름을 한 문자열로 묶는다.
        /// </summary>
        private static string BuildSignature()
        {
            RoomState room = RoomState.Instance;
            if (room == null)
                return string.Empty;

            var builder = new System.Text.StringBuilder();
            foreach (KeyValuePair<int, RoomMember> member in room.Members)
                builder.Append(member.Key).Append(':').Append(room.GetDisplayName(member.Key)).Append(';');
            return builder.ToString();
        }

        /// <summary>호스트는 자기 자신을 내보낼 수 없으므로 목록에서 뺀다.</summary>
        private void Rebuild()
        {
            foreach (GameObject row in rows)
                Destroy(row);
            rows.Clear();

            shownSignature = BuildSignature();
            RoomState room = RoomState.Instance;
            if (room == null)
                return;

            foreach (KeyValuePair<int, RoomMember> member in room.Members)
            {
                if (!member.Value.isHost)
                    AddRow(member.Key, room.GetDisplayName(member.Key));
            }
            if (rows.Count == 0)
                AddEmptyRow();
        }

        /// <summary>
        /// 실수로 내보내지 않도록 누르면 바로 내보내지 않고 확인 창을 띄운다.
        /// </summary>
        private void AddRow(int clientId, string displayName)
        {
            Button row = CreateRow(displayName);
            row.onClick.AddListener(() => ShowConfirm(clientId, displayName));
        }

        /// <summary>
        /// 안내 줄은 누를 대상이 없으므로 비활성으로 만든다.
        /// </summary>
        private void AddEmptyRow()
        {
            CreateRow(Loc.Get("room.players.empty")).interactable = false;
        }

        /// <summary>
        /// 이름은 다른 플레이어가 정한 값이라 태그로 화면을 깨뜨리지 못하도록 리치 텍스트를 끈다.
        /// </summary>
        private Button CreateRow(string label)
        {
            Button row = Instantiate(rowTemplate, listContent);
            row.gameObject.SetActive(true);
            Text rowText = row.GetComponentInChildren<Text>();
            rowText.supportRichText = false;
            rowText.text = label;
            rows.Add(row.gameObject);
            return row;
        }

        /// <summary>확인 문구에 다른 사람이 정한 이름이 들어가므로 리치 텍스트를 끈다.</summary>
        private void ShowConfirm(int clientId, string displayName)
        {
            pendingClientId = clientId;
            confirmText.supportRichText = false;
            confirmText.text = Loc.Format("room.players.confirm", displayName);
            confirmPanel.SetActive(true);
            confirmPanel.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 확인 창을 연 사이 목록이 바뀌어도 맞는 사람을 내보내도록 이름이 아닌 접속 ID로 요청한다.
        /// </summary>
        private void KickPending()
        {
            confirmPanel.SetActive(false);
            RoomState.Instance?.Kick(pendingClientId);
        }
    }
}
