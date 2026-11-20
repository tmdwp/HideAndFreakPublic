using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor.CodeMap
{
    /// <summary>선택한 오브젝트의 컴포넌트 하나와, 그 타입·상속받은 내 코드 부모 타입의 멤버.</summary>
    internal sealed class ComponentEntry
    {
        public ComponentEntry(string title, Type type, CodeOrigin origin, IReadOnlyList<TypeMemberGroup> groups)
        {
            Title = title;
            Type = type;
            Origin = origin;
            Groups = groups;
        }

        public string Title { get; }
        public Type Type { get; }
        public CodeOrigin Origin { get; }
        public IReadOnlyList<TypeMemberGroup> Groups { get; }
    }

    /// <summary>왼쪽 목록. 클릭하면 그 멤버 기준 그래프, 더블클릭하면 코드 열기.</summary>
    internal sealed class CodeMapMemberListView
    {
        private const float RowHeight = 18f;
        private const float BadgeWidth = 16f;
        private const float Indent = 14f;

        private readonly Action<CodeMapInteraction> onInteraction;
        private readonly Dictionary<Type, bool> expandedByType = new Dictionary<Type, bool>();
        private Vector2 scroll;

        public CodeMapMemberListView(Action<CodeMapInteraction> onInteraction)
        {
            this.onInteraction = onInteraction;
        }

        public void Draw(Rect area, IReadOnlyList<ComponentEntry> entries, string search, CodeMember selected, string footerNote)
        {
            GUILayout.BeginArea(area);
            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (entries.Count == 0)
                EditorGUILayout.HelpBox("하이어라키에서 오브젝트를 선택하거나, 프로젝트 창에서 스크립트를 선택하세요.", MessageType.Info);

            bool isSearching = !string.IsNullOrEmpty(search);
            foreach (ComponentEntry entry in entries)
                DrawEntry(entry, search, isSearching, selected);

            if (!string.IsNullOrEmpty(footerNote))
                EditorGUILayout.LabelField(footerNote, EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawEntry(ComponentEntry entry, string search, bool isSearching, CodeMember selected)
        {
            List<(TypeMemberGroup group, List<CodeMember> members)> visibleGroups = entry.Groups
                .Select(group => (group, group.Members.Where(member => Matches(member, search)).ToList()))
                .Where(pair => !isSearching || pair.Item2.Count > 0)
                .ToList();
            if (isSearching && visibleGroups.Count == 0)
                return;

            if (!expandedByType.TryGetValue(entry.Type, out bool isExpanded))
                isExpanded = entry.Origin == CodeOrigin.Own;

            int memberCount = visibleGroups.Sum(pair => pair.members.Count);
            string title = $"{entry.Title}  ({memberCount})";
            var titleContent = new GUIContent(title, CodeTypeNames.Qualified(entry.Type));
            if (isSearching)
            {
                // 검색 중에는 접힌 컴포넌트 안의 결과도 보여야 하므로, 접힘 상태를 건드리지 않고 펼친 채로 그린다.
                EditorGUILayout.LabelField(titleContent, EditorStyles.boldLabel);
            }
            else
            {
                isExpanded = EditorGUILayout.Foldout(isExpanded, titleContent, true, EditorStyles.foldoutHeader);
                expandedByType[entry.Type] = isExpanded;
                if (!isExpanded)
                    return;
            }

            foreach ((TypeMemberGroup group, List<CodeMember> members) in visibleGroups)
            {
                if (group.Type != entry.Type)
                    EditorGUILayout.LabelField("상속: " + CodeTypeNames.Friendly(group.Type), EditorStyles.miniBoldLabel);
                foreach (CodeMember member in members)
                    DrawRow(member, member.Equals(selected));
            }

            GUILayout.Space(4f);
        }

        private void DrawRow(CodeMember member, bool isSelected)
        {
            Rect rect = GUILayoutUtility.GetRect(10f, RowHeight, GUILayout.ExpandWidth(true));
            Event current = Event.current;
            bool isHovered = rect.Contains(current.mousePosition);

            if (current.type == EventType.Repaint)
            {
                if (isSelected)
                    EditorGUI.DrawRect(rect, CodeMapStyles.SelectionColor);
                else if (isHovered)
                    EditorGUI.DrawRect(rect, CodeMapStyles.HoverColor);
            }

            CodeMapStyles.DrawKindBadge(new Rect(rect.x + Indent, rect.y + 2f, BadgeWidth, rect.height - 4f), member.Kind);
            string prefix = member.IsStatic ? "static " : string.Empty;
            string suffix = member.IsEngineCallback ? "  ⟳" : string.Empty;
            var labelRect = new Rect(rect.x + Indent + BadgeWidth + 6f, rect.y, rect.width - Indent - BadgeWidth - 6f, rect.height);
            GUI.Label(labelRect, new GUIContent(prefix + member.ShortLabel + suffix, member.Tooltip), AccessStyle(member));

            if (current.type == EventType.MouseDown && current.button == 0 && isHovered)
            {
                current.Use();
                CodeMapAction action = current.clickCount >= 2 ? CodeMapAction.OpenSource : CodeMapAction.Focus;
                onInteraction(new CodeMapInteraction(member, action));
            }
        }

        /// <summary>public 멤버는 굵게 해 바깥에서 쓰는 입구가 어디인지 바로 보이게 한다.</summary>
        private static GUIStyle AccessStyle(CodeMember member)
        {
            return member.Access == "public" ? EditorStyles.boldLabel : EditorStyles.label;
        }

        private static bool Matches(CodeMember member, string search)
        {
            return string.IsNullOrEmpty(search) || member.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
