using System;
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

        /// <summary>이 사람이 탈락시킨 상대 수.</summary>
        public int eliminations;

        /// <summary>함께 붙어 부순 전원 장치 수.</summary>
        public int objectives;

        /// <summary>판 시작부터 탈락(생존자는 판 끝)까지 버틴 시간(초). 도중에 나가 알 수 없으면 음수.</summary>
        public float aliveSeconds;
    }

    /// <summary>판 도중 탈락 소식 하나. 누가 살아 있는지 판이 끝나기 전에 새지 않도록 이름 없이 역할만 담는다.</summary>
    public struct EliminationNotice
    {
        public PlayerRole victimRole;
    }

    /// <summary>
    /// 이번 판의 승패 결과와 참가자별 결과·기록을 동기화하고, 판 도중 탈락 소식을 모두에게 알린다.
    /// </summary>
    public class MatchResult : NetworkBehaviour
    {
        [SerializeField] private GameStateMachine stateMachine;

        [Tooltip("전원 장치를 부순 사람을 기록하려고 듣는다. 비우면 파괴 기록만 빠진다.")]
        [SerializeField] private Objectives.ObjectiveTracker objectiveTracker;

        private readonly SyncVar<MatchWinner> winner = new(MatchWinner.None);
        private readonly SyncVar<WinReason> reason = new(WinReason.None);

        /// <summary>진행 중에 채워 두면 생존 여부가 클라이언트에 미리 새므로, 판이 끝날 때만 채운다.</summary>
        private readonly SyncList<ParticipantResult> publishedParticipants = new();

        /// <summary>생존 판정에 아바타 참조가 필요하지만 동기화할 수는 없어서 서버에만 따로 둔다.</summary>
        private readonly List<ServerParticipant> serverParticipants = new();

        private float matchStartTime;

        /// <summary>판이 끝나기 전에는 비어 있다.</summary>
        public IReadOnlyList<ParticipantResult> Participants => publishedParticipants;

        /// <summary>탈락 알림 화면이 네트워크를 몰라도 되도록 모든 PC에서 탈락 소식을 알린다.</summary>
        public event Action<EliminationNotice> Eliminated;

        /// <summary>기록은 바뀌므로 구조체 대신 참조로 들고 있는 서버 전용 참가자 정보.</summary>
        private sealed class ServerParticipant
        {
            public string name;
            public PlayerRole role;
            public NetworkObject avatar;
            public int eliminations;
            public int objectives;
            public float? eliminatedAt;
        }

        /// <summary>
        /// 지난 판 참가자가 섞이지 않도록 스폰 직전에 서버에서 호출하고, 버틴 시간의 기준을 이때로 둔다.
        /// </summary>
        [Server]
        public void ResetParticipants()
        {
            serverParticipants.Clear();
            publishedParticipants.Clear();
            matchStartTime = Time.time;
        }

        /// <summary>결과 공개 때 아바타가 남아 있는지로 생존을 가리도록 아바타를 함께 기억한다.</summary>
        [Server]
        public void RegisterParticipant(string displayName, PlayerRole role, NetworkObject avatar)
        {
            serverParticipants.Add(new ServerParticipant { name = displayName, role = role, avatar = avatar });
        }

        /// <summary>다시 들어온 사람이 탈락으로 잘못 나오지 않도록 새 아바타로 바꿔 둔다.</summary>
        [Server]
        public void ReplaceAvatar(NetworkObject previous, NetworkObject current)
        {
            foreach (ServerParticipant participant in serverParticipants)
            {
                if (ReferenceEquals(participant.avatar, previous))
                    participant.avatar = current;
            }
        }

        /// <summary>결과 화면 기록을 남기고, 판 흐름을 따라가도록 모두에게 이름 없는 탈락 소식을 보낸다.</summary>
        /// <param name="attacker">환경 피해처럼 공격자가 없으면 null.</param>
        [Server]
        public void RecordElimination(NetworkObject victim, NetworkObject attacker)
        {
            ServerParticipant victimEntry = FindByAvatar(victim);
            ServerParticipant attackerEntry = attacker != null ? FindByAvatar(attacker) : null;
            if (victimEntry == null)
                return;

            victimEntry.eliminatedAt ??= Time.time;
            if (attackerEntry != null && attackerEntry != victimEntry)
                attackerEntry.eliminations++;

            EliminatedObserversRpc(new EliminationNotice { victimRole = victimEntry.role });
        }

        /// <summary>
        /// 새 판이 시작될 때 지난 결과를 지우고 전원 장치 파괴 기록을 남기려고 서버에서 듣는다.
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();
            stateMachine.ServerStateChanged += OnServerStateChanged;
            if (objectiveTracker != null)
                objectiveTracker.ServerGeneratorDestroyed += OnGeneratorDestroyed;
        }

        /// <summary>
        /// 서버가 멈춘 뒤에도 콜백이 남지 않도록 구독을 끊는다.
        /// </summary>
        public override void OnStopServer()
        {
            base.OnStopServer();
            stateMachine.ServerStateChanged -= OnServerStateChanged;
            if (objectiveTracker != null)
                objectiveTracker.ServerGeneratorDestroyed -= OnGeneratorDestroyed;
        }

        /// <summary>
        /// 서버 판정을 거친 소식만 받으므로 그대로 화면에 넘긴다.
        /// </summary>
        [ObserversRpc]
        private void EliminatedObserversRpc(EliminationNotice notice)
        {
            Eliminated?.Invoke(notice);
        }

        /// <summary>함께 부순 사람 모두의 몫으로 센다. 다시 들어온 사람은 아바타 주인으로 찾는다.</summary>
        private void OnGeneratorDestroyed(IReadOnlyList<int> destroyerClientIds)
        {
            foreach (int clientId in destroyerClientIds)
            {
                ServerParticipant participant = serverParticipants.Find(entry => entry.avatar != null && entry.avatar.OwnerId == clientId);
                if (participant != null)
                    participant.objectives++;
            }
        }

        /// <summary>
        /// 날아가는 동안의 대리 물체가 아니라 아바타 자체로 등록했으므로 참조로 찾는다.
        /// </summary>
        private ServerParticipant FindByAvatar(NetworkObject avatar)
        {
            return serverParticipants.Find(entry => ReferenceEquals(entry.avatar, avatar));
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
            foreach (ServerParticipant participant in serverParticipants)
            {
                bool survived = participant.avatar != null && participant.avatar.IsSpawned;
                // 도중에 나간 사람은 언제까지 버텼는지 기록이 없어 알 수 없음으로 둔다.
                float aliveSeconds = survived ? Time.time - matchStartTime
                    : participant.eliminatedAt.HasValue ? participant.eliminatedAt.Value - matchStartTime
                    : -1f;

                publishedParticipants.Add(new ParticipantResult
                {
                    displayName = participant.name,
                    role = participant.role,
                    survived = survived,
                    eliminations = participant.eliminations,
                    objectives = participant.objectives,
                    aliveSeconds = aliveSeconds
                });
            }
        }
    }
}
