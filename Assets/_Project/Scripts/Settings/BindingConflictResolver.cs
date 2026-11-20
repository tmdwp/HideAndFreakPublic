using System;
using UnityEngine.InputSystem;

namespace GuardVsMannequin.Settings
{
    /// <summary>한 키가 두 동작에 묶이면 둘 중 하나가 먹히지 않으므로, 겹친 쪽에 원래 키를 넘겨 맞바꾼다.</summary>
    public static class BindingConflictResolver
    {
        // 관전 키는 살아 있는 동안 쓰지 않으므로 게임 조작 키와 같아도 부딪히지 않는다.
        private const string SpectatePrefix = "Spectate";

        /// <summary>바꾼 키와 같은 키를 쓰던 동작이 있으면 그 동작에 바꾸기 전 키를 넣는다.</summary>
        /// <param name="changed">방금 바꾼 항목.</param>
        /// <param name="previousPath">바꾸기 전 키 경로. 맞바꿀 때 상대에게 넘긴다.</param>
        /// <param name="swappedWith">맞바꾼 항목.</param>
        /// <returns>맞바꿨으면 true.</returns>
        public static bool TrySwap(InputActionAsset actions, KeyBindingCatalog.Entry changed, string previousPath, out KeyBindingCatalog.Entry swappedWith)
        {
            swappedWith = default;
            if (!TryGetPath(actions, changed, out string newPath) || string.IsNullOrEmpty(newPath) || SamePath(newPath, previousPath))
                return false;

            foreach (KeyBindingCatalog.Entry other in KeyBindingCatalog.All)
            {
                if (IsSameEntry(other, changed) || !IsSameContext(other, changed))
                    continue;
                if (!TryGetPath(actions, other, out string otherPath) || !SamePath(otherPath, newPath))
                    continue;

                InputAction otherAction = actions.FindAction(other.actionName);
                int otherIndex = KeyBindingCatalog.FindBindingIndex(otherAction, other.partName);
                otherAction.ApplyBindingOverride(otherIndex, previousPath);
                swappedWith = other;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 항목이 가리키는 바인딩의 현재 키 경로를 읽는다. 에셋에서 액션이 빠졌으면 실패로 본다.
        /// </summary>
        public static bool TryGetPath(InputActionAsset actions, KeyBindingCatalog.Entry entry, out string path)
        {
            path = null;
            InputAction action = actions.FindAction(entry.actionName);
            if (action == null)
                return false;
            int index = KeyBindingCatalog.FindBindingIndex(action, entry.partName);
            if (index < 0)
                return false;
            path = action.bindings[index].effectivePath;
            return true;
        }

        /// <summary>
        /// 관전 키와 게임 조작 키는 동시에 쓰지 않아 같은 묶음끼리만 겹침으로 본다.
        /// </summary>
        private static bool IsSameContext(KeyBindingCatalog.Entry first, KeyBindingCatalog.Entry second)
        {
            return first.actionName.StartsWith(SpectatePrefix, StringComparison.Ordinal)
                   == second.actionName.StartsWith(SpectatePrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// 구조체 기본 비교는 문구 key까지 보므로 액션·부분 이름만 비교한다.
        /// </summary>
        private static bool IsSameEntry(KeyBindingCatalog.Entry first, KeyBindingCatalog.Entry second)
        {
            return first.actionName == second.actionName && first.partName == second.partName;
        }

        /// <summary>
        /// 경로 표기는 대소문자가 섞여 들어올 수 있어 대소문자를 무시하고 비교한다.
        /// </summary>
        private static bool SamePath(string first, string second)
        {
            return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }
    }
}
