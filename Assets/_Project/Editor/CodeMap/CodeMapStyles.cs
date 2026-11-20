using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor.CodeMap
{
    /// <summary>목록과 그래프가 같은 색·글자로 멤버 종류를 표시해야 두 화면을 오가며 바로 알아볼 수 있다.</summary>
    internal static class CodeMapStyles
    {
        private static readonly Color neutralGray = new Color(0.42f, 0.42f, 0.42f);

        private static GUIStyle badgeStyle;
        private static GUIStyle nodeLabelStyle;
        private static GUIStyle usageLabelStyle;
        private static GUIStyle focusDetailStyle;
        private static GUIStyle sectionStyle;
        private static GUIStyle wrappedCenterStyle;

        public static Color GraphBackground => EditorGUIUtility.isProSkin ? new Color(0.15f, 0.15f, 0.15f) : new Color(0.78f, 0.78f, 0.78f);
        public static Color SelectionColor => new Color(0.24f, 0.48f, 0.9f, 0.35f);
        public static Color HoverColor => new Color(1f, 1f, 1f, 0.08f);

        public static GUIStyle BadgeStyle => badgeStyle ?? (badgeStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
            padding = new RectOffset(0, 0, 0, 0),
        });

        public static GUIStyle NodeLabelStyle => nodeLabelStyle ?? (nodeLabelStyle = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Clip,
            normal = { textColor = Color.white },
            hover = { textColor = Color.white },
        });

        public static GUIStyle UsageLabelStyle => usageLabelStyle ?? (usageLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = new Color(1f, 1f, 1f, 0.8f) },
        });

        public static GUIStyle FocusDetailStyle => focusDetailStyle ?? (focusDetailStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Clip,
            normal = { textColor = new Color(1f, 1f, 1f, 0.8f) },
        });

        public static GUIStyle SectionStyle => sectionStyle ?? (sectionStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.LowerLeft,
        });

        public static GUIStyle WrappedCenterStyle => wrappedCenterStyle ?? (wrappedCenterStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
        {
            alignment = TextAnchor.MiddleCenter,
        });

        public static Color KindColor(CodeMemberKind kind)
        {
            switch (kind)
            {
                case CodeMemberKind.Field:
                    return new Color(0.24f, 0.56f, 0.34f);
                case CodeMemberKind.Property:
                    return new Color(0.16f, 0.52f, 0.58f);
                case CodeMemberKind.Event:
                    return new Color(0.56f, 0.36f, 0.72f);
                case CodeMemberKind.Constructor:
                    return new Color(0.4f, 0.42f, 0.58f);
                default:
                    return new Color(0.26f, 0.44f, 0.78f);
            }
        }

        public static string KindLetter(CodeMemberKind kind)
        {
            switch (kind)
            {
                case CodeMemberKind.Field:
                    return "F";
                case CodeMemberKind.Property:
                    return "P";
                case CodeMemberKind.Event:
                    return "E";
                case CodeMemberKind.Constructor:
                    return "C";
                default:
                    return "M";
            }
        }

        /// <summary>내 코드가 아닌 멤버는 흐리게 칠해 눈이 내 코드부터 가게 한다.</summary>
        public static Color NodeColor(CodeMemberKind kind, CodeOrigin origin)
        {
            Color color = KindColor(kind);
            switch (origin)
            {
                case CodeOrigin.Project:
                    return Color.Lerp(color, neutralGray, 0.45f);
                case CodeOrigin.External:
                    return Color.Lerp(color, neutralGray, 0.75f);
                default:
                    return color;
            }
        }

        public static Color UsageColor(ReferenceUsage usage)
        {
            bool reads = (usage & ReferenceUsage.Read) != 0;
            bool writes = (usage & ReferenceUsage.Write) != 0;
            if (reads && writes)
                return new Color(0.95f, 0.8f, 0.3f);
            if (writes)
                return new Color(0.98f, 0.58f, 0.22f);
            if (reads)
                return new Color(0.42f, 0.82f, 0.48f);
            return new Color(0.48f, 0.66f, 1f);
        }

        public static void DrawKindBadge(Rect rect, CodeMemberKind kind)
        {
            EditorGUI.DrawRect(rect, KindColor(kind));
            GUI.Label(rect, KindLetter(kind), BadgeStyle);
        }
    }
}
