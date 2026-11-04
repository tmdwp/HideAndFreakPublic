using GuardVsMannequin.Cameras;
using GuardVsMannequin.Core;
using GuardVsMannequin.Objectives;
using GuardVsMannequin.Player;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 게임 HUD 관리 (uGUI). 게임 상태와 로컬 플레이어 정보(<see cref="HudContext"/>)를 영역들에 나눠 주고,
    /// 조준점·대기 문구와 커서를 처리한다. 각 영역은 <see cref="IHudSection"/> 컴포넌트가 맡는다:
    /// - <see cref="HudTopPanel"/>(상단 중앙): 단계 | 남은 시간, 남은 마네킹 / 전체 마네킹
    /// - <see cref="HudControlsPanel"/>(중앙 좌측): 조작 키 안내
    /// - <see cref="HudObjectivePanel"/>(중앙 우측): 역할별 목표와 남은 수
    /// - <see cref="HudHealthBar"/>·<see cref="HudStaminaBar"/>(하단 중앙): 체력 게이지와 그 아래 스태미나 막대
    /// - <see cref="HudRevealIndicator"/>(체력 바 위): 수류탄에 맞아 위치가 드러난 동안의 아이콘
    /// - <see cref="HudSwitchToolIndicator"/>(체력 바 옆): 손전등 단축키
    /// - <see cref="HudAmmoDisplay"/>·<see cref="HudCooldownRing"/>(조준점 둘레): 총 탄·장전 게이지, 구울 공격 쿨타임 고리
    /// - <see cref="HudCooldownBars"/>(조준점 왼쪽): 수류탄·변신 쿨타임 세로 바
    /// - <see cref="HudBottomPanel"/>(하단): 사망·미참가 시 관전 정보
    /// - <see cref="HudResultPanel"/>: 결과 화면
    /// 다른 플레이어의 체력과 킬 로그는 기획에 따라 표시하지 않는다.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        [Header("게임 상태 (씬에서 연결)")]
        [SerializeField] private GameStateMachine stateMachine;
        [SerializeField] private PlayerRegistry playerRegistry;
        [SerializeField] private ObjectiveTracker objectiveTracker;
        [SerializeField] private MatchResult matchResult;
        [SerializeField] private SpectatorController spectator;

        [Header("중앙")]
        [SerializeField] private GameObject crosshair;
        [SerializeField] private Text waitingText;

        private readonly LocalPlayerTracker localPlayer = new();
        private HudContext context;
        private IHudSection[] sections;
        private GameState previousState = GameState.Waiting;

        /// <summary>
        /// 공용 정보를 만들어 영역들에 넘기고 모두 숨긴다.
        /// </summary>
        private void Awake()
        {
            localPlayer.Attach();
            context = new HudContext(stateMachine, stateMachine, playerRegistry, objectiveTracker, matchResult, spectator, localPlayer);

            sections = GetComponentsInChildren<IHudSection>(true);
            foreach (IHudSection section in sections)
                section.Bind(context);

            SetAllHidden();
        }

        /// <summary>
        /// 이벤트 구독을 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            localPlayer.Detach();
        }

        /// <summary>
        /// 상태에 맞게 HUD 요소를 켜고 영역들을 갱신한다.
        /// </summary>
        private void Update()
        {
            // 네트워크 초기화 전에는 FishNet 상태를 읽을 수 없다.
            if (!stateMachine.IsClientReady())
            {
                SetAllHidden();
                return;
            }

            GameState state = stateMachine.CurrentState;
            HandleStateTransition(state);

            bool isWaiting = state == GameState.Waiting;
            if (isWaiting)
                localPlayer.ResetRole();

            waitingText.gameObject.SetActive(isWaiting);
            crosshair.SetActive(!isWaiting && localPlayer.Player != null && state != GameState.Ended);
            foreach (IHudSection section in sections)
                section.Refresh(state);
        }

        /// <summary>
        /// 모든 HUD 요소를 숨긴다.
        /// </summary>
        private void SetAllHidden()
        {
            crosshair.SetActive(false);
            waitingText.gameObject.SetActive(false);
            foreach (IHudSection section in sections)
                section.Hide();
        }

        /// <summary>
        /// 결과 화면에 들어서면 버튼을 누를 수 있도록 커서 잠금을 푼다.
        /// </summary>
        private void HandleStateTransition(GameState state)
        {
            if (state == previousState)
                return;

            if (state == GameState.Ended)
                GameCursor.SetLocked(false);
            previousState = state;
        }
    }
}
