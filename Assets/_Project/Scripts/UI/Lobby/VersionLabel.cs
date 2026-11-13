using GuardVsMannequin.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>다른 PC와 접속이 안 될 때 버전이 같은지 확인하도록 현재 게임 버전을 표시한다.</summary>
    public class VersionLabel : MonoBehaviour
    {
        [Tooltip("버전을 쓸 텍스트.")]
        [SerializeField] private Text versionText;

        /// <summary>
        /// 버전은 실행 중에 바뀌지 않으므로 시작할 때 한 번만 채운다.
        /// </summary>
        private void Start()
        {
            if (versionText != null)
                versionText.text = "v" + GameVersion.Current;
        }
    }
}
