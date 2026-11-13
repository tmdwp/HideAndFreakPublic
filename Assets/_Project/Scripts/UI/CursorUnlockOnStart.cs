using UnityEngine;

namespace GuardVsMannequin.UI
{
    /// <summary>게임에서 돌아오면 커서가 잠겨 있을 수 있어 메뉴 화면에 들어오면 커서를 푼다.</summary>
    public class CursorUnlockOnStart : MonoBehaviour
    {
        /// <summary>
        /// 게임 씬에서 잠근 커서가 그대로 넘어오면 메뉴 버튼을 누를 수 없으므로 들어오자마자 푼다.
        /// </summary>
        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
