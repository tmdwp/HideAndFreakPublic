using System;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.UI
{
    /// <summary>되돌릴 수 없는 동작 전에 한 번 더 묻도록, 로비·룸·게임 어디서든 같은 확인 창을 띄운다.</summary>
    public class ConfirmDialog : MonoBehaviour
    {
        /// <summary>에디터 미리 굽기가 만드는 프리팹 경로(Resources 기준).</summary>
        public const string PrefabPath = "UI/ConfirmDialog";

        [Tooltip("확인 창 모양. 비우면 실행 중에 기본 모양으로 만든다.")]
        [SerializeField] private ConfirmDialogView view;

        private static ConfirmDialog instance;
        private static int escapeHandledFrame = -1;

        private Action pendingConfirm;

        /// <summary>창이 떠 있는 동안 ESC 메뉴·화면 뒤로 가기가 반응하지 않도록 다른 화면이 확인한다.</summary>
        public static bool IsOpen => instance != null && instance.pendingConfirm != null;

        /// <summary>
        /// 화면마다 창을 따로 두지 않도록 어느 씬에도 속하지 않는 오브젝트로 한 번 만든다.
        /// 미리 만든 프리팹이 있으면 에디터에서 고친 모양을 쓰고, 없으면 기본 모양을 코드로 만든다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateInstance()
        {
            var prefab = Resources.Load<ConfirmDialog>(PrefabPath);
            GameObject created = prefab != null
                ? Instantiate(prefab).gameObject
                : new GameObject(nameof(ConfirmDialog), typeof(ConfirmDialog));
            created.name = nameof(ConfirmDialog);
            DontDestroyOnLoad(created);
        }

        /// <summary>프리팹 없이 만들어졌으면 화면을 코드로 채운다.</summary>
        private void Awake()
        {
            instance = this;
            EnsureView();
            view.Bind(Confirm, Cancel);
            view.SetVisible(false);
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        /// <summary>에디터 미리 굽기도 같은 기본 모양으로 프리팹을 만들도록 공개한다.</summary>
        public void EnsureView()
        {
            if (view == null)
                view = GetComponentInChildren<ConfirmDialogView>(true);
            if (view == null)
                view = ConfirmDialogView.Create(transform);
        }

        /// <summary>
        /// 정적 이벤트라 끊지 않으면 파괴된 창이 계속 불린다.
        /// </summary>
        private void OnDestroy()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            if (instance == this)
                instance = null;
        }

        /// <summary>ESC를 따로 처리하는 화면이 없는 곳(룸 하단 등)에서도 창을 닫을 수 있도록 직접 확인한다.</summary>
        private void Update()
        {
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                ConsumeEscape();
        }

        /// <summary>창을 띄우고, 확인을 눌렀을 때만 동작을 실행한다.</summary>
        /// <param name="confirmKey">확인 버튼 문구 key. 무엇을 하는지 버튼만 보고 알 수 있게 동작 이름을 쓴다.</param>
        public static void Show(string titleKey, string messageKey, string confirmKey, Action onConfirm)
        {
            if (instance == null || onConfirm == null)
            {
                // 창이 없을 때 막아 버리면 나가기조차 못 하므로 바로 실행한다.
                onConfirm?.Invoke();
                return;
            }

            instance.pendingConfirm = onConfirm;
            instance.view.SetTexts(Loc.Get(titleKey), Loc.Get(messageKey), Loc.Get(confirmKey), Loc.Get("confirm.cancel"));
            instance.view.SetVisible(true);
            // 버튼을 눌러야 하므로 커서를 푼다. 닫힌 뒤 다시 잠그는 것은 창을 연 화면이 정한다.
            GameCursor.SetLocked(false);
        }

        /// <summary>
        /// 같은 프레임에 ESC를 읽는 화면이 여럿이라, 먼저 읽은 쪽이 창을 닫으면 나머지는 그 ESC를 무시하도록 처리 여부를 돌려준다.
        /// </summary>
        /// <returns>이번 ESC를 확인 창이 썼으면 true.</returns>
        public static bool ConsumeEscape()
        {
            if (escapeHandledFrame == Time.frameCount)
                return true;
            if (!IsOpen)
                return false;

            escapeHandledFrame = Time.frameCount;
            instance.Cancel();
            return true;
        }

        /// <summary>동작 안에서 다시 창을 띄울 수 있도록 창을 먼저 닫고 실행한다.</summary>
        private void Confirm()
        {
            Action action = pendingConfirm;
            Close();
            action?.Invoke();
        }

        /// <summary>
        /// 취소는 아무 동작 없이 닫기만 한다.
        /// </summary>
        private void Cancel()
        {
            Close();
        }

        /// <summary>
        /// 연 화면의 씬이 사라진 뒤 확인을 누르면 파괴된 오브젝트를 부르므로 닫는다.
        /// </summary>
        private void OnSceneUnloaded(Scene scene)
        {
            if (pendingConfirm != null)
                Close();
        }

        /// <summary>
        /// 남은 동작이 다음 창에서 실행되지 않도록 함께 지운다.
        /// </summary>
        private void Close()
        {
            pendingConfirm = null;
            view.SetVisible(false);
        }
    }
}
