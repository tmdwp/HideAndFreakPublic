using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>로딩 화면 모양. 에디터에서 프리팹으로 고칠 수 있고, 프리팹이 없으면 같은 기본 모양을 코드로 만든다.</summary>
    public sealed class SceneLoadingOverlayView : MonoBehaviour
    {
        private const int SortingOrder = 30000;

        [SerializeField] private Text titleText;
        [SerializeField] private Text detailText;

        /// <summary>다른 UI 위에 덮이도록 정렬 순서를 크게 두고, 시작할 때 화면이 덮이지 않게 숨긴 채 만든다.</summary>
        public static SceneLoadingOverlayView Create(Transform parent)
        {
            var root = new GameObject("Canvas", typeof(RectTransform));
            root.transform.SetParent(parent, false);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 로딩 중 뒤에 있는 버튼이 눌리지 않게 클릭을 막는다.
            root.AddComponent<GraphicRaycaster>();

            var view = root.AddComponent<SceneLoadingOverlayView>();
            var background = CreateChild<Image>(root.transform, "Background");
            Stretch(background.rectTransform, Vector2.zero, Vector2.one);
            background.color = Color.black;
            background.raycastTarget = true;

            Font font = UiFont.Default;
            view.titleText = CreateText(root.transform, "Title", font, 56, new Vector2(0f, 0.5f), new Vector2(1f, 0.6f));
            view.detailText = CreateText(root.transform, "Detail", font, 30, new Vector2(0f, 0.42f), new Vector2(1f, 0.5f));
            view.detailText.color = new Color(1f, 1f, 1f, 0.7f);

            root.SetActive(false);
            return view;
        }

        /// <summary>
        /// 언제 보일지는 바깥에서 정하므로 켜고 끄기만 맡는다.
        /// </summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        /// <summary>
        /// 문구 내용은 바깥에서 정하므로 받은 그대로 표시만 한다.
        /// </summary>
        public void SetTexts(string title, string detail)
        {
            if (titleText != null)
                titleText.text = title;
            if (detailText != null)
                detailText.text = detail;
        }

        /// <summary>해상도가 바뀌어도 위치가 유지되도록 화면 비율 영역에 문구를 둔다.</summary>
        private static Text CreateText(Transform parent, string objectName, Font font, int fontSize, Vector2 anchorMin, Vector2 anchorMax)
        {
            var text = CreateChild<Text>(parent, objectName);
            Stretch(text.rectTransform, anchorMin, anchorMax);
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>
        /// UI 요소는 RectTransform이 있어야 앵커가 동작하므로 처음부터 RectTransform으로 만든다.
        /// </summary>
        private static T CreateChild<T>(Transform parent, string objectName) where T : Graphic
        {
            var child = new GameObject(objectName, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.AddComponent<T>();
        }

        /// <summary>
        /// 여백이 남으면 해상도마다 크기가 달라지므로 앵커 영역을 꽉 채우게 한다.
        /// </summary>
        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
