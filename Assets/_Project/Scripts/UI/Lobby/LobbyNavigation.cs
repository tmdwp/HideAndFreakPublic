namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 로비 화면(패널) 종류. 화면을 추가하면 여기에 값을 더하고, <see cref="LobbyView"/>의 화면 목록에 패널과 부모 화면을 등록한다.
    /// (프리팹에 번호로 저장되므로 새 값은 끝에만 추가한다)
    /// </summary>
    public enum LobbyScreen
    {
        Main,
        Create,
        SteamCreate,
        Join,
        SteamJoin,
        DirectJoin,
        Mods,
        DirectCreate
    }

    /// <summary>
    /// 로비 화면 이동. 각 메뉴 코드(메인/방 만들기/참가)는 이 인터페이스로만 화면을 바꾼다.
    /// </summary>
    public interface ILobbyNavigator
    {
        /// <summary>화면 하나만 보이게 한다.</summary>
        void Show(LobbyScreen screen);

        /// <summary>이전(부모) 화면으로 돌아간다.</summary>
        void Back();

        /// <summary>화면 목록에 그 화면이 있는지 여부. (예전 프리팹에는 새 화면이 없을 수 있음)</summary>
        bool HasScreen(LobbyScreen screen);
    }

    /// <summary>
    /// 로비 화면 위에 겹쳐 여는 패널(설정, 크레딧).
    /// </summary>
    public interface ILobbyOverlays
    {
        /// <summary>설정 화면을 연다.</summary>
        void OpenSettings();

        /// <summary>크레딧 화면을 연다.</summary>
        void OpenCredits();
    }
}
