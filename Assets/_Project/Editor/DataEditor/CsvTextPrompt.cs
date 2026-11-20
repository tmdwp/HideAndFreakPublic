using System;
using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>열 이름·구역 이름처럼 짧은 글 하나만 받을 때 별도 창 없이 버튼 아래에 띄우는 입력 칸.</summary>
    internal sealed class CsvTextPrompt : PopupWindowContent
    {
        private const string FieldName = "CsvTextPromptField";

        private readonly string title;
        private readonly Action<string> onConfirm;
        private string value;
        private bool isFocused;

        public CsvTextPrompt(string title, string initialValue, Action<string> onConfirm)
        {
            this.title = title;
            value = initialValue ?? string.Empty;
            this.onConfirm = onConfirm;
        }

        public override Vector2 GetWindowSize() => new Vector2(300f, 72f);

        /// <summary>마우스 없이 바로 입력하고 Enter로 끝낼 수 있도록 열리자마자 입력 칸에 포커스를 준다.</summary>
        public override void OnGUI(Rect rect)
        {
            Event current = Event.current;
            if (current.type == EventType.KeyDown && (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter))
            {
                Confirm();
                current.Use();
                return;
            }
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                editorWindow.Close();
                current.Use();
                return;
            }

            GUILayout.Label(title, EditorStyles.boldLabel);
            GUI.SetNextControlName(FieldName);
            value = EditorGUILayout.TextField(value);
            if (!isFocused)
            {
                EditorGUI.FocusTextInControl(FieldName);
                isFocused = true;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("확인", GUILayout.Width(60f)))
                    Confirm();
                if (GUILayout.Button("취소", GUILayout.Width(60f)))
                    editorWindow.Close();
            }
        }

        private void Confirm()
        {
            string trimmed = value?.Trim();
            editorWindow.Close();
            if (!string.IsNullOrEmpty(trimmed))
                onConfirm(trimmed);
        }
    }
}
