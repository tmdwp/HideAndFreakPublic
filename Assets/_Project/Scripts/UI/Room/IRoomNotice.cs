namespace GuardVsMannequin.UI
{
    /// <summary>룸 화면 하단에 잠시 안내 문구를 띄운다.</summary>
    public interface IRoomNotice
    {
        /// <summary>팝업을 띄울 정도는 아닌 알림을 화면을 가리지 않고 전하기 위해 상태 줄에 잠시 표시한다.</summary>
        void ShowNotice(string message);
    }
}
