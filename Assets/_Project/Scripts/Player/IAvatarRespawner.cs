using FishNet.Connection;
using GuardVsMannequin.Core;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 판 도중 아바타를 다시 스폰한다. (서버 전용, 테스트 방의 역할·모습 전환에 쓴다)
    /// </summary>
    public interface IAvatarRespawner
    {
        /// <summary>
        /// 연결이 조종하는 살아 있는 아바타의 역할을 찾는다. 아바타가 없으면 false.
        /// </summary>
        bool TryGetRole(NetworkConnection conn, out PlayerRole role);

        /// <summary>
        /// 연결의 아바타를 지금 자리에서 역할을 바꿔 다시 스폰한다. 아바타가 없으면 역할의 스폰 지점에 스폰한다.
        /// </summary>
        void Respawn(NetworkConnection conn, PlayerRole role);

        /// <summary>
        /// 그 역할의 아바타를 모두 같은 자리에 같은 역할로 다시 스폰한다. (모습·체력 초기화)
        /// </summary>
        void RespawnRole(PlayerRole role);
    }
}
