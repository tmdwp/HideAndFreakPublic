using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace GuardVsMannequin.Editor.CodeMap
{
    /// <summary>
    /// 오브젝트에 붙은 스크립트가 어떤 멤버를 갖고, 각 메소드가 어떤 변수·메소드와 이어지는지 코드를 열지 않고 따라가려고 만든 창.
    /// 분석·배치·그리기는 각 클래스에 맡기고, 이 창은 선택 대상과 탐색 기록만 관리한다.
    /// </summary>
    internal sealed class CodeMapWindow : EditorWindow
    {
        private const string OwnCodeFolder = "Assets/_Project/";
        private const float MinListWidth = 200f;
        private const float MinGraphWidth = 260f;
        private const float SplitterWidth = 4f;
        private const int MaxHistory = 64;

        private enum OutgoingFilter
        {
            OwnCode,
            ProjectCode,
            Everything,
        }

        private enum IncomingScope
        {
            SelectedTarget,
            OwnCode,
        }

        private static readonly string[] outgoingFilterLabels = { "내 코드", "프로젝트 코드", "전부" };
        private static readonly string[] incomingScopeLabels = { "선택 대상 안", "내 코드 전체" };

        [SerializeField] private float listWidth = 320f;
        [SerializeField] private bool isLocked;
        [SerializeField] private bool showEngineComponents;
        [SerializeField] private OutgoingFilter outgoingFilter = OutgoingFilter.OwnCode;
        [SerializeField] private IncomingScope incomingScope = IncomingScope.SelectedTarget;
        [SerializeField] private Object target;

        private CodeOriginClassifier classifier;
        private MethodReferenceAnalyzer analyzer;
        private ScriptSourceLocator sourceLocator;
        private CodeMapMemberListView listView;
        private CodeMapGraphView graphView;
        private SearchField searchField;
        private string searchText = string.Empty;

        private readonly List<ComponentEntry> entries = new List<ComponentEntry>();
        private readonly List<CodeMember> history = new List<CodeMember>();
        private List<CodeMember> ownCodeMembers;
        private int hiddenComponentCount;
        private CodeMember focus;
        private CodeMapGraph graph;
        private CodeMapInteraction? pendingInteraction;
        private bool isResizing;

        [MenuItem(EditorMenus.Root + "개발 도구/코드 구조 보기")]
        private static void Open()
        {
            GetWindow<CodeMapWindow>().Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("코드 구조", EditorGUIUtility.IconContent("cs Script Icon").image);
            wantsMouseMove = true;

            // 스크립트를 다시 컴파일하면 어셈블리가 바뀌므로 분석 결과는 창이 다시 켜질 때마다 새로 만든다.
            classifier = new CodeOriginClassifier(OwnCodeFolder);
            analyzer = new MethodReferenceAnalyzer();
            sourceLocator = new ScriptSourceLocator();
            listView = new CodeMapMemberListView(QueueInteraction);
            graphView = new CodeMapGraphView(QueueInteraction);
            searchField = new SearchField();

            if (target == null)
                target = PickTarget(Selection.activeObject);
            RebuildEntries();
        }

        private void OnSelectionChange()
        {
            if (isLocked)
                return;

            Object picked = PickTarget(Selection.activeObject);
            // 빈 곳을 눌러 선택이 풀려도 보던 내용을 유지한다.
            if (picked == null || picked == target)
                return;

            target = picked;
            RebuildEntries();
            ClearFocus();
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();

            float top = EditorStyles.toolbar.fixedHeight;
            float height = position.height - top;
            listWidth = Mathf.Clamp(listWidth, MinListWidth, Mathf.Max(MinListWidth, position.width - MinGraphWidth));

            var listRect = new Rect(0f, top, listWidth, height);
            var splitterRect = new Rect(listWidth, top, SplitterWidth, height);
            var graphRect = new Rect(listWidth + SplitterWidth, top, position.width - listWidth - SplitterWidth, height);

            listView.Draw(listRect, entries, searchText, focus, HiddenComponentNote());
            HandleSplitter(splitterRect);
            DrawGraphArea(graphRect);

            if (pendingInteraction.HasValue)
            {
                CodeMapInteraction interaction = pendingInteraction.Value;
                pendingInteraction = null;
                Apply(interaction);
            }

            if (Event.current.type == EventType.MouseMove)
                Repaint();
        }

        private void DrawToolbar()
        {
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(history.Count == 0))
                {
                    if (GUILayout.Button(new GUIContent("◀ 뒤로", "이전에 보던 멤버로"), EditorStyles.toolbarButton, GUILayout.Width(56f)))
                        GoBack();
                }

                using (new EditorGUI.DisabledScope(focus == null))
                {
                    if (GUILayout.Button("코드 열기", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                        OpenSource(focus);
                }

                bool locked = GUILayout.Toggle(isLocked, new GUIContent("잠금", "켜 두면 다른 오브젝트를 선택해도 지금 대상을 유지합니다."), EditorStyles.toolbarButton, GUILayout.Width(40f));
                if (locked != isLocked)
                {
                    isLocked = locked;
                    if (!isLocked)
                        OnSelectionChange();
                }

                GUILayout.Label(target != null ? target.name : "(선택 없음)", EditorStyles.miniBoldLabel, GUILayout.MinWidth(60f));
                GUILayout.FlexibleSpace();

                bool showEngine = GUILayout.Toggle(showEngineComponents, new GUIContent("Unity 컴포넌트", "Transform·Renderer 같은 엔진 컴포넌트도 목록에 표시"), EditorStyles.toolbarButton);
                if (showEngine != showEngineComponents)
                {
                    showEngineComponents = showEngine;
                    RebuildEntries();
                }

                GUILayout.Label("호출 표시", EditorStyles.miniLabel);
                var filter = (OutgoingFilter)EditorGUILayout.Popup((int)outgoingFilter, outgoingFilterLabels, EditorStyles.toolbarPopup, GUILayout.Width(84f));
                GUILayout.Label("사용처", EditorStyles.miniLabel);
                var scope = (IncomingScope)EditorGUILayout.Popup((int)incomingScope, incomingScopeLabels, EditorStyles.toolbarPopup, GUILayout.Width(84f));
                if (filter != outgoingFilter || scope != incomingScope)
                {
                    outgoingFilter = filter;
                    incomingScope = scope;
                    RebuildGraph();
                }

                searchText = searchField.OnToolbarGUI(searchText, GUILayout.Width(160f));
            }
        }

        private void HandleSplitter(Rect splitterRect)
        {
            EditorGUI.DrawRect(splitterRect, new Color(0f, 0f, 0f, 0.25f));
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

            Event current = Event.current;
            if (current.type == EventType.MouseDown && splitterRect.Contains(current.mousePosition))
            {
                isResizing = true;
                current.Use();
            }
            else if (isResizing && current.type == EventType.MouseDrag)
            {
                listWidth = current.mousePosition.x;
                current.Use();
                Repaint();
            }
            else if (current.rawType == EventType.MouseUp)
            {
                isResizing = false;
            }
        }

        private void DrawGraphArea(Rect graphRect)
        {
            if (graph == null)
            {
                EditorGUI.DrawRect(graphRect, CodeMapStyles.GraphBackground);
                GUI.Label(graphRect,
                    "왼쪽 목록에서 메소드나 변수를 클릭하면\n그 멤버가 쓰는 것(오른쪽)과 그 멤버를 쓰는 곳(왼쪽)을 보여 줍니다.\n\n" +
                    "그래프 노드 클릭: 그 멤버로 이동 · 우클릭: 코드 열기\n목록 더블클릭: 코드 열기",
                    CodeMapStyles.WrappedCenterStyle);
                return;
            }

            graphView.Draw(graphRect, graph);
        }

        /// <summary>그리는 도중 그래프를 바꾸면 남은 그리기가 엉키므로, 요청은 모아 두었다가 OnGUI 끝에서 처리한다.</summary>
        private void QueueInteraction(CodeMapInteraction interaction)
        {
            pendingInteraction = interaction;
            Repaint();
        }

        private void Apply(CodeMapInteraction interaction)
        {
            if (interaction.Action == CodeMapAction.OpenSource)
                OpenSource(interaction.Member);
            else if (!interaction.Member.Equals(focus))
                SetFocus(interaction.Member, true);
        }

        private void SetFocus(CodeMember member, bool recordHistory)
        {
            if (recordHistory && focus != null)
            {
                history.Add(focus);
                if (history.Count > MaxHistory)
                    history.RemoveAt(0);
            }

            focus = member;
            graphView.ResetScroll();
            RebuildGraph();
        }

        private void GoBack()
        {
            if (history.Count == 0)
                return;

            CodeMember previous = history[history.Count - 1];
            history.RemoveAt(history.Count - 1);
            SetFocus(previous, false);
        }

        private void ClearFocus()
        {
            focus = null;
            graph = null;
            history.Clear();
        }

        private void OpenSource(CodeMember member)
        {
            if (member != null && !sourceLocator.TryOpen(member))
                ShowNotification(new GUIContent("코드 위치를 찾지 못했습니다 (Assets 밖의 코드일 수 있습니다)"));
        }

        private void RebuildGraph()
        {
            if (focus == null)
            {
                graph = null;
                return;
            }

            List<CodeReference> outgoing = analyzer.GetOutgoing(focus).Where(reference => IsVisible(reference.Target)).ToList();
            List<CodeReference> incoming = analyzer.FindIncoming(focus, IncomingCandidates());
            graph = CodeMapGraphLayout.Build(focus, incoming, outgoing, classifier.Classify, IncomingEmptyNote());
        }

        private bool IsVisible(CodeMember member)
        {
            CodeOrigin origin = classifier.Classify(member);
            switch (outgoingFilter)
            {
                case OutgoingFilter.OwnCode:
                    return origin == CodeOrigin.Own;
                case OutgoingFilter.ProjectCode:
                    return origin != CodeOrigin.External;
                default:
                    return true;
            }
        }

        private IEnumerable<CodeMember> IncomingCandidates()
        {
            if (incomingScope == IncomingScope.OwnCode)
                return OwnCodeMembers();

            IEnumerable<System.Type> types = entries.SelectMany(entry => entry.Groups.Select(group => group.Type));
            if (focus.DeclaringType != null)
                types = types.Append(focus.DeclaringType);
            return types.Distinct().SelectMany(TypeMemberCatalog.GetBodyMembers);
        }

        private List<CodeMember> OwnCodeMembers()
        {
            if (ownCodeMembers == null)
            {
                ownCodeMembers = classifier.LoadedOwnAssemblies()
                    .SelectMany(TypeMemberCatalog.GetAuthoredTypes)
                    .SelectMany(TypeMemberCatalog.GetBodyMembers)
                    .ToList();
            }

            return ownCodeMembers;
        }

        private string IncomingEmptyNote()
        {
            if (focus.IsEngineCallback)
                return "코드에서는 호출하지 않습니다. Unity·FishNet이 직접 부르는 함수입니다.";
            if (incomingScope == IncomingScope.SelectedTarget)
                return "선택 대상 안에서는 쓰는 곳이 없습니다. 툴바 '사용처'를 '내 코드 전체'로 바꿔 보세요.";
            return "내 코드 안에서 쓰는 곳이 없습니다. (인스펙터·이벤트 연결·리플렉션으로만 쓰일 수 있습니다)";
        }

        private string HiddenComponentNote()
        {
            return hiddenComponentCount > 0 ? $"Unity 컴포넌트 {hiddenComponentCount}개 숨김 (툴바에서 표시)" : string.Empty;
        }

        private void RebuildEntries()
        {
            entries.Clear();
            hiddenComponentCount = 0;

            switch (target)
            {
                case GameObject gameObject:
                    var seenTypes = new HashSet<System.Type>();
                    foreach (Component component in gameObject.GetComponents<Component>())
                    {
                        // 스크립트가 사라진 컴포넌트는 null로 나온다.
                        if (component == null || !seenTypes.Add(component.GetType()))
                            continue;
                        AddEntry(component.GetType());
                    }

                    break;
                case MonoScript script:
                    AddEntry(script.GetClass());
                    break;
                case Object asset when asset != null:
                    AddEntry(asset.GetType());
                    break;
            }
        }

        private void AddEntry(System.Type type)
        {
            if (type == null)
                return;

            CodeOrigin origin = classifier.Classify(type);
            if (origin == CodeOrigin.External && !showEngineComponents)
            {
                hiddenComponentCount++;
                return;
            }

            // 부모 클래스도 내 코드면 같이 보여 줘야 상속받은 메소드를 찾을 수 있다. NetworkBehaviour 같은 바깥 부모는 너무 커서 뺀다.
            List<TypeMemberGroup> groups = TypeMemberCatalog.GetHierarchy(type, baseType => classifier.Classify(baseType) == CodeOrigin.Own, origin != CodeOrigin.External);
            entries.Add(new ComponentEntry(CodeTypeNames.Friendly(type), type, origin, groups));
        }

        /// <summary>오브젝트는 컴포넌트 목록으로, 스크립트 파일은 그 클래스로 본다. 폴더 등 코드와 무관한 선택은 무시한다.</summary>
        private static Object PickTarget(Object selected)
        {
            switch (selected)
            {
                case GameObject gameObject:
                    return gameObject;
                case MonoScript script when script.GetClass() != null:
                    return script;
                case ScriptableObject scriptableObject:
                    return scriptableObject;
                default:
                    return null;
            }
        }
    }
}
