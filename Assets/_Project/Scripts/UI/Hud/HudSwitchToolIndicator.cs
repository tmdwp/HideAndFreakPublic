using GuardVsMannequin.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>켜고 끄는 도구의 아이콘과 단축키를 상태별 색으로 보여 준다.</summary>
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

        /// <summary>
        /// 키 이름은 설정·언어가 바뀔 때만 다시 읽으면 되므로 변경 알림을 받아 표시만 해 둔다.
        /// </summary>
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
            keyNames = new HudKeyNames(inputActions);
            keyNames.Changed += MarkKeyDirty;
        }

        /// <summary>
        /// 키 이름 공급자는 정적 이벤트를 구독하고 입력 에셋 복제본을 가지므로 함께 정리한다.
        /// </summary>
        private void OnDestroy()
        {
            if (keyNames == null)
                return;
            keyNames.Changed -= MarkKeyDirty;
            keyNames.Dispose();
        }

        /// <summary>
        /// 네트워크 준비 전에는 도구가 없으므로 숨긴다.
        /// </summary>
        public void Hide()
        {
            indicatorPanel.SetActive(false);
        }

        /// <summary>도구가 있을 때만 보이고, 지금 쓸 수 있는지 상태별 색으로 알린다.</summary>
        public void Refresh(GameState state)
        {
            bool isInMatch = state is GameState.Preparation or GameState.Playing or GameState.Hunting;
            ISwitchableTool tool = FindSwitchableTool();
            bool isVisible = isInMatch && tool != null;
            indicatorPanel.SetActive(isVisible);
            if (!isVisible)
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

        /// <summary>역할마다 도구 구성이 달라 인터페이스로 찾고 첫 도구만 쓴다.</summary>
        private ISwitchableTool FindSwitchableTool()
        {
            foreach (ITool tool in context.LocalPlayer.Tools)
            {
                if (tool is ISwitchableTool switchableTool)
                    return switchableTool;
            }
            return null;
        }

        /// <summary>
        /// 알림이 Refresh 밖에서 오므로 바로 읽지 않고 다음 갱신 때 읽게 한다.
        /// </summary>
        private void MarkKeyDirty()
        {
            isKeyDirty = true;
        }
    }
}
