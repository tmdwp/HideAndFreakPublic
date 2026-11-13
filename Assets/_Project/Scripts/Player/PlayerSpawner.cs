using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using GuardVsMannequin.Core;
using GuardVsMannequin.Map;
using GuardVsMannequin.Network;
using GuardVsMannequin.Player.Mannequin;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>준비 단계에 역할을 배정해 아바타를 스폰하고, 끊긴 사람은 상태를 저장해 돌아오면 되살린다.</summary>
    public class PlayerSpawner : NetworkBehaviour, IAvatarRespawner
    {
        /// <summary>역할을 추가해도 코드를 고치지 않도록 프리팹을 인스펙터 목록으로 받는다.</summary>
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

        // 느린 PC도 돌아오도록 판 시작 로딩 대기와 같게 둔 재접속 맵 로딩 시간(초).
        private const float RejoinLoadSeconds = 120f;

        private readonly RoleAssigner roleAssigner = new();
        // 결과 기록을 새 아바타로 옮기려고 기억하는 끊긴 사람의 이전 아바타.
        private readonly Dictionary<string, NetworkObject> lostAvatars = new();
        private readonly List<NetworkObject> spawnedAvatars = new();
        private TestRoomActions testRoomActions;

        /// <summary>스폰은 서버만 하므로 서버에서만 이벤트를 듣고, 테스트 방이면 테스트 조작을 등록한다.</summary>
        public override void OnStartServer()
        {
            base.OnStartServer();
            stateMachine.ServerStateChanged += OnServerStateChanged;
            ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;

            if (GameServices.Room?.IsTestRoom ?? false)
            {
                testRoomActions = new TestRoomActions(stateMachine, this, ServerManager, ClientManager);
                GameServices.RegisterTestRoom(testRoomActions);
            }
        }

        /// <summary>
        /// 다음 씬까지 콜백과 테스트 조작이 남지 않도록 모두 해제하고, 지난 판의 끊긴 아바타 기록도 비운다.
        /// </summary>
        public override void OnStopServer()
        {
            base.OnStopServer();
            stateMachine.ServerStateChanged -= OnServerStateChanged;
            ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
            GameServices.UnregisterTestRoom(testRoomActions);
            testRoomActions = null;
            lostAvatars.Clear();
        }

        /// <summary>돌아오지 않는 사람을 계속 살아 있는 것으로 세면 판이 끝나지 않으므로, 유예 시간이 지난 자리를 없앤다.</summary>
        private void Update()
        {
            if (!IsServerStarted || !IsMatchActive)
                return;

            RoomMembership membership = SessionServices.Session?.Membership;
            if (membership == null)
                return;

            foreach (string key in membership.RemoveExpired(Time.unscaledTime, GameTuning.RejoinGraceSeconds, RejoinLoadSeconds))
                lostAvatars.Remove(key);
        }

        /// <summary>끊겨도 돌아올 수 있는 단계인지를 여러 곳에서 같은 기준으로 판단하려고 둔다.</summary>
        private bool IsMatchActive => stateMachine.CurrentState is GameState.Preparation or GameState.Playing or GameState.Hunting;

        /// <summary>잠깐 끊긴 사람이 바로 탈락하지 않도록 재접속 대기 자리를 만들되, 스스로 나갔거나 추방됐으면 만들지 않는다.</summary>
        private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Stopped || !IsMatchActive || (GameServices.Room?.IsTestRoom ?? false))
                return;

            RoomMembership membership = SessionServices.Session?.Membership;
            if (membership == null || !membership.TryGetKey(connection.ClientId, out string key))
                return;

            // 맵을 불러오다 다시 끊긴 경우: 저장한 상태를 지키려고 자리는 그대로 두고 차지만 푼다.
            if (membership.HasSlot(key))
            {
                membership.ReleaseClaim(key);
                return;
            }
            if (!membership.CanHoldSlot(key))
                return;

            NetworkObject avatar = FindAvatar(connection);
            if (avatar != null)
                lostAvatars[key] = avatar;
            membership.AddSlot(CreateSlot(key, connection, avatar));
        }

        /// <summary>아바타가 곧 제거되므로 다시 스폰하는 데 필요한 값만 복사해 둔다.</summary>
        private RejoinSlot CreateSlot(string key, NetworkConnection connection, NetworkObject avatar)
        {
            var slot = new RejoinSlot { key = key, disconnectTime = Time.unscaledTime, isAlive = avatar != null };
            if (avatar == null)
                return slot;

            PlayerIdentity identity = avatar.GetComponent<PlayerIdentity>();
            PlayerHealth health = avatar.GetComponent<PlayerHealth>();
            slot.role = identity.Role;
            slot.avatar = new RejoinAvatarState
            {
                displayName = identity.DisplayName,
                currentHealth = health.CurrentHealth,
                maxHealth = health.MaxHealth,
                position = avatar.transform.position,
                rotation = avatar.transform.rotation,
                propIndex = avatar.TryGetComponent(out MannequinTransformer transformer) ? transformer.CurrentPropIndex : TransformableProp.None,
                wasHunting = stateMachine.CurrentState == GameState.Hunting
            };
            return slot;
        }

        /// <summary>
        /// 맵을 다 불러오기 전에 스폰하면 바닥이 없어 떨어지므로, 로딩이 끝난 뒤에 저장한 상태로 다시 스폰한다.
        /// </summary>
        private void OnClientLoadedStartScenes(NetworkConnection connection, bool asServer)
        {
            RoomMembership membership = SessionServices.Session?.Membership;
            if (!asServer || membership == null || !membership.TryTakeClaimed(connection.ClientId, out RejoinSlot slot))
                return;

            lostAvatars.TryGetValue(slot.key, out NetworkObject previousAvatar);
            lostAvatars.Remove(slot.key);
            if (slot.isAlive && IsMatchActive)
                SpawnRejoined(connection, slot, previousAvatar);
        }

        /// <summary>
        /// 결과 화면이 돌아온 사람을 탈락으로 표시하지 않도록 참가자 기록을 새 아바타로 옮긴다.
        /// </summary>
        private void SpawnRejoined(NetworkConnection connection, RejoinSlot slot, NetworkObject previousAvatar)
        {
            var pose = new Pose(slot.avatar.position, slot.avatar.rotation);
            NetworkObject avatar = SpawnAvatar(connection, slot.role, pose, registerParticipant: false, slot.avatar);
            if (avatar != null)
                matchResult.ReplaceAvatar(previousAvatar, avatar);
        }

        /// <inheritdoc />
        public bool TryGetRole(NetworkConnection connection, out PlayerRole role)
        {
            NetworkObject avatar = FindAvatar(connection);
            role = avatar != null ? avatar.GetComponent<PlayerIdentity>().Role : PlayerRole.None;
            return avatar != null;
        }

        /// <inheritdoc />
        [Server]
        public void Respawn(NetworkConnection connection, PlayerRole role)
        {
            NetworkObject currentAvatar = FindAvatar(connection);
            Pose? pose = null;
            if (currentAvatar != null)
            {
                pose = new Pose(currentAvatar.transform.position, currentAvatar.transform.rotation);
                RemoveAvatar(currentAvatar);
            }
            SpawnAvatar(connection, role, pose, registerParticipant: false, null);
        }

        /// <inheritdoc />
        [Server]
        public void RespawnRole(PlayerRole role)
        {
            // 반복 중에 목록이 바뀌므로 복사본을 돈다.
            foreach (NetworkObject avatar in spawnedAvatars.ToArray())
            {
                if (avatar == null || !avatar.IsSpawned || avatar.Owner == null || !avatar.Owner.IsActive)
                    continue;
                if (avatar.GetComponent<PlayerIdentity>().Role == role)
                    Respawn(avatar.Owner, role);
            }
        }

        /// <summary>제거된 아바타가 목록에 남을 수 있어 살아 있는 것만 찾는다.</summary>
        private NetworkObject FindAvatar(NetworkConnection connection)
        {
            return spawnedAvatars.Find(avatar => avatar != null && avatar.IsSpawned && avatar.Owner == connection);
        }

        /// <summary>
        /// 다시 스폰하려고 지울 때 사망으로 처리되면 탈락으로 기록되므로, 사망 콜백을 먼저 끊고 제거한다.
        /// </summary>
        private void RemoveAvatar(NetworkObject avatar)
        {
            avatar.GetComponent<PlayerHealth>().ServerDied -= OnAvatarDied;
            spawnedAvatars.Remove(avatar);
            ServerManager.Despawn(avatar);
        }

        /// <summary>
        /// 아바타 수명을 판 단계에 맞추려고, 준비 단계에서 스폰하고 대기 상태로 돌아가면 제거한다.
        /// </summary>
        private void OnServerStateChanged(GameState previousState, GameState nextState)
        {
            if (nextState == GameState.Preparation)
                SpawnAll();
            else if (nextState == GameState.Waiting)
                DespawnAll();

            // 판이 끝나면 돌아올 곳이 없으므로 대기 자리를 남겨 두지 않는다.
            if (nextState == GameState.Ended || nextState == GameState.Waiting)
            {
                SessionServices.Session?.Membership.ClearMatchState();
                lostAvatars.Clear();
            }
        }

        /// <summary>
        /// 지난 판 아바타·스폰 지점 사용 기록이 섞이지 않도록 먼저 비운 뒤, 지금 접속한 사람 모두에게 역할을 나눠 스폰한다.
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
                if (ServerManager.Clients.TryGetValue(pair.Key, out NetworkConnection connection))
                    SpawnAvatar(connection, pair.Value);
            }
        }

        /// <summary>
        /// 판 시작 스폰은 항상 스폰 지점·참가자 등록을 쓰므로 그 조합을 짧게 부르도록 둔다.
        /// </summary>
        private void SpawnAvatar(NetworkConnection connection, PlayerRole role)
        {
            SpawnAvatar(connection, role, null, registerParticipant: true, null);
        }

        /// <summary>
        /// 판 시작·재접속·테스트 방 전환이 모두 같은 스폰 과정을 거치도록 한곳에 모은다.
        /// </summary>
        /// <param name="pose">null이면 역할에 맞는 스폰 지점을 쓴다.</param>
        /// <param name="registerParticipant">테스트 방 재스폰처럼 이미 등록된 사람이면 결과 목록에 두 번 들어가지 않게 false.</param>
        /// <param name="restored">돌아온 사람의 저장된 상태.</param>
        /// <returns>프리팹이 없으면 null.</returns>
        private NetworkObject SpawnAvatar(NetworkConnection connection, PlayerRole role, Pose? pose, bool registerParticipant, RejoinAvatarState? restored)
        {
            NetworkObject prefab = FindPrefab(role);
            if (prefab == null)
            {
                Debug.LogError($"[PlayerSpawner] {role} 역할의 아바타 프리팹이 목록에 없습니다. 인스펙터의 Role Prefabs를 확인하세요.", this);
                return null;
            }

            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            if (pose.HasValue)
            {
                position = pose.Value.position;
                // 마네킹이 벽에 붙어 있던 자리에서 다시 스폰해도 몸이 기울지 않도록 좌우 방향만 남긴다.
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
            // 돌아온 사람은 이름을 아직 다시 알리지 않았으므로 저장한 이름을 쓴다.
            string displayName = restored?.displayName ?? GameServices.Room?.GetDisplayName(connection.ClientId) ?? $"Player {connection.ClientId}";
            avatar.GetComponent<PlayerIdentity>().ServerInitialize(role, displayName, restored.HasValue);

            PlayerHealth health = avatar.GetComponent<PlayerHealth>();
            if (restored.HasValue)
                RestoreState(avatar, health, restored.Value);
            else
                health.ServerInitialize(stateMachine.Settings.GetStats(role).MaxHealth);
            health.ServerDied += OnAvatarDied;

            ServerManager.Spawn(avatar, connection);
            if (registerParticipant)
                matchResult.RegisterParticipant(displayName, role, avatar);
            spawnedAvatars.Add(avatar);
            return avatar;
        }

        /// <summary>스폰 데이터에 실려 가도록 스폰 직전에 저장한 상태를 넣고, 사냥 중 끊겼으면 사냥 준비 연출을 건너뛴다.</summary>
        private static void RestoreState(NetworkObject avatar, PlayerHealth health, RejoinAvatarState state)
        {
            health.ServerRestore(state.currentHealth, state.maxHealth);
            if (avatar.TryGetComponent(out MannequinTransformer transformer))
                transformer.ServerRestoreDisguise(state.propIndex);
            if (state.wasHunting && avatar.TryGetComponent(out MannequinHuntingMode huntingMode))
                huntingMode.ServerSkipHuntSetup();
        }

        /// <summary>
        /// 아바타가 사라지는 것으로 생존 수와 관전 전환이 함께 처리되므로, 사망하면 바로 제거한다.
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
        /// 다음 판에 이전 아바타가 남지 않도록 이번 판에 스폰한 것을 모두 지운다.
        /// </summary>
        private void DespawnAll()
        {
            foreach (NetworkObject avatar in spawnedAvatars)
            {
                // 접속 종료로 이미 제거된 아바타를 다시 지우면 FishNet이 경고를 내므로 건너뛴다.
                if (avatar != null && avatar.IsSpawned)
                    ServerManager.Despawn(avatar);
            }
            spawnedAvatars.Clear();
        }

        /// <summary>
        /// 목록에 빠진 역할이 있어도 예외 없이 오류 로그로 알릴 수 있도록 없으면 null을 준다.
        /// </summary>
        private NetworkObject FindPrefab(PlayerRole role)
        {
            RolePrefab entry = Array.Find(rolePrefabs, candidate => candidate.role == role);
            return entry != null ? entry.prefab : null;
        }
    }
}
