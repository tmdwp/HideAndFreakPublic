using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>입력칸에서 Enter를 누르면 그 화면의 실행 버튼을 누른 것과 같게 한다.</summary>
    public static class LobbyInputSubmit
    {
        /// <summary>
        /// 버튼이 막혀 있을 때(Steam 꺼짐, 접속 중) Enter로 우회하지 못하도록 버튼 상태를 그대로 따른다.
        /// </summary>
        /// <param name="inputs">예전 프리팹에 없는 칸은 null일 수 있어 건너뛴다.</param>
        public static void Connect(Button submitButton, params InputField[] inputs)
        {
            foreach (InputField input in inputs)
            {
                if (input == null)
                    continue;
                input.onSubmit.AddListener(_ =>
                {
                    if (submitButton != null && submitButton.IsActive() && submitButton.interactable)
                        submitButton.onClick.Invoke();
                });
            }
        }
    }
}
