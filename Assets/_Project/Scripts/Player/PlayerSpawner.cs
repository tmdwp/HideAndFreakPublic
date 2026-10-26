using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using GuardVsMannequin.Core;
using GuardVsMannequin.Map;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 준비 단계에 진입하면 역할을 배정하고 역할별 아바타를 스폰한다.
    /// 대기 상태로 돌아가면 스폰한 아바타를 모두 제거한다.
    /// 역할별 프리팹은 인스펙터 목록으로, 역할별 체력은 <see cref="RoleDefinitions"/>로 정한다. (역할을 추가해도 이 코드는 바꾸지 않는다)
    /// </summary>
    public class PlayerSpawner : NetworkBehaviour, IAvatarRespawner
    {
        /// <summary>역할 하나의 아바타 프리팹.</summary>
        [Serializable]
        private class RolePrefab
        {
            public PlayerRole role;
            public NetworkObject prefab;
        }

        [SerializeField] private GameStateMachine stateMachine;
        [SerializeField] private SpawnPointRegistry spawnPoints;

        [Tooltip("역할별 아바타 프리팹.")]
        [SerializeField] private RolePrefab[] rolePrefabs = Array.Empty<RolePrefab>();

        [SerializeField] private MatchResult matchResult;

        private readonly RoleAssigner roleAssigner = new();
        private readonly List<NetworkObject> spawnedAvatars = new();
        private TestRoomActions testRoomActions;

        /// <summary>
        /// 서버에서 상태 변경 이벤트를 구독하고, 테스트 방이면 테스트 조작을 등록한다.
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();
            stateMachine.ServerStateChanged += OnServerStateChanged;

            if (GameServices.Room?.IsTestRoom ?? false)
            {
                testRoomActions = new TestRoomActions(stateMachine, this, ServerManager, ClientManager);
                GameServices.RegisterTestRoom(testRoomActions);
            }
        }

        /// <summary>
        /// 상태 변경 이벤트 구독과 테스트 조작 등록을 해제한다.
        /// </summary>
        public override void OnStopServer()
        {
            base.OnStopServer();
            stateMachine.ServerStateChanged -= OnServerStateChanged;
            GameServices.UnregisterTestRoom(testRoomActions);
            testRoomActions = null;
        }

        /// <inheritdoc />
        public bool TryGetRole(NetworkConnection conn, out PlayerRole role)
        {
            NetworkObject avatar = FindAvatar(conn);
            role = avatar != null ? avatar.GetComponent<PlayerIdentity>().Role : PlayerRole.None;
            return avatar != null;
        }

        /// <inheritdoc />
        [Server]
        public void Respawn(NetworkConnection conn, PlayerRole role)
        {
            NetworkObject current = FindAvatar(conn);
            Pose? pose = null;
            if (current != null)
            {
                pose = new Pose(current.transform.position, current.transform.rotation);
                RemoveAvatar(current);
            }
            SpawnAvatar(conn, role, pose, registerParticipant: false);
        }

        /// <inheritdoc />
        [Server]
        public void RespawnRole(PlayerRole role)
        {
            foreach (NetworkObject avatar in spawnedAvatars.ToArray())
            {
                if (avatar == null || !avatar.IsSpawned || avatar.Owner == null || !avatar.Owner.IsActive)
                    continue;
                if (avatar.GetComponent<PlayerIdentity>().Role == role)
                    Respawn(avatar.Owner, role);
            }
        }

        /// <summary>
        /// 연결이 조종하는 살아 있는 아바타를 찾는다. 없으면 null.
        /// </summary>
        private NetworkObject FindAvatar(NetworkConnection conn)
        {
            return spawnedAvatars.Find(avatar => avatar != null && avatar.IsSpawned && avatar.Owner == conn);
        }

        /// <summary>
        /// 아바타를 목록에서 빼고 제거한다. (사망 처리를 거치지 않음)
        /// </summary>
        private void RemoveAvatar(NetworkObject avatar)
        {
            avatar.GetComponent<PlayerHealth>().ServerDied -= OnAvatarDied;
            spawnedAvatars.Remove(avatar);
            ServerManager.Despawn(avatar);
        }

        /// <summary>
        /// 준비 단계 진입 시 스폰, 대기 상태 복귀 시 제거한다.
        /// </summary>
        private void OnServerStateChanged(GameState prev, GameState next)
        {
            if (next == GameState.Preparation)
                SpawnAll();
            else if (next == GameState.Waiting)
                DespawnAll();
        }

        /// <summary>
        /// 접속 중인 모든 클라이언트에 역할을 배정하고 아바타를 스폰한다.
        /// </summary>
        private void SpawnAll()
        {
            DespawnAll();
            spawnPoints.ResetAssignments();
            matchResult.ResetParticipants();

            List<int> clientIds = new List<int>(ServerManager.Clients.Keys);
            Dictionary<int, PlayerRole> roles = roleAssigner.Assign(clientIds, stateMachine.Settings.guardCount);

            foreach (KeyValuePair<int, PlayerRole> pair in roles)
            {
                if (ServerManager.Clients.TryGetValue(pair.Key, out NetworkConnection conn))
                    SpawnAvatar(conn, pair.Value);
            }
        }

        /// <summary>
        /// 한 플레이어의 아바타를 역할에 맞는 스폰 지점에 생성한다.
        /// </summary>
        private void SpawnAvatar(NetworkConnection conn, PlayerRole role)
        {
            SpawnAvatar(conn, role, null, registerParticipant: true);
        }

        /// <summary>
        /// 한 플레이어의 아바타를 생성한다. 위치를 주면 그 자리에, 아니면 역할에 맞는 스폰 지점에 둔다.
        /// </summary>
        /// <param name="pose">스폰할 위치·방향. null이면 스폰 지점.</param>
        /// <param name="registerParticipant">결과 화면 참가자 목록에 넣을지 여부. (테스트 방 재스폰은 넣지 않음)</param>
        private void SpawnAvatar(NetworkConnection conn, PlayerRole role, Pose? pose, bool registerParticipant)
        {
            NetworkObject prefab = FindPrefab(role);
            if (prefab == null)
            {
                Debug.LogError($"[PlayerSpawner] {role} 역할의 아바타 프리팹이 목록에 없습니다. 인스펙터의 Role Prefabs를 확인하세요.", this);
                return;
            }

            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            if (pose.HasValue)
            {
                position = pose.Value.position;
                // 몸은 바닥과 수평으로 세운다. (마네킹이 벽에 붙어 있던 경우 등)
                rotation = Quaternion.Euler(0f, pose.Value.rotation.eulerAngles.y, 0f);
            }
            else if (spawnPoints.TryGetSpawnPose(role, out Pose spawnPose))
            {
                position = spawnPose.position;
                rotation = spawnPose.rotation;
            }
            else
            {
                Debug.LogWarning($"[PlayerSpawner] {role} 스폰 지점이 없어 원점에 스폰합니다.");
            }

            NetworkObject avatar = Instantiate(prefab, position, rotation);
            string displayName = GameServices.Room?.GetDisplayName(conn.ClientId) ?? $"Player {conn.ClientId}";
            avatar.GetComponent<PlayerIdentity>().ServerInitialize(role, displayName);

            PlayerHealth health = avatar.GetComponent<PlayerHealth>();
            health.ServerInitialize(stateMachine.Settings.GetStats(role).MaxHealth);
            health.ServerDied += OnAvatarDied;

            ServerManager.Spawn(avatar, conn);
            if (registerParticipant)
                matchResult.RegisterParticipant(displayName, role, avatar);
            spawnedAvatars.Add(avatar);
        }

        /// <summary>
        /// 사망한 아바타를 제거한다. 로컬 클라이언트는 아바타가 사라지면 씬 카메라로 전환된다.
        /// (Phase 4에서 관전 모드로 대체)
        /// </summary>
        private void OnAvatarDied(PlayerHealth victim, NetworkObject attacker)
        {
            victim.ServerDied -= OnAvatarDied;

            NetworkObject avatar = victim.NetworkObject;
            spawnedAvatars.Remove(avatar);
            if (avatar.IsSpawned)
                ServerManager.Despawn(avatar);
        }

        /// <summary>
        /// 이번 판에 스폰한 아바타를 모두 제거한다.
        /// </summary>
        private void DespawnAll()
        {
            foreach (NetworkObject avatar in spawnedAvatars)
            {
                // 접속 종료로 이미 제거된 아바타는 건너뛴다.
                if (avatar != null && avatar.IsSpawned)
                    ServerManager.Despawn(avatar);
            }
            spawnedAvatars.Clear();
        }

        /// <summary>
        /// 역할의 아바타 프리팹을 찾는다. 없으면 null.
        /// </summary>
        private NetworkObject FindPrefab(PlayerRole role)
        {
            RolePrefab entry = Array.Find(rolePrefabs, candidate => candidate.role == role);
            return entry != null ? entry.prefab : null;
        }
    }
}
