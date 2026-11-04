using System.Collections.Generic;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Mods;
using GuardVsMannequin.Network;
using GuardVsMannequin.Room;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 게스트에게 고른 맵의 모드가 없을 때의 설치 안내 창. 호스트가 시작을 눌렀다가 중단되면 자동으로 열리고,
    /// 그 전에는 위쪽 안내 버튼으로 열 수 있다. 링크는 호스트가 보낸 값이므로, 허용한 사이트만 전체 주소를 보여 주고 확인을 받은 뒤 연다.
    /// </summary>
    public class RoomModRequiredPopup : MonoBehaviour
    {
        [SerializeField] private GameObject popupPanel;
        [SerializeField] private Button openButton;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private Button rowTemplate;
        [SerializeField] private Button recheckButton;
        [SerializeField] private Button closeButton;

        [Header("링크 열기 확인")]
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private Text confirmUrlText;
        [SerializeField] private Button confirmOpenButton;
        [SerializeField] private Button confirmCancelButton;

        private readonly List<GameObject> rows = new();
        private IReadOnlyList<MissingMod> shownMods;
        private string pendingUrl;

        /// <summary>
        /// 버튼을 연결하고 창을 닫아 둔다.
        /// </summary>
        private void Start()
        {
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            recheckButton.onClick.AddListener(Recheck);
            confirmOpenButton.onClick.AddListener(OpenPendingLink);
            confirmCancelButton.onClick.AddListener(CloseConfirm);
            rowTemplate.gameObject.SetActive(false);
            popupPanel.SetActive(false);
            confirmPanel.SetActive(false);
            openButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// 언어가 바뀌면 줄을 다시 만든다.
        /// </summary>
        private void OnEnable()
        {
            Loc.LanguageChanged += Rebuild;
        }

        /// <summary>
        /// 언어 변경 구독을 해제한다.
        /// </summary>
        private void OnDisable()
        {
            Loc.LanguageChanged -= Rebuild;
        }

        /// <summary>
        /// 시작 중단 알림이 오면 창을 열고, 모드 확인 결과가 바뀌면 안내 버튼과 목록을 갱신한다.
        /// </summary>
        private void Update()
        {
            RoomState room = RoomState.Instance;
            if (room == null)
                return;

            if (room.TryTakeStartBlocked())
                Open();

            IReadOnlyList<MissingMod> missing = room.LocalMissingMods;
            openButton.gameObject.SetActive(missing.Count > 0);
            if (popupPanel.activeSelf && !ReferenceEquals(missing, shownMods))
                Rebuild();
        }

        /// <summary>
        /// 창을 연다.
        /// </summary>
        private void Open()
        {
            Rebuild();
            CloseConfirm();
            popupPanel.SetActive(true);
            popupPanel.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 창을 닫는다.
        /// </summary>
        private void Close()
        {
            CloseConfirm();
            popupPanel.SetActive(false);
        }

        /// <summary>
        /// 모드 폴더를 다시 읽어 설치한 모드를 찾는다.
        /// </summary>
        private static void Recheck()
        {
            RoomState.Instance?.RecheckLocalMods();
        }

        /// <summary>
        /// 쓸 수 없는 모드 줄을 다시 만든다. 링크가 있는 줄만 누를 수 있다.
        /// </summary>
        private void Rebuild()
        {
            RoomState room = RoomState.Instance;
            if (room == null)
                return;

            foreach (GameObject row in rows)
                Destroy(row);
            rows.Clear();

            shownMods = room.LocalMissingMods;
            if (shownMods.Count == 0)
                AddRow(Loc.Get("room.modRequired.allGood"), string.Empty);
            foreach (MissingMod mod in shownMods)
                AddRow(Describe(mod), mod.safeUrl);
        }

        /// <summary>
        /// 줄 하나를 더한다.
        /// </summary>
        private void AddRow(string label, string url)
        {
            Button row = Instantiate(rowTemplate, listContent);
            row.gameObject.SetActive(true);
            // 호스트가 보낸 문자열이 들어가므로 리치 텍스트를 끈다.
            Text text = row.GetComponentInChildren<Text>();
            text.supportRichText = false;
            text.text = label;
            row.interactable = !string.IsNullOrEmpty(url);
            if (row.interactable)
                row.onClick.AddListener(() => ShowConfirm(url));
            rows.Add(row.gameObject);
        }

        /// <summary>
        /// 모드 한 줄의 문구: 이름 v버전 (ID) - 이유, 그 아래에 링크 안내.
        /// </summary>
        private static string Describe(MissingMod mod)
        {
            string version = string.IsNullOrEmpty(mod.hostVersion) ? string.Empty : $" v{mod.hostVersion}";
            string title = mod.displayName == mod.id ? mod.displayName + version : $"{mod.displayName}{version} ({mod.id})";
            string link = Loc.Get(string.IsNullOrEmpty(mod.safeUrl) ? "room.modRequired.noLink" : "room.modRequired.link");
            return $"{title}  -  {DescribeReason(mod)}\n  └ {link}";
        }

        /// <summary>
        /// 모드를 쓸 수 없는 이유 문구.
        /// </summary>
        private static string DescribeReason(MissingMod mod)
        {
            return mod.reason switch
            {
                MissingModReason.VersionDiffers => Loc.Format("room.modRequired.reason.version", mod.local.Manifest.version),
                MissingModReason.FilesDiffer => Loc.Get("room.modRequired.reason.files"),
                MissingModReason.Unusable => Loc.Format("room.modRequired.reason.error", mod.local.Error),
                _ => Loc.Get("room.modRequired.reason.notInstalled")
            };
        }

        /// <summary>
        /// 전체 주소를 보여 주고 열지 묻는다.
        /// </summary>
        private void ShowConfirm(string url)
        {
            pendingUrl = url;
            confirmUrlText.supportRichText = false;
            confirmUrlText.text = url;
            confirmPanel.SetActive(true);
            confirmPanel.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 확인 창을 닫는다.
        /// </summary>
        private void CloseConfirm()
        {
            pendingUrl = null;
            confirmPanel.SetActive(false);
        }

        /// <summary>
        /// 확인한 링크를 브라우저로 연다. 여는 순간에도 규칙을 다시 확인한다.
        /// </summary>
        private void OpenPendingLink()
        {
            if (ModLinkPolicy.TryGetSafeUrl(pendingUrl, out string safeUrl))
                Application.OpenURL(safeUrl);
            CloseConfirm();
        }
    }
}
