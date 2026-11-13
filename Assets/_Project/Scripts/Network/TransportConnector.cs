using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using UnityEngine;

namespace GuardVsMannequin.Network
{
    /// <summary>Steam 방과 직접 접속을 함께 받도록 Multipass 트랜스포트로 연결을 시작하고 끝낸다.</summary>
    public class TransportConnector : MonoBehaviour
    {
        private const string SteamTransportTypeName = "FishySteamworks";
        private const string LocalAddress = "127.0.0.1";
        private const string LoopbackIpv4 = "127.0.0.1";
        private const string LoopbackIpv6 = "::1";

        [SerializeField] private NetworkManager networkManager;

        private Multipass multipass;
        private Transport tugboat;
        private Transport steamTransport;
        private string defaultIpv4BindAddress;
        private string defaultIpv6BindAddress;

        public bool IsServerRunning => networkManager.IsServerStarted;

        public bool IsClientRunning => networkManager.IsClientStarted;

        /// <summary>로비가 Steam 방 버튼을 보여 줄지 정하려고 쓴다.</summary>
        public bool HasSteamTransport
        {
            get
            {
                EnsureTransports();
                return steamTransport != null;
            }
        }

        public NetworkManager NetworkManager => networkManager;

        /// <summary>로비가 Steam 방 버튼을 그리기 전에 트랜스포트 유무를 알 수 있도록 미리 찾아 둔다.</summary>
        private void Start()
        {
            EnsureTransports();
        }

        /// <summary>
        /// NetworkManager의 하위 매니저는 NetworkManager.Awake에서 준비되므로 Awake에서 찾지 않고 처음 필요할 때 한 번만 찾는다.
        /// </summary>
        private void EnsureTransports()
        {
            if (multipass != null)
                return;

            multipass = networkManager.TransportManager.GetTransport<Multipass>();
            if (multipass == null)
            {
                Debug.LogError("[TransportConnector] NetworkManager의 트랜스포트가 Multipass가 아닙니다.");
                return;
            }

            foreach (Transport transport in multipass.Transports)
            {
                if (transport is Tugboat)
                    tugboat = transport;
                // FishySteamworks는 다른 어셈블리에 있어 타입을 직접 참조할 수 없으므로 이름으로 찾는다.
                else if (transport.GetType().Name == SteamTransportTypeName)
                    steamTransport = transport;
            }

            if (tugboat == null)
                Debug.LogError("[TransportConnector] Multipass에 Tugboat가 없습니다.");
            else
            {
                // 직접 방에서 되돌릴 수 있도록 인스펙터에 설정된 바인드 주소를 기억한다.
                defaultIpv4BindAddress = tugboat.GetServerBindAddress(IPAddressType.IPv4);
                defaultIpv6BindAddress = tugboat.GetServerBindAddress(IPAddressType.IPv6);
            }
            if (steamTransport == null)
                Debug.LogWarning("[TransportConnector] Multipass에 FishySteamworks가 없어 Steam 방을 만들 수 없습니다.");
        }

        /// <summary>
        /// 호스트 자신은 항상 Tugboat로 접속하므로 Tugboat는 늘 열고, Steam 방이면 방 코드 없이 IP로 들어오지 못하게 Tugboat를 루프백에만 연다.
        /// </summary>
        /// <returns>서버 시작 요청에 성공했는지 여부.</returns>
        public bool StartServer(bool withSteam)
        {
            EnsureTransports();
            ApplyTugboatBinding(loopbackOnly: withSteam);
            bool isStarted = multipass.StartConnection(true, tugboat.Index);
            if (withSteam && steamTransport != null)
                isStarted &= multipass.StartConnection(true, steamTransport.Index);
            return isStarted;
        }

        /// <summary>
        /// Steam 방에서 IP 직접 접속을 막으려고 루프백 전용 바인딩을 쓰고, 직접 방에서는 인스펙터 설정으로 되돌린다.
        /// </summary>
        private void ApplyTugboatBinding(bool loopbackOnly)
        {
            tugboat.SetServerBindAddress(loopbackOnly ? LoopbackIpv4 : defaultIpv4BindAddress, IPAddressType.IPv4);
            tugboat.SetServerBindAddress(loopbackOnly ? LoopbackIpv6 : defaultIpv6BindAddress, IPAddressType.IPv6);
        }

        /// <summary>
        /// 호스트도 클라이언트로 참여해야 게임 화면이 그려지므로 자기 서버에 접속시킨다.
        /// </summary>
        public bool StartLocalClient()
        {
            return StartDirectClient(LocalAddress);
        }

        /// <summary>개발·LAN에서 Steam 없이 접속하도록 IP로 Tugboat 클라이언트를 연다.</summary>
        public bool StartDirectClient(string address)
        {
            EnsureTransports();
            multipass.SetClientTransport(tugboat);
            tugboat.SetClientAddress(string.IsNullOrWhiteSpace(address) ? LocalAddress : address.Trim());
            return multipass.StartConnection(false);
        }

        /// <summary>
        /// Steam 방은 IP 대신 호스트 SteamID로 접속해 NAT를 넘는다.
        /// </summary>
        public bool StartSteamClient(ulong hostSteamId)
        {
            EnsureTransports();
            if (steamTransport == null)
                return false;

            multipass.SetClientTransport(steamTransport);
            steamTransport.SetClientAddress(hostSteamId.ToString());
            return multipass.StartConnection(false);
        }

        /// <summary>
        /// 다음 방을 열 때 이전 연결이 남아 충돌하지 않도록 클라이언트와 모든 서버 트랜스포트를 닫는다.
        /// </summary>
        public void StopAll()
        {
            EnsureTransports();
            if (networkManager.IsClientStarted)
                networkManager.ClientManager.StopConnection();

            foreach (Transport transport in multipass.Transports)
                multipass.StopConnection(true, transport.Index);
        }
    }
}
