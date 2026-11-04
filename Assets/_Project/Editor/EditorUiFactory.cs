using GuardVsMannequin.Localization;
using GuardVsMannequin.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 에디터에서 uGUI 요소를 코드로 만드는 도우미. 만든 UI는 씬에 저장되어 인스펙터에서 그대로 편집할 수 있다.
    /// </summary>
    public static class EditorUiFactory
    {
        public static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.11f, 0.92f);
        public static readonly Color ButtonColor = new Color(0.22f, 0.25f, 0.30f, 1f);
        public static readonly Color AccentColor = new Color(0.85f, 0.55f, 0.20f, 1f);
        public static readonly Color TextColor = new Color(0.93f, 0.93f, 0.93f, 1f);
        public static readonly Color NoticeColor = new Color(1f, 0.85f, 0.4f, 1f);

        private static Font defaultFont;

        /// <summary>기본 UI 폰트. 프로젝트 폰트(Resources/Fonts)가 없으면 Unity 기본 폰트. (실행 시 OsFontApplier가 기본 폰트를 UI 폰트로 교체)</summary>
        private static Font DefaultFont => defaultFont != null ? defaultFont : defaultFont = UiFont.LoadProjectFont() ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>
        /// 1920x1080 기준으로 크기가 맞춰지는 화면 캔버스를 만든다.
        /// </summary>
        public static Canvas CreateCanvas(string name)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform));
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>
        /// Input System용 입력 모듈을 가진 EventSystem을 만든다.
        /// </summary>
        public static void CreateEventSystem()
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        /// <summary>
        /// 앵커 범위를 가진 배경 패널을 만든다.
        /// </summary>
        public static RectTransform CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        /// <summary>
        /// 자식을 세로로 쌓는 레이아웃을 붙인다.
        /// </summary>
        public static VerticalLayoutGroup AddVerticalLayout(RectTransform rect, float spacing, int padding)
        {
            VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        /// <summary>
        /// 자식을 가로로 나열하는 줄을 만든다.
        /// </summary>
        public static RectTransform CreateRow(Transform parent, string name, float height, float spacing)
        {
            RectTransform rect = CreateRect(parent, name);
            HorizontalLayoutGroup layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            SetHeight(rect, height);
            return rect;
        }

        /// <summary>
        /// 텍스트를 만든다.
        /// content가 strings.csv의 key이면 LocalizedText를 붙여 실행 중 현재 언어로 표시한다.
        /// (코드가 상황에 따라 바꾸는 텍스트에는 key를 넘기지 않는다)
        /// </summary>
        public static Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor alignment, Color color)
        {
            RectTransform rect = CreateRect(parent, name);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.text = content;
            if (Loc.HasKey(content))
            {
                text.text = Loc.Get(content);
                LocalizedText localized = rect.gameObject.AddComponent<LocalizedText>();
                var serialized = new SerializedObject(localized);
                serialized.FindProperty("key").stringValue = content;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>
        /// 라벨이 있는 버튼을 만든다.
        /// </summary>
        public static Button CreateButton(Transform parent, string name, string label, Color color, out Text labelText)
        {
            RectTransform rect = CreateRect(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;

            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            button.colors = colors;

            labelText = CreateText(rect, "Label", label, 24, TextAnchor.MiddleCenter, TextColor);
            Stretch(labelText.rectTransform);
            return button;
        }

        /// <summary>
        /// 라벨 참조가 필요 없는 버튼을 만든다.
        /// </summary>
        public static Button CreateButton(Transform parent, string name, string label, Color color)
        {
            return CreateButton(parent, name, label, color, out _);
        }

        /// <summary>
        /// 안내 문구가 있는 한 줄 입력칸을 만든다.
        /// </summary>
        public static InputField CreateInputField(Transform parent, string name, string placeholder)
        {
            RectTransform rect = CreateRect(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.14f, 0.15f, 0.18f, 1f);

            Text placeholderText = CreateText(rect, "Placeholder", placeholder, 22, TextAnchor.MiddleLeft, new Color(0.6f, 0.6f, 0.6f, 1f));
            placeholderText.fontStyle = FontStyle.Italic;
            StretchWithPadding(placeholderText.rectTransform, 14f);

            Text inputText = CreateText(rect, "Text", string.Empty, 22, TextAnchor.MiddleLeft, TextColor);
            inputText.supportRichText = false;
            StretchWithPadding(inputText.rectTransform, 14f);

            InputField input = rect.gameObject.AddComponent<InputField>();
            input.textComponent = inputText;
            input.placeholder = placeholderText;
            input.targetGraphic = image;
            return input;
        }

        /// <summary>
        /// 가로 슬라이더(배경, 채움, 손잡이)를 만든다.
        /// </summary>
        public static Slider CreateSlider(Transform parent, string name)
        {
            RectTransform root = CreateRect(parent, name);

            RectTransform background = CreateRect(root, "Background");
            background.anchorMin = new Vector2(0f, 0.35f);
            background.anchorMax = new Vector2(1f, 0.65f);
            background.offsetMin = background.offsetMax = Vector2.zero;
            background.gameObject.AddComponent<Image>().color = new Color(0.14f, 0.15f, 0.18f, 1f);

            RectTransform fillArea = CreateRect(root, "Fill Area");
            fillArea.anchorMin = new Vector2(0f, 0.35f);
            fillArea.anchorMax = new Vector2(1f, 0.65f);
            fillArea.offsetMin = new Vector2(5f, 0f);
            fillArea.offsetMax = new Vector2(-5f, 0f);
            RectTransform fill = CreateRect(fillArea, "Fill");
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            fill.gameObject.AddComponent<Image>().color = AccentColor;

            RectTransform handleArea = CreateRect(root, "Handle Slide Area");
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);
            RectTransform handle = CreateRect(handleArea, "Handle");
            handle.sizeDelta = new Vector2(20f, 0f);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = TextColor;

            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        /// <summary>
        /// 세로로 스크롤되는 목록을 만든다. 항목은 반환된 Content 아래에 추가한다.
        /// </summary>
        public static RectTransform CreateScrollList(Transform parent, string name, float spacing)
        {
            RectTransform root = CreateRect(parent, name);
            root.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            RectTransform viewport = CreateRect(root, "Viewport");
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            VerticalLayoutGroup layout = AddVerticalLayout(content, spacing, 10);
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        /// <summary>
        /// 누르면 스크롤 목록이 펼쳐지는 선택 상자를 만든다. (항목이 많으면 목록 안에서 스크롤)
        /// </summary>
        /// <param name="parent">부모.</param>
        /// <param name="name">오브젝트 이름.</param>
        /// <param name="fontSize">글자 크기.</param>
        /// <param name="listHeight">펼쳐진 목록의 높이. 넘치는 항목은 스크롤한다.</param>
        public static Dropdown CreateDropdown(Transform parent, string name, int fontSize, float listHeight)
        {
            const float itemHeight = 40f;
            const float scrollbarWidth = 18f;
            Color fieldColor = new Color(0.14f, 0.15f, 0.18f, 1f);

            RectTransform root = CreateRect(parent, name);
            Image background = root.gameObject.AddComponent<Image>();
            background.color = fieldColor;

            Text caption = CreateText(root, "Label", "-", fontSize, TextAnchor.MiddleCenter, TextColor);
            Stretch(caption.rectTransform);
            caption.rectTransform.offsetMax = new Vector2(-36f, 0f);
            Text arrow = CreateText(root, "Arrow", "▼", fontSize - 6, TextAnchor.MiddleRight, TextColor);
            StretchWithPadding(arrow.rectTransform, 12f);

            // 펼쳐지는 목록(템플릿): 선택 상자 바로 아래에 붙는다.
            RectTransform template = CreateRect(root, "Template");
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = new Vector2(0f, -2f);
            template.sizeDelta = new Vector2(0f, listHeight);
            template.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.11f, 0.13f, 1f);
            ScrollRect scroll = template.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            RectTransform viewport = CreateRect(template, "Viewport");
            Stretch(viewport);
            viewport.offsetMax = new Vector2(-scrollbarWidth, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, itemHeight);

            RectTransform item = CreateRect(content, "Item");
            item.anchorMin = new Vector2(0f, 0.5f);
            item.anchorMax = new Vector2(1f, 0.5f);
            item.sizeDelta = new Vector2(0f, itemHeight);
            Toggle toggle = item.gameObject.AddComponent<Toggle>();

            RectTransform itemBackground = CreateRect(item, "Item Background");
            Stretch(itemBackground);
            Image itemBackgroundImage = itemBackground.gameObject.AddComponent<Image>();
            itemBackgroundImage.color = new Color(0.18f, 0.2f, 0.24f, 1f);

            RectTransform checkmark = CreateRect(item, "Item Checkmark");
            checkmark.anchorMin = new Vector2(0f, 0f);
            checkmark.anchorMax = new Vector2(0f, 1f);
            checkmark.pivot = new Vector2(0f, 0.5f);
            checkmark.sizeDelta = new Vector2(6f, 0f);
            Image checkmarkImage = checkmark.gameObject.AddComponent<Image>();
            checkmarkImage.color = AccentColor;

            Text itemLabel = CreateText(item, "Item Label", "-", fontSize - 2, TextAnchor.MiddleCenter, TextColor);
            Stretch(itemLabel.rectTransform);

            toggle.targetGraphic = itemBackgroundImage;
            toggle.graphic = checkmarkImage;
            toggle.isOn = true;

            // 세로 스크롤바
            RectTransform scrollbarRect = CreateRect(template, "Scrollbar");
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.pivot = new Vector2(1f, 1f);
            scrollbarRect.sizeDelta = new Vector2(scrollbarWidth, 0f);
            scrollbarRect.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 1f);
            Scrollbar scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
            RectTransform slidingArea = CreateRect(scrollbarRect, "Sliding Area");
            Stretch(slidingArea);
            RectTransform handle = CreateRect(slidingArea, "Handle");
            handle.offsetMin = handle.offsetMax = Vector2.zero;
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = ButtonColor;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            template.gameObject.SetActive(false);

            Dropdown dropdown = root.gameObject.AddComponent<Dropdown>();
            dropdown.targetGraphic = background;
            dropdown.template = template;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            return dropdown;
        }

        /// <summary>
        /// 내용이 길면 스크롤되는 텍스트 영역을 만든다. 반환한 Text에 내용을 넣는다.
        /// 영역의 레이아웃 높이는 호출한 쪽에서 정한다(스크롤 루트 = 반환값.transform.parent.parent.parent).
        /// </summary>
        public static Text CreateScrollText(Transform parent, string name, string content, int fontSize, TextAnchor alignment, Color color)
        {
            RectTransform list = CreateScrollList(parent, name, 0f);
            Text text = CreateText(list, "Text", content, fontSize, alignment, color);
            return text;
        }

        /// <summary>
        /// 스크롤 목록/텍스트의 루트(ScrollRect)가 부모 레이아웃에서 남는 높이를 차지하게 한다.
        /// </summary>
        /// <param name="scrollContent">CreateScrollList의 반환값(Content) 또는 CreateScrollText가 만든 Text의 부모.</param>
        /// <param name="minHeight">최소 높이.</param>
        public static void FillRemainingHeight(Transform scrollContent, float minHeight)
        {
            Transform root = scrollContent.parent.parent;
            LayoutElement element = GetLayoutElement(root);
            element.flexibleHeight = 1f;
            element.minHeight = minHeight;
        }

        /// <summary>
        /// Layout Group 없이 고정 위치·크기로 배치한다. 에디터에서 끌어서 옮기거나 크기를 바꾸기 쉽다.
        /// </summary>
        /// <param name="target">배치할 UI.</param>
        /// <param name="anchor">부모 안의 기준점 (0~1, 예: 가운데 (0.5, 0.5), 위 가운데 (0.5, 1))</param>
        /// <param name="position">기준점에서의 위치(픽셀, 1920×1080 기준).</param>
        /// <param name="size">크기(픽셀).</param>
        public static void Place(Component target, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)target.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>
        /// 체크박스(왼쪽 네모 + 문구) 토글을 만든다. 위치는 호출한 쪽이 <see cref="Place"/>로 정한다.
        /// </summary>
        /// <param name="labelKey">문구 key.</param>
        /// <param name="isOn">처음 켜져 있는지 여부.</param>
        public static Toggle CreateToggle(Transform parent, string name, string labelKey, bool isOn)
        {
            const float BoxSize = 30f;
            RectTransform root = CreateRect(parent, name);

            RectTransform box = CreateRect(root, "Box");
            box.anchorMin = new Vector2(0f, 0.5f);
            box.anchorMax = new Vector2(0f, 0.5f);
            box.pivot = new Vector2(0f, 0.5f);
            box.anchoredPosition = Vector2.zero;
            box.sizeDelta = new Vector2(BoxSize, BoxSize);
            Image boxImage = box.gameObject.AddComponent<Image>();
            boxImage.color = ButtonColor;

            RectTransform check = CreateRect(box, "Check");
            check.anchorMin = Vector2.zero;
            check.anchorMax = Vector2.one;
            check.offsetMin = new Vector2(6f, 6f);
            check.offsetMax = new Vector2(-6f, -6f);
            Image checkImage = check.gameObject.AddComponent<Image>();
            checkImage.color = AccentColor;

            Text label = CreateText(root, "Label", labelKey, 22, TextAnchor.MiddleLeft, TextColor);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(BoxSize + 12f, 0f);
            labelRect.offsetMax = Vector2.zero;

            Toggle toggle = root.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;
            toggle.isOn = isOn;
            return toggle;
        }

        /// <summary>
        /// 레이아웃 안에서의 높이를 고정한다.
        /// </summary>
        public static void SetHeight(Component target, float height)
        {
            LayoutElement element = GetLayoutElement(target);
            element.minHeight = height;
            element.preferredHeight = height;
        }

        /// <summary>
        /// 레이아웃 안에서의 너비를 고정한다.
        /// </summary>
        public static void SetWidth(Component target, float width)
        {
            LayoutElement element = GetLayoutElement(target);
            element.minWidth = width;
            element.preferredWidth = width;
        }

        /// <summary>
        /// 레이아웃 안에서 남는 너비를 차지하게 한다.
        /// </summary>
        public static void SetFlexibleWidth(Component target)
        {
            GetLayoutElement(target).flexibleWidth = 1f;
        }

        /// <summary>
        /// 부모 크기에 꽉 차게 한다.
        /// </summary>
        public static void Stretch(RectTransform rect)
        {
            StretchWithPadding(rect, 0f);
        }

        /// <summary>
        /// 부모 크기에서 좌우 여백을 두고 꽉 차게 한다.
        /// </summary>
        public static void StretchWithPadding(RectTransform rect, float horizontalPadding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontalPadding, 0f);
            rect.offsetMax = new Vector2(-horizontalPadding, 0f);
        }

        /// <summary>
        /// RectTransform을 가진 빈 오브젝트를 만든다.
        /// </summary>
        public static RectTransform CreateRect(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return (RectTransform)gameObject.transform;
        }

        /// <summary>
        /// LayoutElement를 가져오거나 추가한다.
        /// </summary>
        private static LayoutElement GetLayoutElement(Component target)
        {
            LayoutElement element = target.GetComponent<LayoutElement>();
            return element != null ? element : target.gameObject.AddComponent<LayoutElement>();
        }
    }
}
