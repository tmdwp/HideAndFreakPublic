using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GuardVsMannequin.Editor.CodeMap
{
    internal enum GraphNodeRole
    {
        Focus,
        Incoming,
        Outgoing,
    }

    internal sealed class GraphNode
    {
        public GraphNode(CodeMember member, ReferenceUsage usage, int count, GraphNodeRole role, Rect rect, string label, CodeOrigin origin)
        {
            Member = member;
            Usage = usage;
            Count = count;
            Role = role;
            Rect = rect;
            Label = label;
            Origin = origin;
        }

        public CodeMember Member { get; }
        public ReferenceUsage Usage { get; }
        public int Count { get; }
        public GraphNodeRole Role { get; }
        public Rect Rect { get; }
        public string Label { get; }
        public CodeOrigin Origin { get; }

        public string UsageLabel => new CodeReference(Member, Usage, Count).UsageLabel + (Count > 1 ? " ×" + Count : string.Empty);
    }

    internal readonly struct GraphSectionLabel
    {
        public GraphSectionLabel(string text, Rect rect, bool isNote = false)
        {
            Text = text;
            Rect = rect;
            IsNote = isNote;
        }

        public string Text { get; }
        public Rect Rect { get; }
        public bool IsNote { get; }
    }

    /// <summary>그리기와 분리된 배치 결과. 화면은 이것을 그리기만 한다.</summary>
    internal sealed class CodeMapGraph
    {
        public GraphNode Focus { get; set; }
        public List<GraphNode> Nodes { get; } = new List<GraphNode>();
        public List<GraphSectionLabel> Sections { get; } = new List<GraphSectionLabel>();
        public Vector2 Size { get; set; }
    }

    /// <summary>왼쪽 열에 사용하는 곳, 가운데에 선택한 멤버, 오른쪽 열에 사용하는 것을 놓는 3열 배치.</summary>
    internal static class CodeMapGraphLayout
    {
        public const float NodeWidth = 270f;
        public const float NodeHeight = 22f;
        public const float FocusHeight = 58f;
        private const float NodeGap = 3f;
        private const float SectionGap = 14f;
        private const float SectionTitleHeight = 20f;
        private const float ColumnGap = 110f;
        private const float Padding = 16f;

        public static CodeMapGraph Build(
            CodeMember focus,
            IReadOnlyList<CodeReference> incoming,
            IReadOnlyList<CodeReference> outgoing,
            Func<CodeMember, CodeOrigin> classify,
            string incomingEmptyNote)
        {
            var graph = new CodeMapGraph();
            float incomingX = Padding;
            float focusX = incomingX + NodeWidth + ColumnGap;
            float outgoingX = focusX + NodeWidth + ColumnGap;
            float top = Padding;

            var incomingSections = new List<(string title, IReadOnlyList<CodeReference> references)>
            {
                ("사용하는 곳", SortByOwner(incoming, focus, classify)),
            };
            var outgoingSections = new List<(string title, IReadOnlyList<CodeReference> references)>
            {
                ("호출하는 메소드·이벤트", SortByOwner(outgoing.Where(reference => IsCallable(reference.Target.Kind)).ToList(), focus, classify)),
                ("읽고 쓰는 변수·프로퍼티", SortByOwner(outgoing.Where(reference => !IsCallable(reference.Target.Kind)).ToList(), focus, classify)),
            };

            float incomingBottom = PlaceColumn(graph, incomingSections, GraphNodeRole.Incoming, incomingX, top, focus, classify, incomingEmptyNote);
            float outgoingBottom = focus.HasBody
                ? PlaceColumn(graph, outgoingSections, GraphNodeRole.Outgoing, outgoingX, top, focus, classify, "본문에서 쓰는 멤버가 없습니다. (툴바 '호출 표시' 범위를 넓혀 보세요)")
                : top;

            var focusRect = new Rect(focusX, top + SectionTitleHeight, NodeWidth, FocusHeight);
            graph.Sections.Add(new GraphSectionLabel("선택한 멤버", new Rect(focusX, top, NodeWidth, SectionTitleHeight)));
            graph.Focus = new GraphNode(focus, ReferenceUsage.None, 0, GraphNodeRole.Focus, focusRect, focus.ShortLabel, classify(focus));

            float width = (focus.HasBody ? outgoingX : focusX) + NodeWidth + Padding;
            float height = Mathf.Max(incomingBottom, outgoingBottom, focusRect.yMax) + Padding;
            graph.Size = new Vector2(width, height);
            return graph;
        }

        private static float PlaceColumn(
            CodeMapGraph graph,
            List<(string title, IReadOnlyList<CodeReference> references)> sections,
            GraphNodeRole role,
            float x,
            float y,
            CodeMember focus,
            Func<CodeMember, CodeOrigin> classify,
            string emptyNote)
        {
            bool isEmpty = sections.All(section => section.references.Count == 0);
            if (isEmpty)
            {
                graph.Sections.Add(new GraphSectionLabel(sections[0].title, new Rect(x, y, NodeWidth, SectionTitleHeight)));
                graph.Sections.Add(new GraphSectionLabel(emptyNote, new Rect(x, y + SectionTitleHeight, NodeWidth, NodeHeight * 2f), true));
                return y + SectionTitleHeight + NodeHeight * 2f;
            }

            foreach ((string title, IReadOnlyList<CodeReference> references) in sections)
            {
                if (references.Count == 0)
                    continue;

                graph.Sections.Add(new GraphSectionLabel($"{title} ({references.Count})", new Rect(x, y, NodeWidth, SectionTitleHeight)));
                y += SectionTitleHeight;
                foreach (CodeReference reference in references)
                {
                    CodeMember member = reference.Target;
                    var rect = new Rect(x, y, NodeWidth, NodeHeight);
                    graph.Nodes.Add(new GraphNode(member, reference.Usage, reference.Count, role, rect, LabelFor(member, focus), classify(member)));
                    y += NodeHeight + NodeGap;
                }

                y += SectionGap;
            }

            return y;
        }

        /// <summary>같은 클래스 멤버를 먼저, 그다음 내 코드·프로젝트·외부 순으로. 같은 묶음 안에서는 본문에 나온 순서를 지킨다.</summary>
        private static IReadOnlyList<CodeReference> SortByOwner(IReadOnlyList<CodeReference> references, CodeMember focus, Func<CodeMember, CodeOrigin> classify)
        {
            return references
                .OrderBy(reference => reference.Target.DeclaringType == focus.DeclaringType ? 0 : 1)
                .ThenBy(reference => (int)classify(reference.Target))
                .ToList();
        }

        private static string LabelFor(CodeMember member, CodeMember focus)
        {
            if (member.DeclaringType == focus.DeclaringType || member.Kind == CodeMemberKind.Constructor)
                return member.ShortLabel;
            return CodeTypeNames.Friendly(member.DeclaringType) + "." + member.ShortLabel;
        }

        private static bool IsCallable(CodeMemberKind kind)
        {
            return kind == CodeMemberKind.Method || kind == CodeMemberKind.Constructor || kind == CodeMemberKind.Event;
        }
    }
}
