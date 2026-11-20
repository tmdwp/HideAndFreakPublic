namespace GuardVsMannequin.UI
{
    /// <summary>나가기 전에 물어야 하는 경우와 그 이유 문구를 정한다. 테스트할 수 있도록 값만 받는다.</summary>
    public static class LeaveConfirmRules
    {
        /// <summary>
        /// 되돌릴 수 없거나 다른 사람에게 영향이 있을 때만 묻고, 혼자 있거나 대기 중인 게스트는 바로 나가게 한다.
        /// </summary>
        /// <param name="otherMemberCount">나를 뺀 참가자 수.</param>
        /// <returns>물어야 하면 안내 문구 key, 아니면 null.</returns>
        public static string GetWarningKey(bool isHost, bool isInGame, int otherMemberCount)
        {
            if (isHost && otherMemberCount > 0)
                return "confirm.leave.host";
            if (!isHost && isInGame)
                return "confirm.leave.inGame";
            return null;
        }
    }
}
