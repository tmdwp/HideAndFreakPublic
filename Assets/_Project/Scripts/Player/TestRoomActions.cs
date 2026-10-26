using FishNet.Connection;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using GuardVsMannequin.Core;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 테스트 방에서 호스트가 쓰는 게임 중 조작. (서버 전용)
    /// - 가드 ↔ 마네킹: 호스트 아바타를 그 자리에서 다른 역할로 다시 스폰
    /// - 마네킹 ↔ 구울: 사냥 시간을 켜고 끔. 끌 때는 마네킹을 다시 스폰해 사냥 모습(구울·1인칭·시야 제한)을 되돌림
    /// - 게임 중 설정 변경
    /// - 룸으로 돌아가기 (승패가 나지 않으므로 맵을 바꿀 때)
    /// </summary>
    public sealed class TestRoomActions : ITestRoomActions
    {
        private readonly GameStateMachine stateMachine;
        private readonly IAvatarRespawner respawner;
        private readonly ServerManager serverManager;
        private readonly ClientManager clientManager;

        /// <summary>
        /// 상태 머신과 재스폰 기능, 호스트 연결을 찾을 네트워크 관리자를 주입받는다.
        /// </summary>
        public TestRoomActions(GameStateMachine stateMachine, IAvatarRespawner respawner, ServerManager serverManager, ClientManager clientManager)
        {
            this.stateMachine = stateMachine;
            this.respawner = respawner;
            this.serverManager = serverManager;
            this.clientManager = clientManager;
        }

        /// <inheritdoc />
        public GameState CurrentState => stateMachine.CurrentState;

        /// <inheritdoc />
        public GameSettings Settings => stateMachine.Settings;

        /// <inheritdoc />
        public bool CanSwitch => CurrentState == GameState.Playing || CurrentState == GameState.Hunting;

        /// <inheritdoc />
        public void SwapLocalRole()
        {
            if (!CanSwitch || !TryGetHostConnection(out NetworkConnection host))
                return;

            // 아바타가 없으면(죽어서 관전 중) 가드로 다시 시작한다.
            PlayerRole next = respawner.TryGetRole(host, out PlayerRole current) && current == PlayerRole.Guard
                ? PlayerRole.Mannequin
                : PlayerRole.Guard;
            respawner.Respawn(host, next);
        }

        /// <inheritdoc />
        public void ToggleHunt()
        {
            if (CurrentState == GameState.Playing)
            {
                stateMachine.ChangeState(GameState.Hunting);
            }
            else if (CurrentState == GameState.Hunting)
            {
                stateMachine.ChangeState(GameState.Playing);
                // 사냥 모습은 한 번 바뀌면 되돌리지 않으므로 다시 스폰해 원래 마네킹으로 만든다.
                respawner.RespawnRole(PlayerRole.Mannequin);
            }
        }

        /// <inheritdoc />
        public void ApplySettings(GameSettings settings)
        {
            stateMachine.OverrideSettings(settings);
        }

        /// <inheritdoc />
        public void ReturnToRoom()
        {
            // 대기 상태로 바꿔 아바타를 정리한 뒤(PlayerSpawner) 룸 씬으로 보낸다.
            // MatchSessionBridge는 결과 화면(종료) 뒤의 대기만 룸 복귀로 처리하므로 여기서 직접 보낸다.
            stateMachine.ChangeState(GameState.Waiting);
            GameServices.SceneFlow?.ReturnToRoom();
        }

        /// <summary>
        /// 서버 쪽 호스트 연결을 찾는다.
        /// </summary>
        private bool TryGetHostConnection(out NetworkConnection host)
        {
            host = null;
            NetworkConnection local = clientManager.Connection;
            return local != null && local.IsValid && serverManager.Clients.TryGetValue(local.ClientId, out host);
        }
    }
}
