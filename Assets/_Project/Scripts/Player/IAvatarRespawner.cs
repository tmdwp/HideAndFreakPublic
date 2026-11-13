using FishNet.Connection;
using GuardVsMannequin.Core;

namespace GuardVsMannequin.Player
{
    /// <summary>테스트 방의 역할·모습 전환을 위해 판 도중 아바타를 다시 스폰한다.</summary>
    public interface IAvatarRespawner
    {
        /// <summary>
        /// 죽어서 관전 중이면 바꿀 역할을 정할 기준이 없으므로, 아바타가 없을 때는 false를 준다.
        /// </summary>
        bool TryGetRole(NetworkConnection conn, out PlayerRole role);

        /// <summary>위치를 다시 찾아가지 않도록 지금 자리에서 다시 스폰한다.</summary>
        void Respawn(NetworkConnection conn, PlayerRole role);

        /// <summary>
        /// 한 번 바뀐 모습·체력을 되돌리는 별도 경로가 없어서, 그 역할 아바타를 모두 같은 자리에 다시 스폰한다.
        /// </summary>
        void RespawnRole(PlayerRole role);
    }
}
