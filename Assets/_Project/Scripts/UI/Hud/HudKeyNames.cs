using System;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Settings;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace GuardVsMannequin.UI
{
    /// <summary>HUD 키 이름이 키 설정·언어 변경을 따라가도록 바뀌면 알린다.</summary>
    public sealed class HudKeyNames : IDisposable
    {
        private readonly InputActionAsset displayActions;

        /// <summary>표시 문구를 매 프레임 다시 만들지 않도록, 다시 읽어야 할 때만 알린다.</summary>
        public event Action Changed;

        /// <summary>
        /// 원본 입력 에셋을 고치지 않도록 복제본에 저장된 키 설정을 적용한다.
        /// </summary>
        /// <param name="source">키 이름을 읽을 입력 에셋.</param>
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

        /// <summary>표시 규칙을 한곳에 두려고 KeyBindingDisplay에 맡긴다.</summary>
        public string Get(string actionName)
        {
            return KeyBindingDisplay.Get(displayActions, actionName);
        }

        /// <summary>
        /// 정적 이벤트는 끊지 않으면 HUD가 파괴된 뒤에도 불리고, 복제본은 씬과 함께 지워지지 않아 직접 정리한다.
        /// </summary>
        public void Dispose()
        {
            OptionsStore.BindingsChanged -= OnBindingsChanged;
            Loc.LanguageChanged -= OnLanguageChanged;
            if (displayActions != null)
                Object.Destroy(displayActions);
        }

        /// <summary>
        /// 게임 중 설정 화면에서 키를 바꾸면 복제본은 모르므로 다시 적용한 뒤 알린다.
        /// </summary>
        private void OnBindingsChanged()
        {
            if (displayActions != null)
                OptionsStore.ApplyBindings(displayActions);
            Changed?.Invoke();
        }

        /// <summary>
        /// 마우스 버튼 같은 키 이름은 번역 문구라 언어가 바뀌면 달라지므로 알린다.
        /// </summary>
        private void OnLanguageChanged()
        {
            Changed?.Invoke();
        }
    }
}
