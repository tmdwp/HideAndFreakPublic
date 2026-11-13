using FishNet.Connection;
using FishNet.Object;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Rules
{
    /// <summary>모두 씬을 불러온 뒤 판을 시작하고 판이 끝나면 룸으로 돌려보내도록 룸과 게임 판을 잇는다.</summary>
    public class MatchSessionBridge : NetworkBehaviour
    {
        [SerializeField] private GameStateMachine stateMachine;

        [Tooltip("씬을 불러오지 못한 플레이어가 \"로딩 중\" 알림도 보내지 않으면(멈춤·끊김) 이 시간 뒤에 시작한다.")]
        [SerializeField] private float maxWaitForPlayers = 15f;

        [Tooltip("로딩 중 알림을 계속 보내는 느린 플레이어라도 이 시간이 지나면 기다리지 않고 시작한다.")]
        [SerializeField] private float maxLoadingWait = 120f;

        private float waitStartTime;
        private bool isMatchStarted;
        private bool isReturningToRoom;

        /// <summary>
        /// 로딩 대기 시간을 이 씬이 서버에서 시작된 시점부터 재고, 판이 끝나는 것을 알기 위해 상태 변경을 듣는다.
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();
            waitStartTime = Time.time;
            stateMachine.ServerStateChanged += OnServerStateChanged;
        }

        /// <summary>
        /// 서버가 멈춘 뒤 콜백이 남지 않도록 구독을 끊는다.
        /// </summary>
        public override void OnStopServer()
        {
            base.OnStopServer();
            stateMachine.ServerStateChanged -= OnServerStateChanged;
        }

        /// <summary>늦은 사람이 시작을 놓치지 않도록 모두 준비될 때까지 기다리되, 끊긴 사람 때문에 멈추지 않게 상한을 둔다.</summary>
        private void Update()
        {
            if (!this.IsServerReady() || isMatchStarted || GameServices.Room == null)
                return;
            if (stateMachine.CurrentState != GameState.Waiting)
                return;

            bool allLoaded = AllPlayersLoadedScene(out bool anyStillLoading);
            float elapsed = Time.time - waitStartTime;
            // 느리게라도 로딩 중인 플레이어는 최대 maxLoadingWait까지 기다리고, 알림이 끊긴 플레이어는 maxWaitForPlayers 후 포기한다.
            bool timedOut = elapsed >= maxLoadingWait || (elapsed >= maxWaitForPlayers && !anyStillLoading);
            if (!allLoaded && !timedOut)
                return;
            if (!allLoaded)
                Debug.LogWarning($"[MatchSessionBridge] 일부 플레이어가 맵을 불러오지 못한 채 {elapsed:F0}초가 지나 판을 시작합니다.");

            if (stateMachine.TryStartMatch(out string failReason))
                isMatchStarted = true;
            else if (timedOut)
            {
                // 인원이 부족해 시작할 수 없으면 게임 씬에 갇히지 않도록 룸으로 돌아간다.
                Debug.LogWarning($"[MatchSessionBridge] 판을 시작하지 못해 룸으로 돌아갑니다: {failReason}");
                ReturnToRoom();
            }
        }

        /// <summary>
        /// 느리지만 살아 있는 사람과 멈춘 사람을 구분해 기다릴 시간을 달리하려고, 로딩 중 알림을 보내는 사람이 있는지도 함께 알려 준다.
        /// </summary>
        private bool AllPlayersLoadedScene(out bool anyStillLoading)
        {
            anyStillLoading = false;
            bool allLoaded = true;
            foreach (NetworkConnection connection in ServerManager.Clients.Values)
            {
                if (connection.Scenes.Contains(gameObject.scene))
                    continue;

                allLoaded = false;
                if (GameServices.SceneFlow != null && GameServices.SceneFlow.IsClientLoading(connection.ClientId))
                    anyStillLoading = true;
            }
            return allLoaded && ServerManager.Clients.Count > 0;
        }

        /// <summary>판 시작 전 대기와 구분하려고 결과 화면 뒤의 대기만 룸 복귀로 본다.</summary>
        private void OnServerStateChanged(GameState previousState, GameState nextState)
        {
            if (previousState == GameState.Ended && nextState == GameState.Waiting)
                ReturnToRoom();
        }

        /// <summary>
        /// 시간 초과와 판 종료가 겹쳐 씬 전환이 두 번 요청되지 않도록 한 번만 보낸다.
        /// </summary>
        private void ReturnToRoom()
        {
            if (isReturningToRoom)
                return;

            isReturningToRoom = true;
            GameServices.SceneFlow?.ReturnToRoom();
        }
    }
}
