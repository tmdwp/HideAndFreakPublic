using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using GuardVsMannequin.Core;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 플레이어 아바타의 역할과 표시 이름을 동기화한다.
    /// </summary>
    public class PlayerIdentity : NetworkBehaviour
    {
        private readonly SyncVar<PlayerRole> role = new(PlayerRole.None);
        private readonly SyncVar<string> displayName = new(string.Empty);

        /// <summary>레지스트리가 아바타를 하나하나 찾지 않고 등록할 수 있도록 알린다.</summary>
        public static event Action<PlayerIdentity> ServerSpawned;

        /// <summary>사망·접속 종료·판 종료 모두 여기로 오므로, 생존자 수 계산은 이것 하나만 들으면 된다.</summary>
        public static event Action<PlayerIdentity> ServerDespawned;

        /// <summary>돌아온 사람을 참가 인원으로 다시 세지 않도록 하는 표시.</summary>
        public bool IsRejoin { get; private set; }

        public PlayerRole Role => role.Value;

        public string DisplayName => displayName.Value;

        /// <summary>
        /// 스폰 전에 넣은 값은 스폰 데이터와 함께 가므로, 클라이언트가 빈 역할을 잠깐 보는 일이 없도록 스폰 직전에 서버에서 정한다.
        /// </summary>
        /// <param name="isRejoin">이번 판 참가 인원을 다시 세지 않도록 돌아온 사람인지 표시한다.</param>
        public void ServerInitialize(PlayerRole newRole, string newDisplayName, bool isRejoin = false)
        {
            role.Value = newRole;
            displayName.Value = newDisplayName;
            IsRejoin = isRejoin;
        }

        /// <summary>
        /// 역할이 스폰 데이터로 이미 정해진 뒤라 구독자가 바로 역할을 읽을 수 있는 시점에 알린다.
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerSpawned?.Invoke(this);
        }

        /// <summary>
        /// 생존자 수가 바로 갱신되도록 제거되는 즉시 알린다.
        /// </summary>
        public override void OnStopServer()
        {
            base.OnStopServer();
            ServerDespawned?.Invoke(this);
        }

        /// <summary>
        /// 관전 대상 목록이 클라이언트에 보이는 아바타만 담도록 여기서 등록한다.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            ClientPlayerList.Add(this);
        }

        /// <summary>
        /// 사라진 아바타를 관전 대상으로 고르지 않도록 뺀다.
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();
            ClientPlayerList.Remove(this);
        }
    }
}
