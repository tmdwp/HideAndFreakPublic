using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using GuardVsMannequin.Core.States;
using GuardVsMannequin.Localization;
using UnityEngine;

namespace GuardVsMannequin.Core
{
    /// <summary>상태별 규칙을 핸들러에 맡기며 판 진행 상태를 서버 권한으로 관리하고 동기화한다.</summary>
    public class GameStateMachine : NetworkBehaviour, IGameStateContext, IGameStateProvider, IMatchControl
    {
        [Tooltip("IMatchRules를 구현한 컴포넌트. 비어 있으면 시간만으로 단계를 넘긴다.")]
        [SerializeField] private MonoBehaviour matchRulesComponent;

        private readonly SyncVar<GameState> currentState = new(GameState.Waiting);
        private readonly SyncVar<GameSettings> currentSettings = new();
        private readonly SyncTimer stateTimer = new();

        private readonly Dictionary<GameState, IGameStateHandler> handlers = new();

        /// <summary>서버 시스템이 단계 전환에 맞춰 움직이도록 알린다.</summary>
        public event Action<GameState, GameState> ServerStateChanged;

        /// <summary>클라이언트 UI·연출이 단계 전환에 맞춰 바뀌도록 알린다.</summary>
        public event Action<GameState, GameState> ClientStateChanged;

        public GameState CurrentState => currentState.Value;

        /// <summary>룸 설정을 시작 인원으로 보정한 값이라 룸 쪽 원본과 다를 수 있다.</summary>
        public GameSettings Settings => currentSettings.Value;

        /// <summary>단위는 초.</summary>
        public float RemainingTime => stateTimer.Remaining;

        /// <summary>
        /// 다른 컴포넌트가 OnStart 단계에서 바로 상태를 읽을 수 있도록, 네트워크 시작 전에 핸들러와 서비스 등록을 끝내 둔다.
        /// </summary>
        private void Awake()
        {
            IMatchRules rules = matchRulesComponent as IMatchRules ?? new TimeOnlyMatchRules();

            RegisterHandler(new WaitingStateHandler());
            RegisterHandler(new PreparationStateHandler());
            RegisterHandler(new PlayingStateHandler(rules));
            RegisterHandler(new HuntingStateHandler(rules));
            RegisterHandler(new EndedStateHandler());

            currentState.OnChange += OnCurrentStateChanged;
            GameServices.RegisterState(this);
        }

        /// <summary>
        /// 씬이 바뀐 뒤 파괴된 상태 기계를 다른 코드가 계속 참조하지 않도록 등록을 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            currentState.OnChange -= OnCurrentStateChanged;
            GameServices.UnregisterState(this);
        }

        /// <summary>
        /// 룸 없이 씬만 열어 테스트할 때도 동작하도록, 룸 설정이 없으면 기본 설정 파일로 시작한다.
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();
            currentSettings.Value = GameServices.Room?.Settings ?? DefaultGameSettings.Load();
            handlers[GameState.Waiting].Enter(this);
        }

        /// <summary>상태 기계를 고치지 않고 핸들러만 갈아 끼울 수 있게 한다.</summary>
        public void RegisterHandler(IGameStateHandler handler)
        {
            handlers[handler.State] = handler;
        }

        /// <summary>
        /// 남은 시간 표시는 클라이언트도 필요해서 타이머는 모두 돌리고, 전환 판단은 서버만 한다.
        /// </summary>
        private void Update()
        {
            stateTimer.Update(Time.deltaTime);

            if (!this.IsServerReady())
                return;

            GameState? nextState = handlers[currentState.Value].Tick(this, Time.deltaTime);
            if (nextState.HasValue)
                ChangeState(nextState.Value);
        }

        /// <summary>
        /// 룸에 있던 사람이 나갔을 수도 있으므로, 시작하는 순간의 인원으로 설정을 다시 보정한 뒤 준비 단계로 넘어간다.
        /// </summary>
        /// <param name="failReason">호스트에게 왜 시작하지 못했는지 보여 주기 위한 문구.</param>
        [Server]
        public bool TryStartMatch(out string failReason)
        {
            if (currentState.Value != GameState.Waiting)
            {
                failReason = Loc.Get("room.reason.inGame");
                return false;
            }

            int playerCount = ServerManager.Clients.Count;
            // 테스트 방은 혼자 기능을 확인하려는 용도라 인원 제한을 두지 않는다.
            bool isTestRoom = GameServices.Room?.IsTestRoom ?? false;
            if (!isTestRoom && !GameSettingsValidator.CanStart(playerCount))
            {
                failReason = Loc.Format("room.reason.playerCount", GameConstants.MinPlayers, GameConstants.MaxPlayers, playerCount);
                return false;
            }

            // 룸 없이 씬만 열어 테스트할 때도 시작할 수 있도록 기본 설정 파일로 물러난다.
            GameSettings baseSettings = GameServices.Room?.Settings ?? DefaultGameSettings.Load();
            currentSettings.Value = GameSettingsValidator.Validate(baseSettings, playerCount);

            ChangeState(GameState.Preparation);
            failReason = null;
            return true;
        }

        /// <summary>테스트 방에서 판을 다시 시작하지 않고 수치를 바로 바꿔 보도록 설정을 교체한다.</summary>
        [Server]
        public void OverrideSettings(GameSettings newSettings)
        {
            currentSettings.Value = GameSettingsValidator.Validate(newSettings, Mathf.Max(1, ServerManager.Clients.Count));
        }

        /// <summary>
        /// 승패 판정 같은 외부 시스템이 타이머를 기다리지 않고 단계를 끝낼 수 있게 연다.
        /// </summary>
        [Server]
        public void ChangeState(GameState nextState)
        {
            GameState previousState = currentState.Value;
            if (previousState == nextState)
                return;

            handlers[previousState].Exit(this);
            currentState.Value = nextState;
            handlers[nextState].Enter(this);

            ServerStateChanged?.Invoke(previousState, nextState);
        }

        /// <inheritdoc />
        public bool CanControlMatch => this.IsServerReady();

        /// <inheritdoc />
        public void ReturnToRoom()
        {
            // 씬 전환은 MatchSessionBridge가 대기 상태를 보고 처리하므로 여기서는 상태만 바꾼다.
            if (CanControlMatch)
                ChangeState(GameState.Waiting);
        }

        /// <summary>
        /// 상태 핸들러가 SyncTimer를 직접 만지지 않도록 컨텍스트를 통해 연다.
        /// </summary>
        [Server]
        public void StartTimer(float seconds)
        {
            stateTimer.StartTimer(seconds);
        }

        /// <summary>
        /// 이미 멈춘 타이머를 다시 멈추면 불필요한 동기화가 나가므로 돌고 있을 때만 멈춘다.
        /// </summary>
        [Server]
        public void StopTimer()
        {
            if (stateTimer.Remaining > 0f)
                stateTimer.StopTimer();
        }

        /// <summary>
        /// 서버 쪽은 ChangeState에서 이미 알렸으므로, 클라이언트에서 받은 변경만 이벤트로 넘긴다.
        /// </summary>
        private void OnCurrentStateChanged(GameState previousState, GameState nextState, bool asServer)
        {
            if (!asServer)
                ClientStateChanged?.Invoke(previousState, nextState);
        }
    }
}
