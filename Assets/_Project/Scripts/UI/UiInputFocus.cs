using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>글자를 치는 중에 누른 ESC·Enter가 화면 이동으로 처리되지 않도록 입력칸에 초점이 있는지 알려 준다.</summary>
    public static class UiInputFocus
    {
        /// <summary>
        /// 선택된 오브젝트가 글자를 받고 있는 입력칸일 때만 true.
        /// </summary>
        public static bool IsTyping()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
                return false;
            return eventSystem.currentSelectedGameObject.TryGetComponent(out InputField field) && field.isFocused;
        }
    }
}
