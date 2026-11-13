using System.Collections.Generic;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Mods;
using GuardVsMannequin.Network;
using GuardVsMannequin.Room;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>게스트가 고른 맵의 모드를 직접 설치할 수 있도록 안내 창을 보여 준다.</summary>
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

        /// <summary>켜진 채 저장된 창이 바로 보이지 않도록 모두 닫아 둔다.</summary>
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
        /// 줄 문구는 코드로 채우므로 언어가 바뀌면 다시 만들어야 한다.
        /// </summary>
        private void OnEnable()
        {
            Loc.LanguageChanged += Rebuild;
        }

        /// <summary>
        /// 꺼진 동안 언어 변경이 이 객체를 부르지 않도록 끊는다.
        /// </summary>
        private void OnDisable()
        {
            Loc.LanguageChanged -= Rebuild;
        }

        /// <summary>시작 중단은 한 번만 알리도록 꺼내 쓰고, 결과 목록이 바뀌었을 때만 다시 만든다.</summary>
        private void Update()
        {
            RoomState room = RoomState.Instance;
            if (room == null)
                return;

            if (room.TryTakeStartBlocked())
                Open();

            IReadOnlyList<MissingMod> missingMods = room.LocalMissingMods;
            openButton.gameObject.SetActive(missingMods.Count > 0);
            if (popupPanel.activeSelf && !ReferenceEquals(missingMods, shownMods))
                Rebuild();
        }

        /// <summary>다른 UI에 가려지지 않도록 맨 앞으로 가져오고 남은 확인 창은 닫는다.</summary>
        private void Open()
        {
            Rebuild();
            CloseConfirm();
            popupPanel.SetActive(true);
            popupPanel.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 확인 창만 남아 있으면 엉뚱한 링크를 열 수 있어 함께 닫는다.
        /// </summary>
        private void Close()
        {
            CloseConfirm();
            popupPanel.SetActive(false);
        }

        /// <summary>
        /// 게임을 껐다 켜지 않고도 방금 설치한 모드를 반영하도록 모드 폴더를 다시 읽는다.
        /// </summary>
        private static void Recheck()
        {
            RoomState.Instance?.RecheckLocalMods();
        }

        /// <summary>줄 수가 적어 통째로 다시 만들고, 모두 설치되면 다 됐다는 안내를 보여 준다.</summary>
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
        /// 허용하지 않은 링크는 열면 안 되므로 안전한 주소가 있는 줄만 누를 수 있게 한다.
        /// </summary>
        private void AddRow(string label, string url)
        {
            Button row = Instantiate(rowTemplate, listContent);
            row.gameObject.SetActive(true);
            // 호스트가 보낸 문자열이 들어가므로 리치 텍스트를 끈다.
            Text rowText = row.GetComponentInChildren<Text>();
            rowText.supportRichText = false;
            rowText.text = label;
            row.interactable = !string.IsNullOrEmpty(url);
            if (row.interactable)
                row.onClick.AddListener(() => ShowConfirm(url));
            rows.Add(row.gameObject);
        }

        /// <summary>이름이 같은 모드를 구별하도록 이름과 ID가 다를 때 ID를 덧붙인다.</summary>
        private static string Describe(MissingMod mod)
        {
            string version = string.IsNullOrEmpty(mod.hostVersion) ? string.Empty : $" v{mod.hostVersion}";
            string title = mod.displayName == mod.id ? mod.displayName + version : $"{mod.displayName}{version} ({mod.id})";
            string linkHint = Loc.Get(string.IsNullOrEmpty(mod.safeUrl) ? "room.modRequired.noLink" : "room.modRequired.link");
            return $"{title}  -  {DescribeReason(mod)}\n  └ {linkHint}";
        }

        /// <summary>
        /// 설치돼 있어도 버전이나 파일이 다르면 쓸 수 없으므로, 무엇을 고쳐야 하는지 이유별로 다르게 안내한다.
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
        /// 호스트가 보낸 주소를 바로 열면 피싱에 쓰일 수 있으므로, 전체 주소를 그대로(리치 텍스트 없이) 보여 주고 확인을 받는다.
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
        /// 취소한 링크가 나중에 열리지 않도록 대기 주소도 지운다.
        /// </summary>
        private void CloseConfirm()
        {
            pendingUrl = null;
            confirmPanel.SetActive(false);
        }

        /// <summary>
        /// 확인 창이 열린 사이 값이 바뀌었을 수 있어 여는 순간에도 규칙을 다시 확인한다.
        /// </summary>
        private void OpenPendingLink()
        {
            if (ModLinkPolicy.TryGetSafeUrl(pendingUrl, out string safeUrl))
                Application.OpenURL(safeUrl);
            CloseConfirm();
        }
    }
}
