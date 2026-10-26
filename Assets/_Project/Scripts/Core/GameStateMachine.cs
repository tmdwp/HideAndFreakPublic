using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using GuardVsMannequin.Core.States;
using GuardVsMannequin.Localization;
using UnityEngine;

namespace GuardVsMannequin.Core
{
    /// <summary>
    /// 한 판의 진행 상태를 서버 권한으로 관리하고 클라이언트에 동기화한다.
    /// 상태별 규칙은 <see cref="IGameStateHandler"/>에 위임한다.
    /// 다른 코드에는 읽기(<see cref="IGameStateProvider"/>)와 판 조작(<see cref="IMatchControl"/>)으로 나눠 노출한다.
    /// </summary>
    public class GameStateMachine : NetworkBehaviour, IGameStateContext, IGameStateProvider, IMatchControl
    {
        [Tooltip("IMatchRules를 구현한 컴포넌트. 비어 있으면 시간만으로 단계를 넘긴다.")]
        [SerializeField] private MonoBehaviour matchRulesComponent;

        private readonly SyncVar<GameState> currentState = new(GameState.Waiting);
        private readonly SyncVar<GameSettings> currentSettings = new();
        private readonly SyncTimer stateTimer = new();

        private readonly Dictionary<GameState, IGameStateHandler> handlers = new();

        /// <summary>서버에서 상태가 바뀐 직후 호출된다. (이전 상태, 새 상태)</summary>
        public event Action<GameState, GameState> ServerStateChanged;

        /// <summary>클라이언트에서 상태 변경을 수신했을 때 호출된다. (이전 상태, 새 상태)</summary>
        public event Action<GameState, GameState> ClientStateChanged;

        /// <summary>현재 상태.</summary>
        public GameState CurrentState => currentState.Value;

        /// <summary>이번 판에 확정된 게임 설정.</summary>
        public GameSettings Settings => currentSettings.Value;

        /// <summary>현재 상태 타이머의 남은 시간(초).</summary>
        public float RemainingTime => stateTimer.Remaining;

        /// <summary>
        /// 기본 상태 핸들러를 등록하고 동기화 콜백을 연결한다.
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
        /// 동기화 콜백과 서비스 등록을 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            currentState.OnChange -= OnCurrentStateChanged;
            GameServices.UnregisterState(this);
        }

        /// <summary>
        /// 서버 시작 시 룸 설정(없으면 기본 설정 파일)으로 초기화하고 대기 상태에 진입한다.
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();
            currentSettings.Value = GameServices.Room?.Settings ?? DefaultGameSettings.Load();
            handlers[GameState.Waiting].Enter(this);
        }

        /// <summary>
        /// 상태 핸들러를 등록한다. 같은 상태의 핸들러가 있으면 교체한다.
        /// 이후 Phase에서 규칙을 확장할 때 사용한다.
        /// </summary>
        public void RegisterHandler(IGameStateHandler handler)
        {
            handlers[handler.State] = handler;
        }

        /// <summary>
        /// 타이머를 진행하고, 서버에서는 현재 상태 핸들러를 갱신한다.
        /// </summary>
        private void Update()
        {
            stateTimer.Update(Time.deltaTime);

            if (!this.IsServerReady())
                return;

            GameState? next = handlers[currentState.Value].Tick(this, Time.deltaTime);
            if (next.HasValue)
                ChangeState(next.Value);
        }

        /// <summary>
        /// 대기 상태에서 게임 시작을 시도한다. 현재 참가 인원으로 설정을 보정한 뒤 준비 단계로 넘어간다.
        /// </summary>
        /// <param name="failReason">실패 시 사유.</param>
        /// <returns>시작에 성공했는지 여부.</returns>
        [Server]
        public bool TryStartMatch(out string failReason)
        {
            if (currentState.Value != GameState.Waiting)
            {
                failReason = Loc.Get("room.reason.inGame");
                return false;
            }

            int playerCount = ServerManager.Clients.Count;
            // 테스트 방은 혼자서도 시작한다.
            bool isTestRoom = GameServices.Room?.IsTestRoom ?? false;
            if (!isTestRoom && !GameSettingsValidator.CanStart(playerCount))
            {
                failReason = Loc.Format("room.reason.playerCount", GameConstants.MinPlayers, GameConstants.MaxPlayers, playerCount);
                return false;
            }

            // 룸에서 호스트가 정한 설정을 우선 사용하고, 룸 없이 실행하면 기본 설정 파일을 쓴다.
            GameSettings baseSettings = GameServices.Room?.Settings ?? DefaultGameSettings.Load();
            currentSettings.Value = GameSettingsValidator.Validate(baseSettings, playerCount);

            ChangeState(GameState.Preparation);
            failReason = null;
            return true;
        }

        /// <summary>
        /// 진행 중인 판의 설정을 바꾼다. (테스트 방의 게임 중 설정 변경용, 현재 인원 기준으로 보정)
        /// 이동 속도·공격 범위·쿨타임처럼 매번 설정을 읽는 값은 바로, 체력처럼 스폰 때 정하는 값은 다음 스폰부터 반영된다.
        /// </summary>
        [Server]
        public void OverrideSettings(GameSettings newSettings)
        {
            currentSettings.Value = GameSettingsValidator.Validate(newSettings, Mathf.Max(1, ServerManager.Clients.Count));
        }

        /// <summary>
        /// 상태를 강제로 전환한다. 승패 판정 등 외부 시스템이 사용한다.
        /// </summary>
        [Server]
        public void ChangeState(GameState next)
        {
            GameState prev = currentState.Value;
            if (prev == next)
                return;

            handlers[prev].Exit(this);
            currentState.Value = next;
            handlers[next].Enter(this);

            ServerStateChanged?.Invoke(prev, next);
        }

        /// <inheritdoc />
        public bool CanControlMatch => this.IsServerReady();

        /// <inheritdoc />
        public void ReturnToRoom()
        {
            // 대기 상태로 바뀌면 MatchSessionBridge가 모두를 룸으로 보낸다.
            if (CanControlMatch)
                ChangeState(GameState.Waiting);
        }

        /// <summary>
        /// 상태 타이머를 시작한다.
        /// </summary>
        [Server]
        public void StartTimer(float seconds)
        {
            stateTimer.StartTimer(seconds);
        }

        /// <summary>
        /// 상태 타이머를 멈춘다.
        /// </summary>
        [Server]
        public void StopTimer()
        {
            if (stateTimer.Remaining > 0f)
                stateTimer.StopTimer();
        }

        /// <summary>
        /// 상태 SyncVar 변경을 클라이언트 이벤트로 전달한다.
        /// </summary>
        private void OnCurrentStateChanged(GameState prev, GameState next, bool asServer)
        {
            if (!asServer)
                ClientStateChanged?.Invoke(prev, next);
        }
    }
}
