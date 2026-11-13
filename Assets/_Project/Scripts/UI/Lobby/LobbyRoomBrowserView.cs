using System.Collections.Generic;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Mods;
using GuardVsMannequin.Network;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>검색은 인터페이스에 맡기고 공개방 목록·새로고침·빠른 참가·필터만 맡는 화면.</summary>
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
        private bool isQuickJoinPending;

        /// <summary>필터는 받은 목록을 다시 거르기만 하면 되므로 검색 없이 줄만 다시 만든다.</summary>
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
        /// 세션 서비스와 검색기는 로비 씬을 다시 불러와도 남으므로, 끊지 않으면 파괴된 화면을 부른다.
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
        /// Steam 상태와 검색 진행 여부는 알림 없이 바뀌므로 매 프레임 확인해, 실패하거나 겹칠 버튼을 미리 막는다.
        /// </summary>
        private void Update()
        {
            ISessionService session = SessionServices.Session;
            if (session == null)
                return;

            openButton.interactable = session.CanOpenSteamMenu();
            refreshButton.interactable = session.CanConnectSteam() && (browser == null || !browser.IsRefreshing);
            quickJoinButton.interactable = session.CanConnectSteam() && !isQuickJoinPending;
        }

        /// <summary>이전 목록은 낡았을 수 있어 열 때마다 새로 찾는다.</summary>
        private void OpenScreen()
        {
            navigator?.Show(LobbyScreen.Browse);
            isQuickJoinPending = false;
            if (!StartRefresh())
                Rebuild();
        }

        /// <summary>
        /// 호출하는 쪽이 결과를 기다릴지 지금 목록을 쓸지 정할 수 있도록 새 검색을 시작했는지 돌려준다.
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
        /// Steam이 늦게 켜질 수 있어 시작할 때가 아니라 처음 쓸 때 검색기를 가져온다.
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

        /// <summary>낡은 목록에서 고르면 찬 방에 들어가려 할 수 있어 새로 찾은 뒤 고른다.</summary>
        private void QuickJoin()
        {
            if (StartRefresh())
                isQuickJoinPending = true;
            else
                JoinBestRoom();
        }

        /// <summary>
        /// 고르는 기준은 테스트할 수 있도록 <see cref="RoomListingRules"/>에 두고, 여기서는 결과만 처리한다.
        /// </summary>
        private void JoinBestRoom()
        {
            isQuickJoinPending = false;
            if (browser != null && RoomListingRules.TryPickQuickJoin(browser.Rooms, out RoomListing room))
                Join(room);
            else
                statusText.text = Loc.Get("lobby.browse.noQuickJoin");
        }

        /// <summary>
        /// 암호 방이면 세션 서비스가 암호 입력 화면을 띄우므로 여기서는 빈 암호로 요청만 한다.
        /// </summary>
        private static void Join(RoomListing room)
        {
            SessionServices.Session?.JoinSteamRoom(room.lobbyId.ToString(), string.Empty);
        }

        /// <summary>
        /// 빠른 참가는 검색 결과를 기다리고 있으므로 목록이 오면 이어서 참가한다.
        /// </summary>
        private void OnListUpdated()
        {
            Rebuild();
            if (isQuickJoinPending)
                JoinBestRoom();
        }

        /// <summary>
        /// 실패한 뒤 다음 목록이 왔을 때 몰래 참가하지 않도록 빠른 참가 대기를 취소한다.
        /// </summary>
        private void OnFailed(string noticeKey)
        {
            isQuickJoinPending = false;
            statusText.text = Loc.Get(noticeKey);
        }

        /// <summary>
        /// 방 수가 적고 자주 바뀌지 않아 줄을 재사용하지 않고 통째로 다시 만든다.
        /// </summary>
        private void Rebuild()
        {
            foreach (GameObject row in rows)
                Destroy(row);
            rows.Clear();

            if (browser == null)
                return;

            List<RoomListing> shownRooms = RoomListingRules.Filter(browser.Rooms, hidePasswordToggle.isOn, hideMissingMapToggle.isOn);
            foreach (RoomListing room in shownRooms)
                AddRow(room);
            statusText.text = shownRooms.Count > 0 ? Loc.Format("lobby.browse.count", shownRooms.Count) : Loc.Get("lobby.browse.empty");
        }

        /// <summary>
        /// 방 이름은 다른 사람이 적은 값이라 태그로 화면을 깨뜨리지 못하도록 리치 텍스트를 끈다.
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

        /// <summary>들어가기 전에 판단하도록 이름·맵·인원·핑과 암호 표시를 한 줄에 보여 준다.</summary>
        private static string Describe(RoomListing room)
        {
            string roomName = room.name.Length > 0 ? room.name : Loc.Get("lobby.browse.unnamed");
            string mapName = ModService.Instance.FindMapName(room.mapId) ?? Loc.Get(room.mapId.Length > 0 ? "lobby.browse.modMap" : "lobby.browse.unknownMap");
            string ping = room.pingMs >= 0 ? $"{room.pingMs}ms" : "-";
            string passwordTag = room.hasPassword ? "  " + Loc.Get("lobby.browse.tag.password") : string.Empty;
            string missingMapTag = room.hasMap ? string.Empty : "  " + Loc.Get("lobby.browse.tag.missingMap");
            return $"{roomName}   |   {mapName}   |   {room.playerCount}/{room.maxPlayers}   |   {ping}{passwordTag}{missingMapTag}";
        }
    }
}
