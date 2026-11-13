using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>도구 수만큼 복제해 쓰는 세로 쿨타임 바 하나.</summary>
    public class HudCooldownBarView : MonoBehaviour
    {
        [Tooltip("채움 막대. 아래쪽 끝 기준으로 진행도만큼 늘어난다.")]
        [SerializeField] private RectTransform fill;

        [SerializeField] private Graphic fillGraphic;

        /// <summary>스프라이트 없이 위쪽 앵커로 진행도를 보이고, 색 판단은 바깥에 맡긴다.</summary>
        public void Show(float progress, Color color)
        {
            fill.anchorMin = new Vector2(fill.anchorMin.x, 0f);
            fill.anchorMax = new Vector2(fill.anchorMax.x, Mathf.Clamp01(progress));
            fillGraphic.color = color;
        }
    }
}
