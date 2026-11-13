using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Player;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>사망·미참가 상태에서만 관전 정보를 보여 주는 HUD 하단.</summary>
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

        /// <summary>
        /// 매 프레임 씬에서 찾지 않도록 공용 정보를 기억한다.
        /// </summary>
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
        }

        /// <summary>
        /// 네트워크 준비 전에는 관전 대상이 없으므로 숨긴다.
        /// </summary>
        public void Hide()
        {
            bottomPanel.SetActive(false);
        }

        /// <summary>살아 있는 동안은 다른 영역이 자리를 쓰므로 아바타가 없을 때만 보인다.</summary>
        public void Refresh(GameState state)
        {
            bool isVisible = state != GameState.Waiting && context.LocalPlayer.Player == null;
            bottomPanel.SetActive(isVisible);
            if (!isVisible)
            {
                // 다시 보일 때 바로 최신 문구를 보여주도록 한다.
                refreshTimer.Reset();
                return;
            }
            if (!refreshTimer.Tick(RefreshInterval))
                return;

            spectatorText.text = GetSpectatorText();
        }

        /// <summary>죽은 사람과 처음부터 관전한 사람은 상황이 달라 머리말을 구별한다.</summary>
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
