using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace GuardVsMannequin.Settings
{
    /// <summary>
    /// 설정 화면에서 바꿀 수 있는 키 목록. 입력 에셋의 액션 이름과 바인딩(복합 입력의 부분 이름)을 가리킨다.
    /// </summary>
    public static class KeyBindingCatalog
    {
        /// <summary>
        /// 바꿀 수 있는 키 하나.
        /// </summary>
        public readonly struct Entry
        {
            public readonly string actionName;

            /// <summary>복합 입력(WASD, Q/E)의 부분 이름. 단일 바인딩이면 null.</summary>
            public readonly string partName;

            /// <summary>표시 이름 문구의 key. (strings.csv)</summary>
            public readonly string labelKey;

            /// <summary>항목을 만든다.</summary>
            public Entry(string actionName, string partName, string labelKey)
            {
                this.actionName = actionName;
                this.partName = partName;
                this.labelKey = labelKey;
            }
        }

        /// <summary>표시 순서대로의 키 목록.</summary>
        public static readonly IReadOnlyList<Entry> All = new[]
        {
            new Entry("Move", "up", "key.moveUp"),
            new Entry("Move", "down", "key.moveDown"),
            new Entry("Move", "left", "key.moveLeft"),
            new Entry("Move", "right", "key.moveRight"),
            new Entry("Primary", null, "key.primary"),
            new Entry("Secondary", null, "key.secondary"),
            new Entry("Flashlight", null, "key.flashlight"),
            new Entry("Reload", null, "key.reload"),
            new Entry("Rotate", "negative", "key.rotateLeft"),
            new Entry("Rotate", "positive", "key.rotateRight"),
            new Entry("Emote1", null, "key.emote1"),
            new Entry("Emote2", null, "key.emote2"),
            new Entry("Emote3", null, "key.emote3"),
            new Entry("Emote4", null, "key.emote4"),
            new Entry("Emote5", null, "key.emote5"),
            new Entry("Emote6", null, "key.emote6"),
            new Entry("SpectatePrevious", null, "key.spectatePrevious"),
            new Entry("SpectateNext", null, "key.spectateNext"),
            new Entry("Sprint", null, "key.sprint"),
            new Entry("Jump", null, "key.jump")
        };

        /// <summary>
        /// 액션 이름과 부분 이름이 같은 항목을 찾는다. (설정 화면의 키 설정 줄을 항목과 짝지을 때)
        /// </summary>
        /// <param name="partName">복합 입력의 부분 이름. 단일 바인딩이면 null 또는 빈 문자열.</param>
        public static bool TryFind(string actionName, string partName, out Entry entry)
        {
            string part = string.IsNullOrEmpty(partName) ? null : partName;
            foreach (Entry candidate in All)
            {
                if (candidate.actionName == actionName && candidate.partName == part)
                {
                    entry = candidate;
                    return true;
                }
            }
            entry = default;
            return false;
        }

        /// <summary>
        /// 액션에서 항목이 가리키는 바인딩의 인덱스를 찾는다. 없으면 -1.
        /// </summary>
        public static int FindBindingIndex(InputAction action, string partName)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (partName == null ? !binding.isComposite && !binding.isPartOfComposite : binding.isPartOfComposite && binding.name == partName)
                    return i;
            }
            return -1;
        }
    }
}
