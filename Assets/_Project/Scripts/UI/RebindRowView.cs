using System;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>변경을 누르면 다음에 누른 키로 바꾸는 키 설정 한 줄.</summary>
    public class RebindRowView : MonoBehaviour
    {
        /// <summary>키 입력을 기다리는 동안 ESC가 메뉴를 열거나 닫지 않도록 다른 화면이 확인한다.</summary>
        public static bool IsAnyRebinding { get; private set; }

        [SerializeField] private string actionName;

        [Tooltip("복합 입력(WASD, Q/E)의 부분 이름. 단일 바인딩이면 비워 둔다.")]
        [SerializeField] private string partName;

        [SerializeField] private Text labelText;
        [SerializeField] private Text keyText;
        [SerializeField] private Button rebindButton;
        [SerializeField] private Button resetButton;

        public string ActionName => actionName;

        public string PartName => partName;

        // 맞바꾼 줄을 알아보도록 키 글자를 잠깐 강조하는 시간(초)과 색.
        private const float SwapFlashDuration = 1.5f;
        private static readonly Color SwapFlashColor = new(0.45f, 0.9f, 0.55f, 1f);

        private InputAction action;
        private string labelKey;
        private int bindingIndex = -1;
        private Action<RebindRowView, string> onChanged;
        private InputActionRebindingExtensions.RebindingOperation rebindOperation;
        private string pathBeforeChange;
        private Color normalKeyColor;
        private float flashEndTime;

        /// <summary>
        /// 원본 입력 에셋을 고치지 않도록 설정 화면이 편집하는 복제본에서 대상 바인딩을 찾는다.
        /// </summary>
        /// <param name="editingActions">설정 화면이 편집하는 입력 에셋 복제본.</param>
        /// <param name="labelKey">표시 이름 문구의 key.</param>
        /// <param name="changed">바인딩이 바뀌면 부를 함수. 겹친 키를 맞바꿀 수 있도록 이 줄과 바꾸기 전 키 경로를 넘긴다.</param>
        public void Bind(InputActionAsset editingActions, string labelKey, Action<RebindRowView, string> changed)
        {
            this.labelKey = labelKey;
            onChanged = changed;
            action = editingActions.FindAction(actionName, true);
            bindingIndex = KeyBindingCatalog.FindBindingIndex(action, string.IsNullOrEmpty(partName) ? null : partName);

            rebindButton.onClick.AddListener(StartRebind);
            resetButton.onClick.AddListener(ResetBinding);
            normalKeyColor = keyText.color;
            Refresh();
        }

        /// <summary>
        /// 화면이 닫힌 뒤 누른 키가 몰래 바인딩되지 않도록 진행 중인 입력 대기를 취소하고, 강조도 끝낸다.
        /// </summary>
        private void OnDisable()
        {
            rebindOperation?.Cancel();
            if (flashEndTime > 0f)
            {
                flashEndTime = 0f;
                keyText.color = normalKeyColor;
            }
        }

        /// <summary>
        /// 강조 시간이 지나면 원래 색으로 돌린다.
        /// </summary>
        private void Update()
        {
            if (flashEndTime > 0f && Time.unscaledTime >= flashEndTime)
            {
                flashEndTime = 0f;
                keyText.color = normalKeyColor;
            }
        }

        /// <summary>
        /// 다른 줄에서 키를 바꿔 이 줄의 키가 바뀌었음을 알아보도록 잠깐 다른 색으로 보인다.
        /// </summary>
        public void FlashSwapped()
        {
            Refresh();
            keyText.color = SwapFlashColor;
            flashEndTime = Time.unscaledTime + SwapFlashDuration;
        }

        /// <summary>
        /// 언어나 바인딩이 바뀌면 표시가 낡으므로 바깥에서도 다시 그릴 수 있게 공개한다.
        /// </summary>
        public void Refresh()
        {
            labelText.text = Loc.Get(labelKey);
            keyText.text = bindingIndex >= 0 ? action.GetBindingDisplayString(bindingIndex) : "-";
        }

        /// <summary>누른 키가 게임 동작으로 처리되지 않도록 액션을 끄고, 잘못 잡히는 마우스 이동·휠은 제외한다.</summary>
        private void StartRebind()
        {
            if (bindingIndex < 0 || IsAnyRebinding)
                return;

            IsAnyRebinding = true;
            pathBeforeChange = action.bindings[bindingIndex].effectivePath;
            keyText.text = Loc.Get("options.pressKey");
            action.Disable();

            rebindOperation = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(_ => FinishRebind(isChanged: true))
                .OnCancel(_ => FinishRebind(isChanged: false))
                .Start();
        }

        /// <summary>
        /// 대기 작업은 해제하지 않으면 메모리가 남으므로 정리하고, 취소했을 때는 저장하지 않는다.
        /// </summary>
        private void FinishRebind(bool isChanged)
        {
            rebindOperation?.Dispose();
            rebindOperation = null;
            IsAnyRebinding = false;

            Refresh();
            if (isChanged)
                onChanged?.Invoke(this, pathBeforeChange);
        }

        /// <summary>
        /// 덮어쓴 값만 지우면 기본 키로 돌아가므로 오버라이드를 제거한다.
        /// </summary>
        private void ResetBinding()
        {
            if (bindingIndex < 0 || IsAnyRebinding)
                return;

            // 기본 키가 다른 동작에 이미 쓰이고 있을 수 있어 바꾸기 전 키를 함께 넘긴다.
            string previousPath = action.bindings[bindingIndex].effectivePath;
            action.RemoveBindingOverride(bindingIndex);
            Refresh();
            onChanged?.Invoke(this, previousPath);
        }
    }
}
