using System;
using GuardVsMannequin.Core;
using GuardVsMannequin.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GuardVsMannequin.Player
{
    /// <summary>로컬 소유자 아바타에서 설정한 키 바인딩으로 Input System 액션을 읽는다.</summary>
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
        private InputAction reloadAction;
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
        public event Action ReloadPressed;
        public event Action<int> SpectateCycled;

        /// <summary>
        /// 원본 에셋을 그대로 쓰면 다른 아바타와 상태가 섞이고 바인딩 변경이 에셋에 남으므로, 복제본에 저장된 키를 적용해 쓴다.
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
            reloadAction = map.FindAction("Reload", true);
            rotateAction = map.FindAction("Rotate", true);
            sprintAction = map.FindAction("Sprint", true);
            jumpAction = map.FindAction("Jump", true);
            spectatePreviousAction = map.FindAction("SpectatePrevious", true);
            spectateNextAction = map.FindAction("SpectateNext", true);
            for (int i = 0; i < EmoteCount; i++)
                emoteActions[i] = map.FindAction($"Emote{i + 1}", true);
        }

        /// <summary>
        /// 소유자 아바타에서만 켜지므로, 켜지는 순간이 곧 내 게임이 시작되는 순간이라 커서도 함께 잠근다.
        /// </summary>
        private void OnEnable()
        {
            primaryAction.performed += OnPrimaryPerformed;
            primaryAction.canceled += OnPrimaryCanceled;
            secondaryAction.performed += OnSecondaryPerformed;
            flashlightAction.performed += OnFlashlightPerformed;
            reloadAction.performed += OnReloadPerformed;
            spectatePreviousAction.performed += OnSpectatePreviousPerformed;
            spectateNextAction.performed += OnSpectateNextPerformed;
            foreach (InputAction emoteAction in emoteActions)
                emoteAction.performed += OnEmotePerformed;
            OptionsStore.BindingsChanged += OnBindingsChanged;

            runtimeActions.Enable();
            GameCursor.SetLocked(true);
        }

        /// <summary>
        /// 사라진 아바타가 입력에 계속 반응하지 않도록 액션을 끄고 구독을 모두 푼다.
        /// </summary>
        private void OnDisable()
        {
            runtimeActions.Disable();

            primaryAction.performed -= OnPrimaryPerformed;
            primaryAction.canceled -= OnPrimaryCanceled;
            secondaryAction.performed -= OnSecondaryPerformed;
            flashlightAction.performed -= OnFlashlightPerformed;
            reloadAction.performed -= OnReloadPerformed;
            spectatePreviousAction.performed -= OnSpectatePreviousPerformed;
            spectateNextAction.performed -= OnSpectateNextPerformed;
            foreach (InputAction emoteAction in emoteActions)
                emoteAction.performed -= OnEmotePerformed;
            OptionsStore.BindingsChanged -= OnBindingsChanged;

            // 다시 스폰으로 새 로컬 아바타가 이미 생겼으면 그 아바타의 커서 잠금을 풀지 않는다.
            PlayerIdentity currentPlayer = LocalPlayerEvents.Current;
            if (currentPlayer == null || currentPlayer.gameObject == gameObject)
                GameCursor.SetLocked(false);
        }

        /// <summary>
        /// Instantiate로 만든 에셋은 씬과 함께 지워지지 않으므로 직접 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            if (runtimeActions != null)
                Destroy(runtimeActions);
        }

        /// <summary>
        /// 게임 중 설정 화면에서 키를 바꿨을 때 다시 스폰하지 않아도 바로 적용되게 한다.
        /// </summary>
        private void OnBindingsChanged()
        {
            OptionsStore.ApplyBindings(runtimeActions);
        }

        /// <summary>메뉴를 누르려던 클릭이 게임 입력으로 새지 않도록 커서가 잠겼을 때만 전달한다.</summary>
        private void OnPrimaryPerformed(InputAction.CallbackContext context)
        {
            if (GameCursor.IsLocked)
                PrimaryPressed?.Invoke();
        }

        /// <summary>누른 채 메뉴를 열면 상호작용이 끝나지 않고 남으므로, 뗌은 잠금과 관계없이 전달한다.</summary>
        private void OnPrimaryCanceled(InputAction.CallbackContext context)
        {
            PrimaryReleased?.Invoke();
        }

        /// <summary>
        /// 메뉴를 누르려던 클릭이 게임 입력으로 새지 않도록 커서가 잠겼을 때만 전달한다.
        /// </summary>
        private void OnSecondaryPerformed(InputAction.CallbackContext context)
        {
            if (GameCursor.IsLocked)
                SecondaryPressed?.Invoke();
        }

        /// <summary>메뉴에서 친 키가 게임 입력으로 새지 않도록 커서가 잠겼을 때만 전달한다.</summary>
        private void OnFlashlightPerformed(InputAction.CallbackContext context)
        {
            if (GameCursor.IsLocked)
                FlashlightPressed?.Invoke();
        }

        /// <summary>메뉴에서 친 키가 게임 입력으로 새지 않도록 커서가 잠겼을 때만 전달한다.</summary>
        private void OnReloadPerformed(InputAction.CallbackContext context)
        {
            if (GameCursor.IsLocked)
                ReloadPressed?.Invoke();
        }

        /// <summary>관전 중에는 커서를 쓰지 않아도 대상을 넘길 수 있어야 해서 잠금과 관계없이 전달한다.</summary>
        private void OnSpectatePreviousPerformed(InputAction.CallbackContext context)
        {
            SpectateCycled?.Invoke(-1);
        }

        /// <summary>관전 중에는 커서를 쓰지 않아도 대상을 넘길 수 있어야 해서 잠금과 관계없이 전달한다.</summary>
        private void OnSpectateNextPerformed(InputAction.CallbackContext context)
        {
            SpectateCycled?.Invoke(1);
        }

        /// <summary>
        /// 여섯 액션이 한 콜백을 같이 쓰므로, 어느 액션인지로 번호(1~6)를 알아낸다.
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
