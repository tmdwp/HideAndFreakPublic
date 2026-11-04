using System;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Settings;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// HUD에 보여줄 키 이름을 지금 키 설정대로 알려준다. 입력 에셋 복제본에 저장된 키 설정을 적용해 두고,
    /// 게임 중 키 설정·언어가 바뀌면 <see cref="Changed"/>로 알린다. (조작 안내, 손전등 키 아이콘이 함께 쓴다)
    /// </summary>
    public sealed class HudKeyNames : IDisposable
    {
        private readonly InputActionAsset displayActions;

        /// <summary>키 설정이나 언어가 바뀌어 표시 이름을 다시 읽어야 할 때.</summary>
        public event Action Changed;

        /// <summary>
        /// 입력 에셋을 복제해 저장된 키 설정을 적용하고 변경 이벤트를 구독한다.
        /// </summary>
        /// <param name="source">키 이름을 읽을 입력 에셋. null이면 모든 키가 "-".</param>
        public HudKeyNames(InputActionAsset source)
        {
            if (source != null)
            {
                displayActions = Object.Instantiate(source);
                OptionsStore.ApplyBindings(displayActions);
            }
            OptionsStore.BindingsChanged += OnBindingsChanged;
            Loc.LanguageChanged += OnLanguageChanged;
        }

        /// <summary>
        /// 액션에 지금 묶인 키의 표시 이름.
        /// </summary>
        public string Get(string actionName)
        {
            return KeyBindingDisplay.Get(displayActions, actionName);
        }

        /// <summary>
        /// 구독을 해제하고 복제본을 지운다.
        /// </summary>
        public void Dispose()
        {
            OptionsStore.BindingsChanged -= OnBindingsChanged;
            Loc.LanguageChanged -= OnLanguageChanged;
            if (displayActions != null)
                Object.Destroy(displayActions);
        }

        /// <summary>
        /// 게임 중 키 설정을 바꾸면 복제본에도 적용하고 알린다.
        /// </summary>
        private void OnBindingsChanged()
        {
            if (displayActions != null)
                OptionsStore.ApplyBindings(displayActions);
            Changed?.Invoke();
        }

        /// <summary>
        /// 언어가 바뀌면 키 이름(마우스 버튼 등 번역 문구)이 달라질 수 있어 알린다.
        /// </summary>
        private void OnLanguageChanged()
        {
            Changed?.Invoke();
        }
    }
}
