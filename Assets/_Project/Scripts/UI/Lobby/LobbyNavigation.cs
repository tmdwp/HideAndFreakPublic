namespace GuardVsMannequin.UI
{
    /// <summary>프리팹에 번호로 저장되므로 새 값은 끝에 더하는 로비 화면 종류.</summary>
    public enum LobbyScreen
    {
        Main,
        Create,
        SteamCreate,
        Join,
        SteamJoin,
        DirectJoin,
        Mods,
        DirectCreate,
        Browse
    }

    /// <summary>메뉴 코드가 화면 구성을 몰라도 되도록 화면 이동을 인터페이스로 둔다.</summary>
    public interface ILobbyNavigator
    {
        /// <summary>패널이 겹쳐 보이지 않도록 화면 하나만 보이게 한다.</summary>
        void Show(LobbyScreen screen);

        /// <summary>버튼마다 돌아갈 곳을 따로 정하지 않도록, 화면 목록에 등록된 부모 화면으로 돌아간다.</summary>
        void Back();

        /// <summary>예전 프리팹에는 새 화면이 없을 수 있어, 메뉴가 다른 동작으로 대신할 수 있게 확인한다.</summary>
        bool HasScreen(LobbyScreen screen);
    }

    /// <summary>
    /// 로비 화면 위에 겹쳐 여는 패널(설정, 크레딧).
    /// </summary>
    public interface ILobbyOverlays
    {
        /// <summary>메뉴가 설정 패널을 직접 참조하지 않도록 로비 화면에 열기를 맡긴다.</summary>
        void OpenSettings();

        /// <summary>메뉴가 크레딧 패널을 직접 참조하지 않도록 로비 화면에 열기를 맡긴다.</summary>
        void OpenCredits();
    }
}
