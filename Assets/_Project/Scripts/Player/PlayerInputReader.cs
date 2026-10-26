using System;
using GuardVsMannequin.Core;
using GuardVsMannequin.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// Input System 액션을 <see cref="IPlayerInput"/>으로 변환한다.
    /// 로컬 소유자 아바타에서만 활성화되며, 설정 화면에서 바꾼 키 바인딩을 적용한다.
    /// 커서가 풀려 있으면(ESC 메뉴, 결과 화면) 게임 입력을 막는다.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour, IPlayerInput
    {
        private const string PlayerMapName = "Player";
        private const int EmoteCount = 6;

        [SerializeField] private InputActionAsset inputActions;

        private InputActionAsset runtimeActions;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction primaryAction;
        private InputAction secondaryAction;
        private InputAction flashlightAction;
        private InputAction rotateAction;
        private InputAction sprintAction;
        private InputAction jumpAction;
        private InputAction spectatePreviousAction;
        private InputAction spectateNextAction;
        private readonly InputAction[] emoteActions = new InputAction[EmoteCount];

        public Vector2 Move => GameCursor.IsLocked ? moveAction?.ReadValue<Vector2>() ?? Vector2.zero : Vector2.zero;
        public Vector2 Look => GameCursor.IsLocked ? lookAction?.ReadValue<Vector2>() ?? Vector2.zero : Vector2.zero;
        public float Rotate => GameCursor.IsLocked ? rotateAction?.ReadValue<float>() ?? 0f : 0f;
        public bool IsPrimaryHeld => GameCursor.IsLocked && primaryAction != null && primaryAction.IsPressed();
        public bool IsSprintHeld => GameCursor.IsLocked && sprintAction != null && sprintAction.IsPressed();
        public bool IsJumpHeld => GameCursor.IsLocked && jumpAction != null && jumpAction.IsPressed();

        public event Action PrimaryPressed;
        public event Action PrimaryReleased;
        public event Action SecondaryPressed;
        public event Action<int> EmotePressed;
        public event Action FlashlightPressed;
        public event Action<int> SpectateCycled;

        /// <summary>
        /// 에셋을 복제해 저장된 키 바인딩을 적용하고 액션을 찾는다. 복제본을 쓰면 다른 아바타와 상태가 섞이지 않는다.
        /// </summary>
        private void Awake()
        {
            runtimeActions = Instantiate(inputActions);
            OptionsStore.ApplyBindings(runtimeActions);
            InputActionMap map = runtimeActions.FindActionMap(PlayerMapName, true);

            moveAction = map.FindAction("Move", true);
            lookAction = map.FindAction("Look", true);
            primaryAction = map.FindAction("Primary", true);
            secondaryAction = map.FindAction("Secondary", true);
            flashlightAction = map.FindAction("Flashlight", true);
            rotateAction = map.FindAction("Rotate", true);
            sprintAction = map.FindAction("Sprint", true);
            jumpAction = map.FindAction("Jump", true);
            spectatePreviousAction = map.FindAction("SpectatePrevious", true);
            spectateNextAction = map.FindAction("SpectateNext", true);
            for (int i = 0; i < EmoteCount; i++)
                emoteActions[i] = map.FindAction($"Emote{i + 1}", true);
        }

        /// <summary>
        /// 액션을 활성화하고 버튼·설정 변경 이벤트를 구독한다. 게임을 시작하면 커서를 잠근다.
        /// </summary>
        private void OnEnable()
        {
            primaryAction.performed += OnPrimaryPerformed;
            primaryAction.canceled += OnPrimaryCanceled;
            secondaryAction.performed += OnSecondaryPerformed;
            flashlightAction.performed += OnFlashlightPerformed;
            spectatePreviousAction.performed += OnSpectatePreviousPerformed;
            spectateNextAction.performed += OnSpectateNextPerformed;
            foreach (InputAction emote in emoteActions)
                emote.performed += OnEmotePerformed;
            OptionsStore.BindingsChanged += OnBindingsChanged;

            runtimeActions.Enable();
            GameCursor.SetLocked(true);
        }

        /// <summary>
        /// 액션을 비활성화하고 이벤트 구독을 해제한다.
        /// </summary>
        private void OnDisable()
        {
            runtimeActions.Disable();

            primaryAction.performed -= OnPrimaryPerformed;
            primaryAction.canceled -= OnPrimaryCanceled;
            secondaryAction.performed -= OnSecondaryPerformed;
            flashlightAction.performed -= OnFlashlightPerformed;
            spectatePreviousAction.performed -= OnSpectatePreviousPerformed;
            spectateNextAction.performed -= OnSpectateNextPerformed;
            foreach (InputAction emote in emoteActions)
                emote.performed -= OnEmotePerformed;
            OptionsStore.BindingsChanged -= OnBindingsChanged;

            // 다시 스폰으로 새 로컬 아바타가 이미 생겼으면 그 아바타의 커서 잠금을 풀지 않는다.
            PlayerIdentity current = LocalPlayerEvents.Current;
            if (current == null || current.gameObject == gameObject)
                GameCursor.SetLocked(false);
        }

        /// <summary>
        /// 복제한 입력 에셋을 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            if (runtimeActions != null)
                Destroy(runtimeActions);
        }

        /// <summary>
        /// 게임 중 설정 화면에서 키를 바꾸면 바로 적용한다.
        /// </summary>
        private void OnBindingsChanged()
        {
            OptionsStore.ApplyBindings(runtimeActions);
        }

        /// <summary>좌클릭 누름을 전달한다.</summary>
        private void OnPrimaryPerformed(InputAction.CallbackContext context)
        {
            if (GameCursor.IsLocked)
                PrimaryPressed?.Invoke();
        }

        /// <summary>좌클릭 뗌을 전달한다. 잠금 상태와 관계없이 전달해 상호작용이 끊기지 않게 한다.</summary>
        private void OnPrimaryCanceled(InputAction.CallbackContext context)
        {
            PrimaryReleased?.Invoke();
        }

        /// <summary>
        /// 우클릭을 전달한다.
        /// </summary>
        private void OnSecondaryPerformed(InputAction.CallbackContext context)
        {
            if (GameCursor.IsLocked)
                SecondaryPressed?.Invoke();
        }

        /// <summary>손전등 키를 전달한다.</summary>
        private void OnFlashlightPerformed(InputAction.CallbackContext context)
        {
            if (GameCursor.IsLocked)
                FlashlightPressed?.Invoke();
        }

        /// <summary>이전 관전 대상 키를 전달한다.</summary>
        private void OnSpectatePreviousPerformed(InputAction.CallbackContext context)
        {
            SpectateCycled?.Invoke(-1);
        }

        /// <summary>다음 관전 대상 키를 전달한다.</summary>
        private void OnSpectateNextPerformed(InputAction.CallbackContext context)
        {
            SpectateCycled?.Invoke(1);
        }

        /// <summary>
        /// 눌린 감정 표현 액션의 번호(1~6)를 찾아 전달한다.
        /// </summary>
        private void OnEmotePerformed(InputAction.CallbackContext context)
        {
            if (!GameCursor.IsLocked)
                return;

            int index = Array.IndexOf(emoteActions, context.action);
            if (index >= 0)
                EmotePressed?.Invoke(index + 1);
        }
    }
}
