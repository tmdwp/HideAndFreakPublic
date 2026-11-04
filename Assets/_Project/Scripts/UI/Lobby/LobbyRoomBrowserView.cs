using System.Collections.Generic;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Mods;
using GuardVsMannequin.Network;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 공개방 찾기 화면. 목록·새로고침·빠른 참가·표시 필터를 맡고, 검색은 <see cref="IRoomBrowser"/>에 맡긴다.
    /// 방 이름과 맵 이름은 로비 주인이 적은 값이라 리치 텍스트를 끄고 보여 준다.
    /// </summary>
    public class LobbyRoomBrowserView : MonoBehaviour
    {
        [SerializeField] private Button openButton;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private Button rowTemplate;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button quickJoinButton;
        [SerializeField] private Toggle hidePasswordToggle;
        [SerializeField] private Toggle hideMissingMapToggle;
        [SerializeField] private Text statusText;

        private readonly List<GameObject> rows = new();
        private ILobbyNavigator navigator;
        private IRoomBrowser browser;
        private bool quickJoinPending;

        /// <summary>
        /// 버튼과 필터를 연결한다.
        /// </summary>
        private void Start()
        {
            navigator = UiLookup.FindInParent<ILobbyNavigator>(this);
            openButton.onClick.AddListener(OpenScreen);
            refreshButton.onClick.AddListener(() => StartRefresh());
            quickJoinButton.onClick.AddListener(QuickJoin);
            hidePasswordToggle.onValueChanged.AddListener(_ => Rebuild());
            hideMissingMapToggle.onValueChanged.AddListener(_ => Rebuild());
            rowTemplate.gameObject.SetActive(false);
            Loc.LanguageChanged += Rebuild;
        }

        /// <summary>
        /// 구독을 해제한다. (세션 서비스와 검색기는 로비 씬을 다시 불러와도 남는다)
        /// </summary>
        private void OnDestroy()
        {
            Loc.LanguageChanged -= Rebuild;
            if (browser == null)
                return;
            browser.ListUpdated -= OnListUpdated;
            browser.Failed -= OnFailed;
        }

        /// <summary>
        /// Steam 연결과 진행 중인 작업 여부에 따라 버튼을 켜고 끈다.
        /// </summary>
        private void Update()
        {
            ISessionService session = SessionServices.Session;
            if (session == null)
                return;

            openButton.interactable = session.CanOpenSteamMenu();
            refreshButton.interactable = session.CanConnectSteam() && (browser == null || !browser.IsRefreshing);
            quickJoinButton.interactable = session.CanConnectSteam() && !quickJoinPending;
        }

        /// <summary>
        /// 화면을 열고 목록을 새로 찾는다.
        /// </summary>
        private void OpenScreen()
        {
            navigator?.Show(LobbyScreen.Browse);
            quickJoinPending = false;
            if (!StartRefresh())
                Rebuild();
        }

        /// <summary>
        /// 검색을 시작한다. 새 검색을 시작했으면 true.
        /// </summary>
        private bool StartRefresh()
        {
            ConnectBrowser();
            if (browser == null || !browser.Refresh())
                return false;

            statusText.text = Loc.Get("lobby.browse.searching");
            return true;
        }

        /// <summary>
        /// 검색기를 가져와 구독한다. Steam이 없으면 안내만 한다.
        /// </summary>
        private void ConnectBrowser()
        {
            if (browser != null)
                return;

            browser = SessionServices.Session?.RoomBrowser;
            if (browser == null)
            {
                statusText.text = Loc.Get("notice.steamNotRunning");
                return;
            }
            browser.ListUpdated += OnListUpdated;
            browser.Failed += OnFailed;
        }

        /// <summary>
        /// 빠른 참가: 목록을 새로 찾은 뒤 고른다. 방금 찾았으면 지금 목록에서 고른다.
        /// </summary>
        private void QuickJoin()
        {
            if (StartRefresh())
                quickJoinPending = true;
            else
                JoinBestRoom();
        }

        /// <summary>
        /// 들어갈 수 있는 방 중 1순위에 참가한다. 없으면 안내한다.
        /// </summary>
        private void JoinBestRoom()
        {
            quickJoinPending = false;
            if (browser != null && RoomListingRules.TryPickQuickJoin(browser.Rooms, out RoomListing room))
                Join(room);
            else
                statusText.text = Loc.Get("lobby.browse.noQuickJoin");
        }

        /// <summary>
        /// 방에 참가한다. 암호 방이면 세션 서비스가 암호 입력 화면을 띄운다.
        /// </summary>
        private static void Join(RoomListing room)
        {
            SessionServices.Session?.JoinSteamRoom(room.lobbyId.ToString(), string.Empty);
        }

        /// <summary>
        /// 검색이 끝나면 목록을 만들고, 빠른 참가를 기다리는 중이면 이어서 참가한다.
        /// </summary>
        private void OnListUpdated()
        {
            Rebuild();
            if (quickJoinPending)
                JoinBestRoom();
        }

        /// <summary>
        /// 검색 실패를 안내한다.
        /// </summary>
        private void OnFailed(string noticeKey)
        {
            quickJoinPending = false;
            statusText.text = Loc.Get(noticeKey);
        }

        /// <summary>
        /// 필터를 적용해 줄을 다시 만든다.
        /// </summary>
        private void Rebuild()
        {
            foreach (GameObject row in rows)
                Destroy(row);
            rows.Clear();

            if (browser == null)
                return;

            List<RoomListing> shown = RoomListingRules.Filter(browser.Rooms, hidePasswordToggle.isOn, hideMissingMapToggle.isOn);
            foreach (RoomListing room in shown)
                AddRow(room);
            statusText.text = shown.Count > 0 ? Loc.Format("lobby.browse.count", shown.Count) : Loc.Get("lobby.browse.empty");
        }

        /// <summary>
        /// 줄 하나를 더한다.
        /// </summary>
        private void AddRow(RoomListing room)
        {
            Button row = Instantiate(rowTemplate, listContent);
            row.gameObject.SetActive(true);
            Text label = row.GetComponentInChildren<Text>();
            label.supportRichText = false;
            label.text = Describe(room);
            row.onClick.AddListener(() => Join(room));
            rows.Add(row.gameObject);
        }

        /// <summary>
        /// 방 한 줄의 문구: 이름 | 맵 | 인원 | 핑, 그 뒤에 암호·모드 표시.
        /// </summary>
        private static string Describe(RoomListing room)
        {
            string name = room.name.Length > 0 ? room.name : Loc.Get("lobby.browse.unnamed");
            string map = ModService.Instance.FindMapName(room.mapId) ?? Loc.Get(room.mapId.Length > 0 ? "lobby.browse.modMap" : "lobby.browse.unknownMap");
            string ping = room.pingMs >= 0 ? $"{room.pingMs}ms" : "-";
            string password = room.hasPassword ? "  " + Loc.Get("lobby.browse.tag.password") : string.Empty;
            string missing = room.hasMap ? string.Empty : "  " + Loc.Get("lobby.browse.tag.missingMap");
            return $"{name}   |   {map}   |   {room.playerCount}/{room.maxPlayers}   |   {ping}{password}{missing}";
        }
    }
}
