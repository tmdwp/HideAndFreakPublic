using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Player;
using UnityEngine;
using UnityEngine.InputSystem;
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

        [Tooltip("관전 전환 키 이름을 읽을 입력 에셋. (실행 시 복제해 저장된 키 설정을 적용)")]
        [SerializeField] private InputActionAsset inputActions;

        // 관전 대상이 바뀌는 것을 따라가는 정도면 충분해 이 간격으로만 문구를 다시 만든다.
        private const float RefreshInterval = 0.2f;

        private HudContext context;
        private RefreshTimer refreshTimer;
        private HudKeyNames keyNames;

        /// <summary>
        /// 매 프레임 씬에서 찾지 않도록 공용 정보를 기억하고, 키를 바꾸면 안내도 바로 바뀌도록 키 이름 변경을 듣는다.
        /// </summary>
        public void Bind(HudContext hudContext)
        {
            context = hudContext;
            keyNames = new HudKeyNames(inputActions);
            // 구조체 필드의 메서드를 바로 넘기면 복사본이 초기화되므로 람다로 필드 자체를 초기화한다.
            keyNames.Changed += () => refreshTimer.Reset();
        }

        /// <summary>
        /// 키 이름 공급자는 정적 이벤트를 구독하고 입력 에셋 복제본을 가지므로 함께 정리한다.
        /// </summary>
        private void OnDestroy()
        {
            keyNames?.Dispose();
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
            // 입력 에셋을 아직 연결하지 않은 예전 프리팹은 키를 읽을 수 없어 기본 키를 보인다.
            string previousKey = inputActions != null ? keyNames.Get("SpectatePrevious") : "←";
            string nextKey = inputActions != null ? keyNames.Get("SpectateNext") : "→";
            return $"{header}\n{Loc.Format("hud.spectate.target", identity.DisplayName, HudText.GetRoleName(identity.Role), previousKey, nextKey)}";
        }
    }
}
