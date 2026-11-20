using GuardVsMannequin.Cameras;
using GuardVsMannequin.Core;
using GuardVsMannequin.Objectives;
using GuardVsMannequin.Player;
using GuardVsMannequin.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>영역들이 따로 상태를 읽지 않도록 게임 정보를 나눠 주고 조준점·커서를 처리하는 HUD 관리자.</summary>
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
        private Graphic crosshairGraphic;

        /// <summary>영역을 추가해도 고치지 않도록 하위 영역을 모두 찾고, 아바타가 바로 스폰될 수 있어 추적을 먼저 시작한다.</summary>
        private void Awake()
        {
            localPlayer.Attach();
            context = new HudContext(stateMachine, stateMachine, playerRegistry, objectiveTracker, matchResult, spectator, localPlayer);

            sections = GetComponentsInChildren<IHudSection>(true);
            foreach (IHudSection section in sections)
                section.Bind(context);

            crosshairGraphic = crosshair.GetComponent<Graphic>();
            ApplyCrosshairColor();
            OptionsStore.OptionsChanged += ApplyCrosshairColor;

            SetAllHidden();
        }

        /// <summary>
        /// 추적기·설정 알림은 정적 이벤트를 구독하므로 끊지 않으면 파괴된 HUD가 계속 불린다.
        /// </summary>
        private void OnDestroy()
        {
            localPlayer.Detach();
            OptionsStore.OptionsChanged -= ApplyCrosshairColor;
        }

        /// <summary>
        /// 배경에 묻히지 않도록 사용자가 고른 조준점 색을 쓴다.
        /// </summary>
        private void ApplyCrosshairColor()
        {
            if (crosshairGraphic != null)
                crosshairGraphic.color = CrosshairSettings.GetColor(OptionsStore.Current);
        }

        /// <summary>영역마다 상태를 따로 읽지 않도록 한 번 읽은 상태를 나눠 주고, 대기 중에는 역할을 지운다.</summary>
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
        /// 네트워크 준비 전에는 읽을 정보가 없어 빈 HUD가 보이지 않도록 한꺼번에 숨긴다.
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
