using GuardVsMannequin.Network;

namespace GuardVsMannequin.UI
{
    /// <summary>방 만들기·참가 메뉴가 같은 조건을 쓰도록 로비 버튼을 누를 수 있는 조건을 모은다.</summary>
    public static class SessionButtonRules
    {
        /// <summary>Steam이 꺼져 있으면 들어가도 할 수 있는 일이 없으므로 Steam 화면 버튼을 막는다.</summary>
        public static bool CanOpenSteamMenu(this ISessionService session)
        {
            return session.IsSteamAvailable;
        }

        /// <summary>접속이 겹치면 세션 상태가 꼬이므로, 진행 중인 작업이 없고 Steam을 쓸 수 있을 때만 허용한다.</summary>
        public static bool CanConnectSteam(this ISessionService session)
        {
            return !session.IsBusy && session.IsSteamAvailable;
        }

        /// <summary>직접 접속은 Steam이 없어도 되지만 접속이 겹치면 안 되므로 진행 중인 작업만 확인한다.</summary>
        public static bool CanConnectDirect(this ISessionService session)
        {
            return !session.IsBusy;
        }
    }
}
