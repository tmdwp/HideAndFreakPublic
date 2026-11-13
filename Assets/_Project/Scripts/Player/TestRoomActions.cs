using FishNet.Connection;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using GuardVsMannequin.Core;

namespace GuardVsMannequin.Player
{
    /// <summary>테스트 방 호스트가 혼자 기능을 확인하도록 게임 중 역할·사냥 시간을 바꾼다.</summary>
    public sealed class TestRoomActions : ITestRoomActions
    {
        private readonly GameStateMachine stateMachine;
        private readonly IAvatarRespawner respawner;
        private readonly ServerManager serverManager;
        private readonly ClientManager clientManager;

        /// <summary>일반 클래스라 씬 참조를 직접 찾을 수 없어 필요한 것을 스포너에게서 받는다.</summary>
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

            // 죽어서 관전 중이면 바꿀 기준이 없으므로 가드로 다시 시작한다.
            PlayerRole nextRole = respawner.TryGetRole(host, out PlayerRole currentRole) && currentRole == PlayerRole.Guard
                ? PlayerRole.Mannequin
                : PlayerRole.Guard;
            respawner.Respawn(host, nextRole);
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
            // 결과 화면 뒤의 대기만 룸 복귀로 처리되므로 아바타를 정리한 뒤 여기서 직접 룸 씬으로 보낸다.
            stateMachine.ChangeState(GameState.Waiting);
            GameServices.SceneFlow?.ReturnToRoom();
        }

        /// <summary>
        /// 스폰은 서버 쪽 연결 객체로 해야 하는데 호스트가 아는 건 클라이언트 쪽 연결이라, 같은 ID로 서버 쪽 연결을 찾는다.
        /// </summary>
        private bool TryGetHostConnection(out NetworkConnection host)
        {
            host = null;
            NetworkConnection localConnection = clientManager.Connection;
            return localConnection != null && localConnection.IsValid && serverManager.Clients.TryGetValue(localConnection.ClientId, out host);
        }
    }
}
