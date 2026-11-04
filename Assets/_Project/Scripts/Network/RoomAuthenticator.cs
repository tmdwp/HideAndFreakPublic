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
    /// <summary>
    /// 접속을 검증한다. 통과하지 못한 연결은 거부 이유를 알린 뒤 FishNet이 끊는다.
    /// - 클라이언트: 접속하자마자 게임 버전(<see cref="GameVersion"/>)과 입력한 방 암호를 서버에 보낸다.
    /// - 서버:
    ///   1. 버전이 호스트와 다르거나, 정해진 시간 안에 버전을 보내지 않으면(옛 빌드) 거부. 방 암호가 있으면 암호도 확인 (여러 번 틀리면 잠시 막음)
    ///      모드는 입장 때 확인하지 않는다. 고른 맵의 모드는 게임을 시작할 때 RoomState가 확인한다.
    ///   2. Steam 방: Steam 접속은 로비 멤버만, IP 접속은 호스트 자신(루프백)만 허용
    ///   3. 판이 진행 중이면 거부 (Steam 로비의 참가 차단을 우회한 접속 방지)
    ///   4. 인원이 가득 찼으면 거부
    /// 거부 이유(<see cref="ConnectionRejectedBroadcast"/>)는 SessionService가 받아 로비에 안내한다.
    /// </summary>
    public class RoomAuthenticator : Authenticator
    {
        // 참가자는 로비에 들어간 뒤 접속하지만, 호스트의 로비 멤버 목록 갱신이 늦을 수 있어 잠시 기다린다.
        private const float LobbyMemberWaitSeconds = 5f;
        private const float PollInterval = 0.25f;

        // 클라이언트가 버전을 보낼 때까지 기다리는 시간
        private const float VersionWaitSeconds = 10f;

        private readonly Dictionary<int, JoinRequestBroadcast> receivedRequests = new();
        private readonly PasswordAttemptLimiter passwordAttempts = new();
        private ISessionService session;
        private SteamLobbyService steamLobby;

        public override event Action<NetworkConnection, bool> OnAuthenticationResult;

        /// <summary>
        /// 판정에 필요한 서비스를 연결한다.
        /// </summary>
        public void Configure(ISessionService sessionService, SteamLobbyService lobbyService)
        {
            session = sessionService;
            steamLobby = lobbyService;
        }

        /// <summary>
        /// 참가 요청 메시지를 등록한다. (서버: 인증 전 메시지 수신 허용, 클라이언트: 접속 시 버전·암호 전송)
        /// </summary>
        public override void InitializeOnce(NetworkManager networkManager)
        {
            base.InitializeOnce(networkManager);
            NetworkManager.ServerManager.RegisterBroadcast<JoinRequestBroadcast>(OnJoinRequest, false);
            NetworkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            NetworkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
        }

        /// <summary>
        /// 등록을 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            if (NetworkManager == null)
                return;
            NetworkManager.ServerManager.UnregisterBroadcast<JoinRequestBroadcast>(OnJoinRequest);
            NetworkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            NetworkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        }

        /// <summary>
        /// 클라이언트: 서버에 접속되면 곧바로 버전과 입력한 암호를 보낸다. (호스트 자신은 방 암호를 보낸다)
        /// </summary>
        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
                SendJoinRequest();
        }

        /// <summary>
        /// 클라이언트: 버전·암호를 담은 참가 요청을 보낸다.
        /// </summary>
        private void SendJoinRequest()
        {
            NetworkManager.ClientManager.Broadcast(new JoinRequestBroadcast
            {
                version = GameVersion.Current,
                password = session?.JoinPassword ?? string.Empty
            });
        }

        /// <summary>
        /// 서버: 클라이언트가 보낸 참가 요청을 기억한다. (검증 코루틴이 읽어 간다)
        /// </summary>
        private void OnJoinRequest(NetworkConnection connection, JoinRequestBroadcast message, Channel channel)
        {
            if (!connection.IsAuthenticated)
                receivedRequests[connection.ClientId] = message;
        }

        /// <summary>
        /// 서버: 끊긴 연결의 기록을 지운다.
        /// </summary>
        private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Stopped)
                receivedRequests.Remove(connection.ClientId);
        }

        /// <summary>
        /// 접속 직후(FishNet 버전 확인 후) 호출된다. 검증은 코루틴으로 진행한다.
        /// </summary>
        public override void OnRemoteConnection(NetworkConnection connection)
        {
            StartCoroutine(Authenticate(connection));
        }

        /// <summary>
        /// 버전 → 접속 경로 → 방 상태(진행 중/인원) 순서로 검증하고 결과를 알린다.
        /// </summary>
        private IEnumerator Authenticate(NetworkConnection connection)
        {
            // 1. 버전 확인
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

            // 1-1. 암호 확인 (암호를 정한 방만)
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

            // 2. 접속 경로 확인 (Steam 방)
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

            // 기다리는 동안 연결이 끊겼으면 결과를 알리지 않는다.
            if (!connection.IsActive)
                yield break;

            // 3~4. 방 상태 확인
            if (GameServices.Room != null && GameServices.Room.IsInGame)
            {
                Reject(connection, ConnectionRejectReason.InGame, "게임 진행 중 접속");
                yield break;
            }

            if (CountAuthenticatedClients() >= GameConstants.MaxPlayers)
            {
                Reject(connection, ConnectionRejectReason.RoomFull, "방 인원 초과");
                yield break;
            }

            receivedRequests.Remove(connection.ClientId);
            OnAuthenticationResult?.Invoke(connection, true);
        }

        /// <summary>
        /// Steam 로비 멤버인지 확인한다.
        /// </summary>
        private bool IsLobbyMember(ulong steamId)
        {
            return steamLobby != null && steamLobby.IsLobbyMember(steamId);
        }

        /// <summary>
        /// 루프백 주소인지 확인한다. (IPv4를 매핑한 IPv6 주소 포함)
        /// </summary>
        private static bool IsLoopback(IPAddress ip)
        {
            if (ip.IsIPv4MappedToIPv6)
                ip = ip.MapToIPv4();
            return IPAddress.IsLoopback(ip);
        }

        /// <summary>
        /// 인증을 마친 연결 수를 센다.
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
        /// 거부 이유를 클라이언트에 알린 뒤 접속을 거부한다.
        /// (메시지가 먼저 전달되도록 결과 알림보다 앞서 보낸다. FishNet은 보낼 메시지를 내보낸 뒤 연결을 끊는다)
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
