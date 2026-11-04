using GuardVsMannequin.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 체력·스태미나 바 옆: 켜고 끄는 도구(<see cref="ISwitchableTool"/> — 손전등)의 아이콘과 단축키 상자.
    /// 켜져 있음 / 꺼져 있지만 켤 수 있음 / 지금은 쓸 수 없음(사냥 시간 아님)에 따라 아이콘과 키 글자 색을 바꾼다.
    /// </summary>
    public class HudSwitchToolIndicator : MonoBehaviour, IHudSection
    {
        [SerializeField] private GameObject indicatorPanel;

        [Tooltip("도구 아이콘. 비워 두면 키 상자만 보인다.")]
        [SerializeField] private Graphic icon;

        [SerializeField] private Text keyText;

        [Tooltip("키 이름을 읽을 입력 에셋. (실행 시 복제해 저장된 키 설정을 적용)")]
        [SerializeField] private InputActionAsset inputActions;

        [Tooltip("도구를 켜고 끄는 입력 액션 이름.")]
        [SerializeField] private string actionName = "Flashlight";

        [SerializeField] private Color onColor = new(1f, 0.9f, 0.45f, 1f);
        [SerializeField] private Color offColor = new(1f, 1f, 1f, 0.85f);
        [SerializeField] private Color unavailableColor = new(0.45f, 0.45f, 0.45f, 0.7f);

        private HudKeyNames keyNames;
        private HudContext context;
        private bool isKeyDirty = true;

        /// <inheritdoc />
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
            keyNames = new HudKeyNames(inputActions);
            keyNames.Changed += MarkKeyDirty;
        }

        /// <summary>
        /// 구독을 해제하고 키 이름 공급자를 정리한다.
        /// </summary>
        private void OnDestroy()
        {
            if (keyNames == null)
                return;
            keyNames.Changed -= MarkKeyDirty;
            keyNames.Dispose();
        }

        /// <inheritdoc />
        public void Hide()
        {
            indicatorPanel.SetActive(false);
        }

        /// <inheritdoc />
        public void Refresh(GameState state)
        {
            bool isInMatch = state is GameState.Preparation or GameState.Playing or GameState.Hunting;
            ISwitchableTool tool = FindSwitchableTool();
            bool visible = isInMatch && tool != null;
            indicatorPanel.SetActive(visible);
            if (!visible)
                return;

            if (isKeyDirty)
            {
                keyText.text = keyNames.Get(actionName);
                isKeyDirty = false;
            }
            Color stateColor = tool.IsOn ? onColor : tool.IsReady ? offColor : unavailableColor;
            keyText.color = stateColor;
            if (icon != null)
                icon.color = stateColor;
        }

        /// <summary>
        /// 로컬 아바타의 도구 중 켜고 끄는 도구를 찾는다. 없으면 null.
        /// </summary>
        private ISwitchableTool FindSwitchableTool()
        {
            foreach (ITool tool in context.LocalPlayer.Tools)
            {
                if (tool is ISwitchableTool switchable)
                    return switchable;
            }
            return null;
        }

        /// <summary>
        /// 다음 갱신 때 키 이름을 다시 읽게 한다.
        /// </summary>
        private void MarkKeyDirty()
        {
            isKeyDirty = true;
        }
    }
}
