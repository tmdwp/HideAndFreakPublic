using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>씬이나 프리팹에 의존하지 않도록 로딩 화면 모양을 코드로 만든다.</summary>
    public sealed class SceneLoadingOverlayView
    {
        private const int SortingOrder = 30000;

        private readonly GameObject root;
        private readonly Text titleText;
        private readonly Text detailText;

        /// <summary>다른 UI 위에 덮이도록 정렬 순서를 크게 두고, 시작할 때 화면이 덮이지 않게 숨긴 채 만든다.</summary>
        public SceneLoadingOverlayView(Transform parent)
        {
            root = new GameObject("Canvas", typeof(RectTransform));
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

            var background = CreateChild<Image>("Background");
            Stretch(background.rectTransform, Vector2.zero, Vector2.one);
            background.color = Color.black;
            background.raycastTarget = true;

            Font font = UiFont.Default;
            titleText = CreateText("Title", font, 56, new Vector2(0f, 0.5f), new Vector2(1f, 0.6f));
            detailText = CreateText("Detail", font, 30, new Vector2(0f, 0.42f), new Vector2(1f, 0.5f));
            detailText.color = new Color(1f, 1f, 1f, 0.7f);

            root.SetActive(false);
        }

        /// <summary>
        /// 언제 보일지는 바깥에서 정하므로 켜고 끄기만 맡는다.
        /// </summary>
        public void SetVisible(bool visible)
        {
            root.SetActive(visible);
        }

        /// <summary>
        /// 문구 내용은 바깥에서 정하므로 받은 그대로 표시만 한다.
        /// </summary>
        public void SetTexts(string title, string detail)
        {
            titleText.text = title;
            detailText.text = detail;
        }

        /// <summary>해상도가 바뀌어도 위치가 유지되도록 화면 비율 영역에 문구를 둔다.</summary>
        private Text CreateText(string objectName, Font font, int fontSize, Vector2 anchorMin, Vector2 anchorMax)
        {
            var text = CreateChild<Text>(objectName);
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
        private T CreateChild<T>(string objectName) where T : Graphic
        {
            var child = new GameObject(objectName, typeof(RectTransform));
            child.transform.SetParent(root.transform, false);
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
