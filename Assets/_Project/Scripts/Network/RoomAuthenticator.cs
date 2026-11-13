using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using FishNet.Authenticating;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Network
{
    /// <summary>아무나 들어오지 못하도록 버전·암호·추방·방 상태로 접속을 검증한다.</summary>
    public class RoomAuthenticator : Authenticator
    {
        // 참가자는 로비에 들어간 뒤 접속하지만, 호스트의 로비 멤버 목록 갱신이 늦을 수 있어 잠시 기다린다.
        private const float LobbyMemberWaitSeconds = 5f;
        private const float PollInterval = 0.25f;

        // 클라이언트가 버전을 보낼 때까지 기다리는 시간
        private const float VersionWaitSeconds = 10f;

        // 같은 사람의 이전 연결이 정리되기를 기다리는 시간
        private const float PreviousConnectionWaitSeconds = 3f;

        private readonly Dictionary<int, JoinRequestBroadcast> receivedRequests = new();
        private readonly PasswordAttemptLimiter passwordAttempts = new();
        private ISessionService session;
        private SteamLobbyService steamLobby;

        public override event Action<NetworkConnection, bool> OnAuthenticationResult;

        /// <summary>
        /// FishNet이 검증기를 만드는 시점과 세션이 준비되는 시점이 달라서, 판정에 필요한 서비스를 나중에 연결한다.
        /// </summary>
        public void Configure(ISessionService sessionService, SteamLobbyService lobbyService)
        {
            session = sessionService;
            steamLobby = lobbyService;
        }

        /// <summary>
        /// 버전·암호는 인증 전에 받아야 하므로 서버는 인증 없이 받을 수 있게 등록하고, 클라이언트는 접속하자마자 보내도록 연결 상태를 듣는다.
        /// </summary>
        public override void InitializeOnce(NetworkManager networkManager)
        {
            base.InitializeOnce(networkManager);
            NetworkManager.ServerManager.RegisterBroadcast<JoinRequestBroadcast>(OnJoinRequest, false);
            NetworkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            NetworkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
        }

        /// <summary>
        /// 파괴된 뒤 메시지·연결 콜백이 이 객체를 부르지 않도록 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            if (NetworkManager == null)
                return;
            NetworkManager.ServerManager.UnregisterBroadcast<JoinRequestBroadcast>(OnJoinRequest);
            NetworkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            NetworkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        }

        /// <summary>서버의 버전 대기 시간 안에 도착하도록 접속되자마자 참가 요청을 보낸다.</summary>
        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
                SendJoinRequest();
        }

        /// <summary>
        /// 재접속한 사람을 알아보도록 이번 실행의 토큰도 함께 보낸다.
        /// </summary>
        private void SendJoinRequest()
        {
            NetworkManager.ClientManager.Broadcast(new JoinRequestBroadcast
            {
                version = GameVersion.Current,
                password = session?.JoinPassword ?? string.Empty,
                clientToken = session?.ClientToken ?? string.Empty
            });
        }

        /// <summary>검증 코루틴이 기다리고 있으므로 받은 요청을 기억해 두기만 한다.</summary>
        private void OnJoinRequest(NetworkConnection connection, JoinRequestBroadcast message, Channel channel)
        {
            if (!connection.IsAuthenticated)
                receivedRequests[connection.ClientId] = message;
        }

        /// <summary>
        /// 끊긴 연결의 요청이 남아 같은 ID로 새로 접속한 사람에게 잘못 쓰이지 않도록 지운다.
        /// </summary>
        private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Stopped)
                receivedRequests.Remove(connection.ClientId);
        }

        /// <summary>요청 도착과 로비 멤버 갱신을 기다려야 해서 검증을 코루틴으로 진행한다.</summary>
        public override void OnRemoteConnection(NetworkConnection connection)
        {
            StartCoroutine(Authenticate(connection));
        }

        /// <summary>버전이 다르면 다른 검사가 의미 없으므로 버전부터 정해진 순서로 검증한다.</summary>
        private IEnumerator Authenticate(NetworkConnection connection)
        {
            // 버전 확인.
            float versionWaitEnd = Time.unscaledTime + VersionWaitSeconds;
            while (connection.IsActive && !receivedRequests.ContainsKey(connection.ClientId) && Time.unscaledTime < versionWaitEnd)
                yield return new WaitForSecondsRealtime(PollInterval);

            if (!connection.IsActive)
                yield break;
            if (!receivedRequests.TryGetValue(connection.ClientId, out JoinRequestBroadcast request))
            {
                Reject(connection, ConnectionRejectReason.NoVersion, "버전을 보내지 않음 (버전 확인이 없는 옛 빌드)");
                yield break;
            }
            if (request.version != GameVersion.Current)
            {
                Reject(connection, ConnectionRejectReason.VersionMismatch, $"버전 불일치 (호스트 {GameVersion.Current}, 접속자 {request.version})");
                yield break;
            }

            RoomMembership membership = session?.Membership;
            string playerKey = PlayerKey.Resolve(connection.GetAddress(), request.clientToken, connection.ClientId);
            if (membership != null && membership.IsBanned(playerKey))
            {
                Reject(connection, ConnectionRejectReason.Banned, $"추방된 사람의 접속 ({playerKey})");
                yield break;
            }

            // 무작위 대입을 막도록 여러 번 틀린 주소는 잠시 막으며 암호를 확인한다.
            string roomPassword = session?.RoomPassword ?? string.Empty;
            if (roomPassword.Length > 0)
            {
                string attemptKey = connection.GetAddress();
                if (passwordAttempts.IsLocked(attemptKey, Time.unscaledTime))
                {
                    Reject(connection, ConnectionRejectReason.TooManyPasswordAttempts, $"암호 시도 초과 ({attemptKey})");
                    yield break;
                }
                if ((request.password ?? string.Empty) != roomPassword)
                {
                    passwordAttempts.RecordFailure(attemptKey, Time.unscaledTime);
                    Reject(connection, ConnectionRejectReason.WrongPassword, "암호 불일치");
                    yield break;
                }
                passwordAttempts.Clear(attemptKey);
            }

            // 접속 경로 확인.
            string address = connection.GetAddress();
            if (session != null && session.IsSteamRoom)
            {
                if (IPAddress.TryParse(address, out IPAddress ip))
                {
                    // Steam 방의 Tugboat는 루프백에만 열리지만, 설정 실수에 대비해 한 번 더 확인한다.
                    if (!IsLoopback(ip))
                    {
                        Reject(connection, ConnectionRejectReason.NotLobbyMember, $"Steam 방에 IP로 접속 시도 ({address})");
                        yield break;
                    }
                }
                else if (ulong.TryParse(address, out ulong steamId))
                {
                    float waitEnd = Time.unscaledTime + LobbyMemberWaitSeconds;
                    while (!IsLobbyMember(steamId) && Time.unscaledTime < waitEnd)
                        yield return new WaitForSecondsRealtime(PollInterval);

                    if (!IsLobbyMember(steamId))
                    {
                        Reject(connection, ConnectionRejectReason.NotLobbyMember, $"로비 멤버가 아닌 SteamID의 접속 ({steamId})");
                        yield break;
                    }
                }
                else
                {
                    Reject(connection, ConnectionRejectReason.NotLobbyMember, $"알 수 없는 주소 형식 ({address})");
                    yield break;
                }
            }

            if (!connection.IsActive)
                yield break;

            // 끊긴 것을 서버가 아직 모르는 경우: 이전 연결을 먼저 끊어 대기 자리를 만들게 한다.
            yield return DisconnectPreviousConnection(membership, playerKey, connection);
            if (!connection.IsActive)
                yield break;

            // 방 상태 확인.
            bool isRejoin = false;
            if (GameServices.Room != null && GameServices.Room.IsInGame)
            {
                isRejoin = membership != null && membership.TryClaim(playerKey, connection.ClientId, Time.unscaledTime);
                if (!isRejoin)
                {
                    Reject(connection, ConnectionRejectReason.InGame, "게임 진행 중 접속");
                    yield break;
                }
            }

            if (!isRejoin && CountAuthenticatedClients() >= GameConstants.MaxPlayers)
            {
                Reject(connection, ConnectionRejectReason.RoomFull, "방 인원 초과");
                yield break;
            }

            membership?.Bind(connection.ClientId, playerKey);
            receivedRequests.Remove(connection.ClientId);
            OnAuthenticationResult?.Invoke(connection, true);
        }

        /// <summary>
        /// 같은 사람의 옛 연결이 남아 있으면 재접속 대기 자리가 만들어지지 않으므로 먼저 끊고, 정리될 때까지 잠시 기다린다.
        /// </summary>
        private IEnumerator DisconnectPreviousConnection(RoomMembership membership, string playerKey, NetworkConnection connection)
        {
            if (membership == null || !membership.TryGetClient(playerKey, out int previousId) || previousId == connection.ClientId)
                yield break;
            if (!NetworkManager.ServerManager.Clients.TryGetValue(previousId, out NetworkConnection previousConnection) || !previousConnection.IsActive)
                yield break;

            Debug.Log($"[RoomAuthenticator] 같은 사람의 이전 연결을 끊습니다: 연결 {previousId}");
            previousConnection.Disconnect(true);
            float waitEnd = Time.unscaledTime + PreviousConnectionWaitSeconds;
            while (NetworkManager.ServerManager.Clients.ContainsKey(previousId) && Time.unscaledTime < waitEnd)
                yield return new WaitForSecondsRealtime(PollInterval);
        }

        /// <summary>
        /// 직접 방처럼 Steam 로비가 없으면 멤버일 수 없으므로 false가 된다.
        /// </summary>
        private bool IsLobbyMember(ulong steamId)
        {
            return steamLobby != null && steamLobby.IsLobbyMember(steamId);
        }

        /// <summary>
        /// IPv6 소켓에서는 IPv4 루프백이 매핑된 형태로 오므로 풀어서 확인한다.
        /// </summary>
        private static bool IsLoopback(IPAddress ip)
        {
            if (ip.IsIPv4MappedToIPv6)
                ip = ip.MapToIPv4();
            return IPAddress.IsLoopback(ip);
        }

        /// <summary>
        /// 검증 중인 연결까지 세면 동시에 들어온 사람들이 서로를 막으므로 인증을 마친 연결만 센다.
        /// </summary>
        private int CountAuthenticatedClients()
        {
            int count = 0;
            foreach (NetworkConnection client in NetworkManager.ServerManager.Clients.Values)
            {
                if (client.IsAuthenticated)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// FishNet은 보낼 메시지를 내보낸 뒤 연결을 끊으므로, 클라이언트가 이유를 알 수 있게 결과 알림보다 먼저 거부 이유를 보낸다.
        /// </summary>
        private void Reject(NetworkConnection connection, ConnectionRejectReason reason, string logDetail)
        {
            Debug.LogWarning($"[RoomAuthenticator] 접속 거부: {logDetail}, 연결 {connection.ClientId}");
            receivedRequests.Remove(connection.ClientId);
            if (!connection.IsActive)
                return;

            var message = new ConnectionRejectedBroadcast
            {
                reason = reason,
                hostVersion = GameVersion.Current
            };
            NetworkManager.ServerManager.Broadcast(connection, message, false);
            OnAuthenticationResult?.Invoke(connection, false);
        }
    }
}
