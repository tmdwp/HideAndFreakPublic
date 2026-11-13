namespace GuardVsMannequin.UI
{
    /// <summary>설정 화면이 자동으로 찾아 호출하도록 설정 묶음 하나를 인터페이스로 둔다.</summary>
    public interface ISettingsSection
    {
        /// <summary>
        /// 꺼진 패널은 Awake가 늦게 불리므로, 설정 화면이 처음 열릴 때 한 번 불러 UI 이벤트를 연결하게 한다.
        /// </summary>
        void Initialize();

        /// <summary>
        /// 다른 곳에서 바뀐 값이나 언어 변경을 반영하도록, 열 때와 언어가 바뀔 때 저장된 값을 다시 표시하게 한다.
        /// </summary>
        void Refresh();

        /// <summary>묶음마다 되돌릴지 여부가 달라 기본값 복원 처리를 각 묶음에 맡긴다.</summary>
        void ResetToDefaults();
    }
}
