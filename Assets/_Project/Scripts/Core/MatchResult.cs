using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace GuardVsMannequin.Core
{
    /// <summary>승리한 진영.</summary>
    public enum MatchWinner : byte
    {
        None = 0,
        Guard = 1,
        Mannequin = 2
    }

    /// <summary>승패가 결정된 이유.</summary>
    public enum WinReason : byte
    {
        None = 0,

        /// <summary>마네킹 전멸로 가드 승리.</summary>
        AllMannequinsEliminated = 1,

        /// <summary>제한 시간 동안 목표 미달성으로 가드 승리.</summary>
        ObjectiveFailed = 2,

        /// <summary>사냥 시간 종료까지 가드 생존으로 가드 승리.</summary>
        GuardsSurvived = 3,

        /// <summary>사냥 시간 중 가드 전멸로 마네킹 승리.</summary>
        AllGuardsEliminated = 4
    }

    /// <summary>참가자 한 명의 판 결과.</summary>
    public struct ParticipantResult
    {
        public string displayName;
        public PlayerRole role;
        public bool survived;
    }

    /// <summary>
    /// 이번 판의 승패 결과와 참가자별 결과를 동기화한다.
    /// </summary>
    public class MatchResult : NetworkBehaviour
    {
        [SerializeField] private GameStateMachine stateMachine;

        private readonly SyncVar<MatchWinner> winner = new(MatchWinner.None);
        private readonly SyncVar<WinReason> reason = new(WinReason.None);

        /// <summary>진행 중에 채워 두면 누가 죽었는지 클라이언트에 새므로, 판이 끝날 때만 채운다.</summary>
        private readonly SyncList<ParticipantResult> publishedParticipants = new();

        /// <summary>생존 판정에 아바타 참조가 필요하지만 동기화할 수는 없어서 서버에만 따로 둔다.</summary>
        private readonly List<(string name, PlayerRole role, NetworkObject avatar)> serverParticipants = new();

        /// <summary>판이 끝나기 전에는 비어 있다.</summary>
        public IReadOnlyList<ParticipantResult> Participants => publishedParticipants;

        /// <summary>
        /// 지난 판 참가자가 섞이지 않도록 스폰 직전에 서버에서 호출한다.
        /// </summary>
        [Server]
        public void ResetParticipants()
        {
            serverParticipants.Clear();
            publishedParticipants.Clear();
        }

        /// <summary>결과 공개 때 아바타가 남아 있는지로 생존을 가리도록 아바타를 함께 기억한다.</summary>
        [Server]
        public void RegisterParticipant(string displayName, PlayerRole role, NetworkObject avatar)
        {
            serverParticipants.Add((displayName, role, avatar));
        }

        /// <summary>다시 들어온 사람이 탈락으로 잘못 나오지 않도록 새 아바타로 바꿔 둔다.</summary>
        [Server]
        public void ReplaceAvatar(NetworkObject previous, NetworkObject current)
        {
            for (int i = 0; i < serverParticipants.Count; i++)
            {
                if (ReferenceEquals(serverParticipants[i].avatar, previous))
                    serverParticipants[i] = (serverParticipants[i].name, serverParticipants[i].role, current);
            }
        }

        /// <summary>
        /// 새 판이 시작될 때 지난 결과를 지우려고 서버에서 상태 변경을 듣는다.
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();
            stateMachine.ServerStateChanged += OnServerStateChanged;
        }

        /// <summary>
        /// 서버가 멈춘 뒤에도 콜백이 남지 않도록 구독을 끊는다.
        /// </summary>
        public override void OnStopServer()
        {
            base.OnStopServer();
            stateMachine.ServerStateChanged -= OnServerStateChanged;
        }

        /// <summary>
        /// 준비 단계에 지난 판 결과가 남아 있으면 HUD가 결과를 다시 띄우므로 지운다.
        /// </summary>
        private void OnServerStateChanged(GameState previousState, GameState nextState)
        {
            if (nextState == GameState.Preparation)
                Clear();
        }

        /// <summary>결정 전에는 None.</summary>
        public MatchWinner Winner => winner.Value;

        public WinReason Reason => reason.Value;

        /// <summary>승자가 정해지는 순간에만 참가자 결과를 공개하도록 기록과 공개를 한곳에서 처리한다.</summary>
        [Server]
        public void SetResult(MatchWinner newWinner, WinReason newReason)
        {
            winner.Value = newWinner;
            reason.Value = newReason;

            if (newWinner != MatchWinner.None)
                PublishParticipants();
        }

        /// <summary>새 판에 이전 승패가 보이지 않게 지우되, 참가자 등록은 스폰 쪽이 관리하므로 남긴다.</summary>
        [Server]
        public void Clear()
        {
            winner.Value = MatchWinner.None;
            reason.Value = WinReason.None;
            publishedParticipants.Clear();
        }

        /// <summary>
        /// 죽거나 나간 참가자는 아바타가 이미 제거됐으므로, 아바타가 남아 있는지만으로 생존을 확정할 수 있다.
        /// </summary>
        private void PublishParticipants()
        {
            publishedParticipants.Clear();
            foreach ((string name, PlayerRole role, NetworkObject avatar) in serverParticipants)
            {
                publishedParticipants.Add(new ParticipantResult
                {
                    displayName = name,
                    role = role,
                    survived = avatar != null && avatar.IsSpawned
                });
            }
        }
    }
}
