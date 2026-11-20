using System;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>확인 창 모양. 에디터에서 프리팹으로 고칠 수 있고, 프리팹이 없으면 같은 기본 모양을 코드로 만든다.</summary>
    public sealed class ConfirmDialogView : MonoBehaviour
    {
        // 일반 UI·FPS 표시보다 위, 로딩 화면(30000)보다 아래에 그려지도록 한다.
        private const int SortingOrder = 29500;

        private static readonly Color DimColor = new(0f, 0f, 0f, 0.65f);
        private static readonly Color BoxColor = new(0.08f, 0.09f, 0.11f, 0.98f);
        private static readonly Color AccentColor = new(0.85f, 0.55f, 0.20f, 1f);
        private static readonly Color DangerColor = new(0.75f, 0.25f, 0.22f, 1f);
        private static readonly Color ButtonColor = new(0.22f, 0.25f, 0.30f, 1f);

        [SerializeField] private Text titleText;
        [SerializeField] private Text messageText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Text confirmLabel;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Text cancelLabel;

        /// <summary>버튼 연결을 바깥에 맡겨 모양과 동작을 나눈다.</summary>
        public void Bind(Action onConfirm, Action onCancel)
        {
            confirmButton.onClick.AddListener(() => onConfirm());
            cancelButton.onClick.AddListener(() => onCancel());
        }

        /// <summary>
        /// 언제 보일지는 바깥에서 정하므로 켜고 끄기만 맡고, 실수로 확인을 누르지 않도록 열릴 때 취소 버튼에 초점을 둔다.
        /// </summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
            if (visible)
                cancelButton.Select();
        }

        /// <summary>문구 내용은 바깥에서 정하므로 받은 그대로 표시만 한다.</summary>
        public void SetTexts(string title, string message, string confirm, string cancel)
        {
            titleText.text = title;
            messageText.text = message;
            confirmLabel.text = confirm;
            cancelLabel.text = cancel;
        }

        /// <summary>다른 UI 위에 덮이도록 정렬 순서를 크게 두고, 시작할 때 화면이 덮이지 않게 숨긴 채 만든다.</summary>
        public static ConfirmDialogView Create(Transform parent)
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
            root.AddComponent<GraphicRaycaster>();

            var view = root.AddComponent<ConfirmDialogView>();

            // 창이 떠 있는 동안 뒤의 버튼이 눌리지 않게 화면 전체를 덮어 클릭을 막는다.
            Image dim = CreateChild<Image>(root.transform, "Dim");
            Stretch(dim.rectTransform, Vector2.zero, Vector2.one);
            dim.color = DimColor;

            Image box = CreateChild<Image>(root.transform, "Box");
            Stretch(box.rectTransform, new Vector2(0.33f, 0.33f), new Vector2(0.67f, 0.67f));
            box.color = BoxColor;

            Font font = UiFont.Default;
            view.titleText = CreateText(box.transform, "Title", font, 34, new Vector2(0.06f, 0.74f), new Vector2(0.94f, 0.94f));
            view.titleText.color = AccentColor;
            view.titleText.fontStyle = FontStyle.Bold;
            view.messageText = CreateText(box.transform, "Message", font, 24, new Vector2(0.06f, 0.3f), new Vector2(0.94f, 0.74f));

            view.cancelButton = CreateButton(box.transform, "CancelButton", font, ButtonColor, new Vector2(0.06f, 0.07f), new Vector2(0.48f, 0.24f), out view.cancelLabel);
            view.confirmButton = CreateButton(box.transform, "ConfirmButton", font, DangerColor, new Vector2(0.52f, 0.07f), new Vector2(0.94f, 0.24f), out view.confirmLabel);

            root.SetActive(false);
            return view;
        }

        /// <summary>두 버튼이 같은 모양이 되도록 배경·글자 생성을 한곳에 둔다.</summary>
        private static Button CreateButton(Transform parent, string objectName, Font font, Color color, Vector2 anchorMin, Vector2 anchorMax, out Text label)
        {
            Image background = CreateChild<Image>(parent, objectName);
            Stretch(background.rectTransform, anchorMin, anchorMax);
            background.color = color;
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            label = CreateText(background.transform, "Label", font, 26, Vector2.zero, Vector2.one);
            return button;
        }

        /// <summary>해상도가 바뀌어도 위치가 유지되도록 비율 영역에 문구를 둔다.</summary>
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
