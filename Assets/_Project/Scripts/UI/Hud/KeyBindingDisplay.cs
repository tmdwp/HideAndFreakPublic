using System.Text;
using GuardVsMannequin.Localization;
using UnityEngine.InputSystem;

namespace GuardVsMannequin.UI
{
    /// <summary>키 설정을 바꾸면 HUD 안내도 따라 바뀌도록 지금 묶인 키를 표시용 이름으로 바꾼다.</summary>
    public static class KeyBindingDisplay
    {
        // WASD처럼 보이도록 하는 복합 입력 부분의 표시 순서.
        private static readonly string[] PartOrder = { "up", "left", "down", "right", "negative", "positive" };

        /// <summary>HUD 안내에는 대표 키 하나면 되므로 첫 바인딩만 쓴다.</summary>
        public static string Get(InputActionAsset actions, string actionName)
        {
            InputAction action = actions != null ? actions.FindAction(actionName) : null;
            if (action == null)
                return "-";

            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite)
                    return GetCompositeDisplay(action, i);
                if (!binding.isPartOfComposite)
                    return GetSingleDisplay(action, i);
            }
            return "-";
        }

        /// <summary>저장 순서는 읽는 순서와 달라 정해진 순서로 다시 놓는다.</summary>
        private static string GetCompositeDisplay(InputAction action, int compositeIndex)
        {
            var builder = new StringBuilder();
            bool isAllSingleCharacter = true;
            var partNames = new string[PartOrder.Length];

            for (int i = compositeIndex + 1; i < action.bindings.Count && action.bindings[i].isPartOfComposite; i++)
            {
                int order = System.Array.IndexOf(PartOrder, action.bindings[i].name);
                if (order < 0)
                    continue;
                partNames[order] = GetSingleDisplay(action, i);
                isAllSingleCharacter &= partNames[order].Length == 1;
            }

            foreach (string partName in partNames)
            {
                if (string.IsNullOrEmpty(partName))
                    continue;
                if (builder.Length > 0 && !isAllSingleCharacter)
                    builder.Append('/');
                builder.Append(partName);
            }
            return builder.Length > 0 ? builder.ToString() : "-";
        }

        /// <summary>
        /// 기본 이름("LMB" 등)은 영어 약어라 알아보기 어려우므로 번역 문구가 있으면 그것을 쓴다.
        /// </summary>
        private static string GetSingleDisplay(InputAction action, int bindingIndex)
        {
            string localizationKey = GetLocalizationKey(action.bindings[bindingIndex].effectivePath);
            return localizationKey != null && Loc.HasKey(localizationKey) ? Loc.Get(localizationKey) : action.GetBindingDisplayString(bindingIndex);
        }

        /// <summary>키마다 문구를 따로 등록하지 않도록 바인딩 경로에서 문구 key를 만든다.</summary>
        private static string GetLocalizationKey(string path)
        {
            if (string.IsNullOrEmpty(path) || path[0] != '<')
                return null;

            int deviceEndIndex = path.IndexOf('>');
            int slashIndex = path.IndexOf('/', deviceEndIndex + 1);
            if (deviceEndIndex < 0 || slashIndex < 0)
                return null;

            string deviceName = path.Substring(1, deviceEndIndex - 1).ToLowerInvariant();
            return $"input.{deviceName}.{path.Substring(slashIndex + 1)}";
        }
    }
}
