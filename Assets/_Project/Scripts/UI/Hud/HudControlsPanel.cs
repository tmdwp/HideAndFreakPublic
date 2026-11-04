using System.Text;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// HUD 중앙 좌측: 조작 키 안내. (이동, 좌클릭, 우클릭, 장전(총이 있을 때), 달리기, 점프)
    /// 키 이름은 지금 키 설정을 따르고, 좌·우클릭 동작 이름은 역할과 단계(<see cref="HudText.GetRoleText"/>)를 따른다.
    /// 문구는 키 설정·언어·역할·단계가 바뀔 때만 다시 만든다.
    /// </summary>
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
        private bool shownHunting;
        private bool shownReload;

        /// <summary>
        /// 키 이름 공급자를 만들고 키 설정·언어 변경을 구독한다.
        /// </summary>
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
            keyNames = new HudKeyNames(inputActions);
            keyNames.Changed += MarkDirty;
        }

        /// <summary>
        /// 구독을 해제하고 키 이름 공급자를 정리한다.
        /// </summary>
        private void OnDestroy()
        {
            if (keyNames == null)
                return;
            keyNames.Changed -= MarkDirty;
            keyNames.Dispose();
        }

        /// <inheritdoc />
        public void Hide()
        {
            controlsPanel.SetActive(false);
        }

        /// <inheritdoc />
        public void Refresh(GameState state)
        {
            bool isInMatch = state is GameState.Preparation or GameState.Playing or GameState.Hunting;
            bool visible = isInMatch && context.LocalPlayer.Player != null;
            controlsPanel.SetActive(visible);
            if (!visible)
                return;

            PlayerRole role = context.LocalPlayer.Player.Role;
            bool isHunting = state == GameState.Hunting;
            // 탄창 있는 도구(총)를 가진 경우에만 장전 줄을 보인다.
            bool showReload = context.LocalPlayer.Ammo != null;
            if (!isDirty && role == shownRole && isHunting == shownHunting && showReload == shownReload)
                return;

            shownRole = role;
            shownHunting = isHunting;
            shownReload = showReload;
            isDirty = false;
            controlsText.text = BuildText(role, isHunting, showReload);
        }

        /// <summary>
        /// "키 : 동작" 줄들을 만든다.
        /// </summary>
        private string BuildText(PlayerRole role, bool isHunting, bool showReload)
        {
            builder.Clear();
            builder.Append("<b>").Append(Loc.Get("hud.controls.title")).Append("</b>");
            AppendLine("Move", Loc.Get("hud.controls.move"));
            AppendLine("Primary", HudText.GetRoleText(role, HudText.PrimaryPart, isHunting));
            AppendLine("Secondary", HudText.GetRoleText(role, HudText.SecondaryPart, isHunting));
            if (showReload)
                AppendLine("Reload", Loc.Get("hud.controls.reload"));
            AppendLine("Sprint", Loc.Get("hud.controls.sprint"));
            AppendLine("Jump", Loc.Get("hud.controls.jump"));
            return builder.ToString();
        }

        /// <summary>
        /// 한 줄을 덧붙인다. 동작 이름이 없으면(안내 없는 역할) 건너뛴다.
        /// </summary>
        private void AppendLine(string actionName, string label)
        {
            if (string.IsNullOrEmpty(label))
                return;
            builder.Append('\n').Append(keyNames.Get(actionName)).Append("  :  ").Append(label);
        }

        /// <summary>
        /// 다음 갱신 때 문구를 다시 만들게 한다.
        /// </summary>
        private void MarkDirty()
        {
            isDirty = true;
        }
    }
}
