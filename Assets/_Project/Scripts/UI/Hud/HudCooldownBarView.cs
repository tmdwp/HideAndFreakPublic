using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 세로 쿨타임 바 하나(글자 없음). 채움 막대의 위쪽 앵커를 진행도만큼 올린다.
    /// <see cref="HudCooldownBars"/>가 숨겨진 견본을 도구 수만큼 복제해 쓴다.
    /// </summary>
    public class HudCooldownBarView : MonoBehaviour
    {
        [Tooltip("채움 막대. 아래쪽 끝 기준으로 진행도만큼 늘어난다.")]
        [SerializeField] private RectTransform fill;

        [SerializeField] private Graphic fillGraphic;

        /// <summary>
        /// 진행도(0~1)와 색을 반영한다.
        /// </summary>
        public void Show(float progress, Color color)
        {
            fill.anchorMin = new Vector2(fill.anchorMin.x, 0f);
            fill.anchorMax = new Vector2(fill.anchorMax.x, Mathf.Clamp01(progress));
            fillGraphic.color = color;
        }
    }
}
