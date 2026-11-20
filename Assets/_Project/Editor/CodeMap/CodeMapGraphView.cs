using System;
using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor.CodeMap
{
    /// <summary>배치된 그래프를 그리고 클릭을 요청으로 바꾼다. 노드 클릭은 그 멤버로 이동, 가운데 노드 더블클릭·우클릭 메뉴는 코드 열기.</summary>
    internal sealed class CodeMapGraphView
    {
        private const float BadgeWidth = 18f;
        private const float UsageLabelWidth = 78f;
        private const float EdgeTangent = 50f;
        private const float EdgeWidth = 2f;
        private const float HoveredEdgeWidth = 4.5f;

        private readonly Action<CodeMapInteraction> onInteraction;
        private Vector2 scroll;

        public CodeMapGraphView(Action<CodeMapInteraction> onInteraction)
        {
            this.onInteraction = onInteraction;
        }

        public void ResetScroll() => scroll = Vector2.zero;

        public void Draw(Rect area, CodeMapGraph graph)
        {
            EditorGUI.DrawRect(area, CodeMapStyles.GraphBackground);
            var content = new Rect(0f, 0f, Mathf.Max(graph.Size.x, area.width - 16f), Mathf.Max(graph.Size.y, area.height - 16f));
            scroll = GUI.BeginScrollView(area, scroll, content);

            Vector2 mouse = Event.current.mousePosition;
            GraphNode hovered = FindHovered(graph, mouse);

            if (Event.current.type == EventType.Repaint)
            {
                foreach (GraphNode node in graph.Nodes)
                    DrawEdge(graph.Focus, node, node == hovered);
            }

            foreach (GraphSectionLabel section in graph.Sections)
                GUI.Label(section.Rect, section.Text, section.IsNote ? EditorStyles.wordWrappedMiniLabel : CodeMapStyles.SectionStyle);

            foreach (GraphNode node in graph.Nodes)
                DrawNode(node, node == hovered);
            DrawFocus(graph.Focus, graph.Focus == hovered);

            HandleMouse(graph, hovered);
            GUI.EndScrollView();
        }

        private static GraphNode FindHovered(CodeMapGraph graph, Vector2 mouse)
        {
            if (graph.Focus.Rect.Contains(mouse))
                return graph.Focus;
            foreach (GraphNode node in graph.Nodes)
            {
                if (node.Rect.Contains(mouse))
                    return node;
            }

            return null;
        }

        /// <summary>선 방향은 항상 "사용하는 쪽 → 쓰이는 쪽"이라 왼쪽 열은 가운데로, 가운데는 오른쪽 열로 들어간다.</summary>
        private static void DrawEdge(GraphNode focus, GraphNode node, bool isHovered)
        {
            Vector3 start;
            Vector3 end;
            if (node.Role == GraphNodeRole.Incoming)
            {
                start = new Vector3(node.Rect.xMax, node.Rect.center.y);
                end = new Vector3(focus.Rect.xMin, focus.Rect.center.y);
            }
            else
            {
                start = new Vector3(focus.Rect.xMax, focus.Rect.center.y);
                end = new Vector3(node.Rect.xMin, node.Rect.center.y);
            }

            Color color = CodeMapStyles.UsageColor(node.Usage);
            if (!isHovered)
                color.a = node.Origin == CodeOrigin.Own ? 0.75f : 0.35f;
            Handles.DrawBezier(start, end, start + Vector3.right * EdgeTangent, end + Vector3.left * EdgeTangent, color, null,
                isHovered ? HoveredEdgeWidth : EdgeWidth);
        }

        private static void DrawNode(GraphNode node, bool isHovered)
        {
            Rect rect = node.Rect;
            EditorGUI.DrawRect(rect, CodeMapStyles.NodeColor(node.Member.Kind, node.Origin));
            if (isHovered)
                EditorGUI.DrawRect(rect, CodeMapStyles.HoverColor);

            // 선 색과 같은 띠를 붙여, 선을 따라가지 않아도 읽기/쓰기/호출을 알 수 있게 한다.
            float stripeX = node.Role == GraphNodeRole.Incoming ? rect.xMax - 3f : rect.xMin;
            EditorGUI.DrawRect(new Rect(stripeX, rect.y, 3f, rect.height), CodeMapStyles.UsageColor(node.Usage));

            CodeMapStyles.DrawKindBadge(new Rect(rect.x + 4f, rect.y + 2f, BadgeWidth, rect.height - 4f), node.Member.Kind);
            var labelRect = new Rect(rect.x + BadgeWidth + 8f, rect.y, rect.width - BadgeWidth - UsageLabelWidth - 10f, rect.height);
            GUI.Label(labelRect, new GUIContent(node.Label, node.Member.Tooltip), CodeMapStyles.NodeLabelStyle);
            GUI.Label(new Rect(rect.xMax - UsageLabelWidth - 6f, rect.y, UsageLabelWidth, rect.height), node.UsageLabel, CodeMapStyles.UsageLabelStyle);
        }

        private static void DrawFocus(GraphNode focus, bool isHovered)
        {
            Rect rect = focus.Rect;
            EditorGUI.DrawRect(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), Color.white * 0.85f);
            EditorGUI.DrawRect(rect, CodeMapStyles.NodeColor(focus.Member.Kind, focus.Origin));
            if (isHovered)
                EditorGUI.DrawRect(rect, CodeMapStyles.HoverColor);

            CodeMapStyles.DrawKindBadge(new Rect(rect.x + 6f, rect.y + 6f, BadgeWidth, 16f), focus.Member.Kind);
            GUI.Label(new Rect(rect.x + BadgeWidth + 12f, rect.y + 4f, rect.width - BadgeWidth - 16f, 20f),
                new GUIContent(focus.Label, focus.Member.Tooltip), CodeMapStyles.NodeLabelStyle);

            string detail = CodeTypeNames.Qualified(focus.Member.DeclaringType) + (focus.Member.IsEngineCallback ? "  · 엔진 콜백" : string.Empty);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 24f, rect.width - 12f, 16f), detail, CodeMapStyles.FocusDetailStyle);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 39f, rect.width - 12f, 16f), "더블클릭: 코드 열기", CodeMapStyles.FocusDetailStyle);
        }

        private void HandleMouse(CodeMapGraph graph, GraphNode hovered)
        {
            Event current = Event.current;
            if (hovered == null || current.type != EventType.MouseDown)
                return;

            if (current.button == 1)
            {
                ShowContextMenu(hovered);
                current.Use();
                return;
            }

            if (current.button != 0)
                return;

            current.Use();
            if (hovered == graph.Focus)
            {
                if (current.clickCount >= 2)
                    onInteraction(new CodeMapInteraction(hovered.Member, CodeMapAction.OpenSource));
                return;
            }

            // 한 번 클릭하면 노드가 가운데로 옮겨져 더블클릭 위치가 어긋나므로, 다른 노드는 한 번 클릭으로 이동만 한다.
            onInteraction(new CodeMapInteraction(hovered.Member, CodeMapAction.Focus));
        }

        private void ShowContextMenu(GraphNode node)
        {
            var menu = new GenericMenu();
            CodeMember member = node.Member;
            if (node.Role != GraphNodeRole.Focus)
                menu.AddItem(new GUIContent("이 멤버 기준으로 보기"), false, () => onInteraction(new CodeMapInteraction(member, CodeMapAction.Focus)));
            menu.AddItem(new GUIContent("코드 열기"), false, () => onInteraction(new CodeMapInteraction(member, CodeMapAction.OpenSource)));
            menu.AddItem(new GUIContent("시그니처 복사"), false, () => EditorGUIUtility.systemCopyBuffer = member.Signature.Trim());
            menu.ShowAsContext();
        }
    }
}
