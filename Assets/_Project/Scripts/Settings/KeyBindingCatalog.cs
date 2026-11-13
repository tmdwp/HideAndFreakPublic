using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace GuardVsMannequin.Settings
{
    /// <summary>설정 화면에서 바꿀 수 있는 키 목록.</summary>
    public static class KeyBindingCatalog
    {
        /// <summary>
        /// 바꿀 수 있는 키 하나.
        /// </summary>
        public readonly struct Entry
        {
            public readonly string actionName;

            /// <summary>한 액션에 여러 키가 묶인 경우 그중 하나를 가리키는 부분 이름(단일이면 null).</summary>
            public readonly string partName;

            /// <summary>strings.csv의 key.</summary>
            public readonly string labelKey;

            /// <summary>목록을 표 형태로 한눈에 볼 수 있도록 세 값을 한 줄에 넣어 만든다.</summary>
            public Entry(string actionName, string partName, string labelKey)
            {
                this.actionName = actionName;
                this.partName = partName;
                this.labelKey = labelKey;
            }
        }

        /// <summary>설정 화면에 이 순서대로 보인다.</summary>
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

        /// <summary>프리팹에는 액션·부분 이름만 저장되므로 그 값으로 목록 항목을 찾는다.</summary>
        public static bool TryFind(string actionName, string partName, out Entry entry)
        {
            string normalizedPart = string.IsNullOrEmpty(partName) ? null : partName;
            foreach (Entry candidate in All)
            {
                if (candidate.actionName == actionName && candidate.partName == normalizedPart)
                {
                    entry = candidate;
                    return true;
                }
            }
            entry = default;
            return false;
        }

        /// <summary>재바인딩 API가 인덱스를 요구하므로 항목이 가리키는 바인딩 위치를 찾는다.</summary>
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
