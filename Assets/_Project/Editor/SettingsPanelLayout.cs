using GuardVsMannequin.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 설정 항목이 늘어 페이지 밖으로 넘치지 않도록 설정 화면을 키우고, 페이지를 끌거나 휠·스크롤바로 내려 볼 수 있게 한다.
    /// 새로 만들 때와 기존 프리팹 보강이 같은 모양이 되도록 한곳에 둔다.
    /// </summary>
    public static class SettingsPanelLayout
    {
        // 화면 가운데 큰 창. 로비·룸·게임 어디서든 같은 크기로 보이도록 화면 비율로 정한다.
        public static readonly Vector2 PanelAnchorMin = new(0.1f, 0.04f);
        public static readonly Vector2 PanelAnchorMax = new(0.9f, 0.96f);

        private const float ScrollbarWidth = 14f;
        private static readonly Color ScrollbarTrackColor = new(0f, 0f, 0f, 0.3f);
        private static readonly Color ScrollbarHandleColor = new(0.55f, 0.58f, 0.64f, 0.9f);

        /// <summary>캔버스·프리팹마다 따로 덮어쓴 크기가 있어, 찾은 설정 화면마다 같은 크기로 맞춘다.</summary>
        /// <param name="root">설정 화면을 품은 캔버스 또는 설정 화면 프리팹의 루트.</param>
        /// <returns>크기를 바꾼 설정 화면 수(없으면 -1).</returns>
        public static int Enlarge(GameObject root)
        {
            SettingsPanelView[] views = root.GetComponentsInChildren<SettingsPanelView>(true);
            if (views.Length == 0)
                return -1;

            int changed = 0;
            foreach (SettingsPanelView view in views)
            {
                // 예전에 탭 상태가 저장된 캔버스는 스크롤 틀 안 페이지가 꺼진 채 남을 수 있어, 켜고 끄기는 틀에 맡기고 내용은 항상 켠다.
                foreach (ScrollRect scroll in view.GetComponentsInChildren<ScrollRect>(true))
                {
                    if (scroll.content != null && scroll.content.name.EndsWith("Page") && !scroll.content.gameObject.activeSelf)
                    {
                        scroll.content.gameObject.SetActive(true);
                        changed++;
                    }
                }

                var rect = (RectTransform)view.transform;
                if (rect.anchorMin == PanelAnchorMin && rect.anchorMax == PanelAnchorMax && rect.offsetMin == Vector2.zero && rect.offsetMax == Vector2.zero)
                    continue;
                rect.anchorMin = PanelAnchorMin;
                rect.anchorMax = PanelAnchorMax;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                changed++;
            }
            return changed;
        }

        /// <summary>
        /// 검은 배경이 설정 창 자체의 이미지라 크기를 바꾸면 안의 내용까지 함께 바뀌므로, 배경을 레이아웃에서 빠진 자식으로 떼어
        /// 배경만 따로 키우고 줄일 수 있게 한다. 창 자체 이미지는 투명하게 남겨 뒤의 버튼이 눌리지 않게 막는다.
        /// </summary>
        /// <returns>배경을 떼어 냈으면 1, 이미 있으면 0, 설정 화면이 없으면 -1.</returns>
        public static int SeparateBackground(GameObject settingsPanel)
        {
            SettingsPanelView view = settingsPanel.GetComponentInChildren<SettingsPanelView>(true);
            if (view == null)
                return -1;
            if (view.transform.Find(BackgroundName) != null || !view.TryGetComponent(out Image panelImage))
                return 0;

            RectTransform background = EditorUiFactory.CreateRect(view.transform, BackgroundName);
            background.SetAsFirstSibling();
            EditorUiFactory.Stretch(background);
            background.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            Image backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.sprite = panelImage.sprite;
            backgroundImage.type = panelImage.type;
            backgroundImage.color = panelImage.color;

            panelImage.color = new Color(panelImage.color.r, panelImage.color.g, panelImage.color.b, 0f);
            return 1;
        }

        // 설정 창 배경 오브젝트 이름. 이 이름으로 이미 떼어 냈는지 확인한다.
        private const string BackgroundName = "Background";

        /// <summary>
        /// 페이지마다 스크롤 틀을 씌운다. 키 목록처럼 이미 스크롤 목록이 있는 페이지는 겹쳐 스크롤되면 조작이 헷갈려 건너뛴다.
        /// 탭이 틀째로 켜지고 꺼지도록 화면의 페이지 목록도 틀로 바꾼다.
        /// </summary>
        /// <returns>스크롤 틀을 씌운 페이지 수(설정 화면이 없으면 -1).</returns>
        public static int MakePagesScrollable(GameObject settingsPanel)
        {
            SettingsPanelView view = settingsPanel.GetComponentInChildren<SettingsPanelView>(true);
            if (view == null)
                return -1;

            var serialized = new SerializedObject(view);
            SerializedProperty pages = serialized.FindProperty("pages");
            int wrapped = 0;
            for (int i = 0; i < pages.arraySize; i++)
            {
                if (!(pages.GetArrayElementAtIndex(i).objectReferenceValue is GameObject page))
                    continue;
                if (page.GetComponentInChildren<ScrollRect>(true) != null)
                    continue;

                pages.GetArrayElementAtIndex(i).objectReferenceValue = WrapInScroll((RectTransform)page.transform);
                wrapped++;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return wrapped;
        }

        /// <summary>
        /// 페이지를 스크롤 내용으로 옮긴다. 설정 묶음 컴포넌트는 페이지에 그대로 남아 연결이 끊기지 않는다.
        /// </summary>
        /// <returns>탭이 켜고 끌 스크롤 틀.</returns>
        private static GameObject WrapInScroll(RectTransform page)
        {
            bool wasActive = page.gameObject.activeSelf;
            RectTransform frame = EditorUiFactory.CreateRect(page.parent, page.name + "Scroll");
            frame.SetSiblingIndex(page.GetSiblingIndex());
            CopyRect(page, frame);

            ScrollRect scroll = frame.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            RectTransform viewport = EditorUiFactory.CreateRect(frame, "Viewport");
            EditorUiFactory.Stretch(viewport);
            viewport.offsetMax = new Vector2(-(ScrollbarWidth + 6f), 0f);
            // 빈 곳을 잡고 끌어도 스크롤되도록 보이지 않는 바탕으로 클릭을 받는다.
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();

            page.SetParent(viewport, false);
            page.anchorMin = new Vector2(0f, 1f);
            page.anchorMax = new Vector2(1f, 1f);
            page.pivot = new Vector2(0.5f, 1f);
            page.offsetMin = page.offsetMax = Vector2.zero;
            if (page.TryGetComponent(out VerticalLayoutGroup layout))
                layout.childForceExpandHeight = false;
            if (!page.TryGetComponent(out ContentSizeFitter fitter))
                fitter = page.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            page.gameObject.SetActive(true);

            scroll.viewport = viewport;
            scroll.content = page;
            scroll.verticalScrollbar = CreateScrollbar(frame);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scroll.verticalScrollbarSpacing = 6f;

            frame.gameObject.SetActive(wasActive);
            return frame.gameObject;
        }

        /// <summary>마우스로 끌어 내릴 수 있는 세로 스크롤바를 오른쪽에 만든다.</summary>
        private static Scrollbar CreateScrollbar(Transform frame)
        {
            RectTransform track = EditorUiFactory.CreateRect(frame, "Scrollbar");
            track.anchorMin = new Vector2(1f, 0f);
            track.anchorMax = Vector2.one;
            track.pivot = new Vector2(1f, 0.5f);
            track.sizeDelta = new Vector2(ScrollbarWidth, 0f);
            track.anchoredPosition = Vector2.zero;
            track.gameObject.AddComponent<Image>().color = ScrollbarTrackColor;

            RectTransform slidingArea = EditorUiFactory.CreateRect(track, "SlidingArea");
            EditorUiFactory.Stretch(slidingArea);
            RectTransform handle = EditorUiFactory.CreateRect(slidingArea, "Handle");
            EditorUiFactory.Stretch(handle);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = ScrollbarHandleColor;

            Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            return scrollbar;
        }

        /// <summary>
        /// 틀이 원래 페이지 자리를 그대로 차지하도록 크기·위치를 옮긴다.
        /// </summary>
        private static void CopyRect(RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.offsetMin = source.offsetMin;
            target.offsetMax = source.offsetMax;
        }
    }
}
