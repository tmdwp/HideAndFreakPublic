using UnityEngine;

namespace GuardVsMannequin.Core
{
    /// <summary>메뉴에서는 UI를 누르고 게임 중에는 입력을 받도록 커서 잠금을 한곳에서 관리한다.</summary>
    public static class GameCursor
    {
        /// <summary>게임 입력을 받아도 되는지를 커서 잠금으로 판단하기 때문에 노출한다.</summary>
        public static bool IsLocked => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>
        /// 잠금과 표시 여부가 어긋나면 잠긴 채 커서가 보이는 문제가 생기므로 항상 함께 바꾼다.
        /// </summary>
        public static void SetLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
