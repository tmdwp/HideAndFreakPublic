using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Mods;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>게임을 끄지 않고 모드를 켜고 끄거나 다시 읽도록 설치된 모드 목록을 보여 준다.</summary>
    public class LobbyModsMenu : MonoBehaviour
    {
        [Tooltip("메인 화면의 '모드' 버튼.")]
        [SerializeField] private Button openButton;

        [Tooltip("모드 줄을 넣을 스크롤 목록의 Content.")]
        [SerializeField] private RectTransform listContent;

        [Tooltip("모드 한 줄의 원본(꺼진 상태로 둔다). 모드마다 복제한다.")]
        [SerializeField] private Button rowTemplate;

        [Tooltip("켜진 모드 수와 모드 폴더 안내.")]
        [SerializeField] private Text summaryText;

        [SerializeField] private Button refreshButton;
        [SerializeField] private Button openFolderButton;

        private readonly List<GameObject> rows = new();

        /// <summary>결과가 바로 보이도록 모드 목록과 언어가 바뀔 때마다 목록을 다시 만든다.</summary>
        private void Start()
        {
            ILobbyNavigator navigator = UiLookup.FindInParent<ILobbyNavigator>(this);
            openButton.onClick.AddListener(() => navigator?.Show(LobbyScreen.Mods));
            refreshButton.onClick.AddListener(() => ModService.Instance.Refresh());
            openFolderButton.onClick.AddListener(OpenModsFolder);

            rowTemplate.gameObject.SetActive(false);
            ModService.Instance.Changed += Rebuild;
            Loc.LanguageChanged += Rebuild;
            Rebuild();
        }

        /// <summary>
        /// 모드 서비스는 씬보다 오래 살아서, 끊지 않으면 파괴된 화면을 부른다.
        /// </summary>
        private void OnDestroy()
        {
            ModService.Instance.Changed -= Rebuild;
            Loc.LanguageChanged -= Rebuild;
        }

        /// <summary>모드 수가 자주 바뀌지 않아 줄을 통째로 다시 만들고, 오류 모드는 누를 수 없게 한다.</summary>
        private void Rebuild()
        {
            foreach (GameObject row in rows)
                Destroy(row);
            rows.Clear();

            IReadOnlyList<ModInfo> mods = ModService.Instance.Mods;
            if (mods.Count == 0)
                AddRow(Loc.Get("lobby.mods.empty"), null);
            foreach (ModInfo mod in mods)
                AddRow(Describe(mod), mod.Status == ModStatus.Error ? null : mod);

            int activeCount = mods.Count(installedMod => installedMod.Status == ModStatus.Active);
            summaryText.text = Loc.Format("lobby.mods.summary", activeCount, mods.Count, ModScanner.SearchFolders[0]);
        }

        /// <summary>
        /// 안내 줄과 모드 줄을 같은 모양으로 보이려고 같은 원본을 쓰며, 모드가 null이면 누를 수 없는 안내 줄로 만든다.
        /// </summary>
        private void AddRow(string label, ModInfo mod)
        {
            Button row = Instantiate(rowTemplate, listContent);
            row.gameObject.SetActive(true);
            row.GetComponentInChildren<Text>().text = label;
            row.interactable = mod != null;
            if (mod != null)
                row.onClick.AddListener(() => ModService.Instance.SetEnabled(mod.Id, mod.Status == ModStatus.Disabled));
            rows.Add(row.gameObject);
        }

        /// <summary>
        /// 같은 이름의 모드를 구별하고 오류를 고칠 수 있도록 버전·ID와 오류 이유를 함께 보여 준다.
        /// </summary>
        private static string Describe(ModInfo mod)
        {
            string stateLabel = mod.Status switch
            {
                ModStatus.Active => Loc.Get("lobby.mods.state.active"),
                ModStatus.Disabled => Loc.Get("lobby.mods.state.disabled"),
                _ => Loc.Get("lobby.mods.state.error")
            };
            string version = mod.Manifest?.version ?? "?";
            string line = $"{stateLabel} {mod.DisplayName} v{version}  ({mod.Id})";
            return mod.Status == ModStatus.Error ? $"{line}\n  └ {mod.Error}" : line;
        }

        /// <summary>처음 설치하는 사람은 폴더가 없으므로 만든 뒤 연다.</summary>
        private static void OpenModsFolder()
        {
            string folder = ModScanner.SearchFolders[0];
            try
            {
                Directory.CreateDirectory(folder);
                Application.OpenURL("file:///" + folder.Replace('\\', '/'));
            }
            catch (System.Exception exception) when (exception is IOException || exception is System.UnauthorizedAccessException)
            {
                Debug.LogWarning($"[LobbyModsMenu] 모드 폴더를 열 수 없습니다: {folder} ({exception.Message})");
            }
        }
    }
}
