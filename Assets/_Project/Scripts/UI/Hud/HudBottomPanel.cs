using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Player;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// HUD 하단: 사망·미참가 상태의 관전 정보. 살아 있는 동안은 숨긴다.
    /// (살아 있을 때의 하단은 체력 <see cref="HudHealthBar"/>, 스태미나 <see cref="HudStaminaBar"/>, 손전등 <see cref="HudSwitchToolIndicator"/>,
    /// 발각 아이콘 <see cref="HudRevealIndicator"/>가, 조준점 둘레는 총 <see cref="HudAmmoDisplay"/>와 쿨타임 바 <see cref="HudCooldownBars"/>가 맡는다)
    /// </summary>
    public class HudBottomPanel : MonoBehaviour, IHudSection
    {
        [SerializeField] private GameObject bottomPanel;

        [Tooltip("관전 안내 문구.")]
        [FormerlySerializedAs("objectiveText")]
        [SerializeField] private Text spectatorText;

        // 관전 대상이 바뀌는 것을 따라가는 정도면 충분해 이 간격으로만 문구를 다시 만든다.
        private const float RefreshInterval = 0.2f;

        private HudContext context;
        private RefreshTimer refreshTimer;

        /// <inheritdoc />
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
        }

        /// <inheritdoc />
        public void Hide()
        {
            bottomPanel.SetActive(false);
        }

        /// <inheritdoc />
        public void Refresh(GameState state)
        {
            bool visible = state != GameState.Waiting && context.LocalPlayer.Player == null;
            bottomPanel.SetActive(visible);
            if (!visible)
            {
                // 다시 보일 때 바로 최신 문구를 보여주도록 한다.
                refreshTimer.Reset();
                return;
            }
            if (!refreshTimer.Tick(RefreshInterval))
                return;

            spectatorText.text = GetSpectatorText();
        }

        /// <summary>
        /// 사망 또는 미참가 상태의 관전 안내 문구를 만든다.
        /// </summary>
        private string GetSpectatorText()
        {
            string header = Loc.Get(context.LocalPlayer.LastRole == PlayerRole.None ? "hud.spectating" : "hud.spectatingDead");
            SpectatorViewPoint target = context.Spectator != null ? context.Spectator.CurrentTarget : null;
            if (target == null)
                return $"{header}\n{Loc.Get("hud.spectate.none")}";

            PlayerIdentity identity = target.Identity;
            return $"{header}\n{Loc.Format("hud.spectate.target", identity.DisplayName, HudText.GetRoleName(identity.Role))}";
        }
    }
}
