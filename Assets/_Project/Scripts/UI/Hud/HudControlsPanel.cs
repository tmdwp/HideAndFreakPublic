using System.Text;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>지금 키 설정과 역할·단계에 맞는 조작 키 안내를 보여 준다.</summary>
    public class HudControlsPanel : MonoBehaviour, IHudSection
    {
        [SerializeField] private GameObject controlsPanel;
        [SerializeField] private Text controlsText;

        [Tooltip("키 이름을 읽을 입력 에셋. (실행 시 복제해 저장된 키 설정을 적용)")]
        [SerializeField] private InputActionAsset inputActions;

        private readonly StringBuilder builder = new();
        private HudKeyNames keyNames;
        private HudContext context;
        private bool isDirty = true;
        private PlayerRole shownRole = PlayerRole.None;
        private bool wasHuntingShown;
        private bool wasReloadShown;

        /// <summary>
        /// 키 설정이나 언어가 바뀌어도 매 프레임 비교하지 않도록 변경 알림을 받아 다시 만들 표시만 해 둔다.
        /// </summary>
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
            keyNames = new HudKeyNames(inputActions);
            keyNames.Changed += MarkDirty;
        }

        /// <summary>
        /// 키 이름 공급자는 정적 이벤트를 구독하고 입력 에셋 복제본을 가지므로 함께 정리한다.
        /// </summary>
        private void OnDestroy()
        {
            if (keyNames == null)
                return;
            keyNames.Changed -= MarkDirty;
            keyNames.Dispose();
        }

        /// <summary>
        /// 네트워크 준비 전에는 역할을 몰라 안내할 동작이 없으므로 숨긴다.
        /// </summary>
        public void Hide()
        {
            controlsPanel.SetActive(false);
        }

        /// <summary>관전 중이면 숨기고, 가비지를 줄이려고 내용이 바뀔 때만 다시 만든다.</summary>
        public void Refresh(GameState state)
        {
            bool isInMatch = state is GameState.Preparation or GameState.Playing or GameState.Hunting;
            bool isVisible = isInMatch && context.LocalPlayer.Player != null;
            controlsPanel.SetActive(isVisible);
            if (!isVisible)
                return;

            PlayerRole role = context.LocalPlayer.Player.Role;
            bool isHunting = state == GameState.Hunting;
            // 탄창 있는 도구(총)를 가진 경우에만 장전 줄을 보인다.
            bool isReloadShown = context.LocalPlayer.Ammo != null;
            if (!isDirty && role == shownRole && isHunting == wasHuntingShown && isReloadShown == wasReloadShown)
                return;

            shownRole = role;
            wasHuntingShown = isHunting;
            wasReloadShown = isReloadShown;
            isDirty = false;
            controlsText.text = BuildText(role, isHunting, isReloadShown);
        }

        /// <summary>
        /// 줄 수가 역할마다 달라서 StringBuilder로 필요한 줄만 이어 붙인다.
        /// </summary>
        private string BuildText(PlayerRole role, bool isHunting, bool isReloadShown)
        {
            builder.Clear();
            builder.Append("<b>").Append(Loc.Get("hud.controls.title")).Append("</b>");
            AppendLine("Move", Loc.Get("hud.controls.move"));
            AppendLine("Primary", HudText.GetRoleText(role, HudText.PrimaryPart, isHunting));
            AppendLine("Secondary", HudText.GetRoleText(role, HudText.SecondaryPart, isHunting));
            if (isReloadShown)
                AppendLine("Reload", Loc.Get("hud.controls.reload"));
            AppendLine("Sprint", Loc.Get("hud.controls.sprint"));
            AppendLine("Jump", Loc.Get("hud.controls.jump"));
            return builder.ToString();
        }

        /// <summary>
        /// 안내가 없는 역할의 동작은 "키 :" 만 남아 어색하므로 건너뛴다.
        /// </summary>
        private void AppendLine(string actionName, string label)
        {
            if (string.IsNullOrEmpty(label))
                return;
            builder.Append('\n').Append(keyNames.Get(actionName)).Append("  :  ").Append(label);
        }

        /// <summary>
        /// 알림이 Refresh 밖에서 오므로 바로 다시 만들지 않고 다음 갱신 때 만들게 한다.
        /// </summary>
        private void MarkDirty()
        {
            isDirty = true;
        }
    }
}
