using FishNet;
using FishNet.Object;
using UnityEngine;

namespace GuardVsMannequin.Room
{
    /// <summary>게임 후 룸으로 돌아와도 방 상태가 하나만 있도록 없을 때만 서버에서 스폰한다.</summary>
    public class RoomBootstrap : MonoBehaviour
    {
        [Tooltip("전역(Global)으로 설정된 RoomState 프리팹.")]
        [SerializeField] private NetworkObject roomStatePrefab;

        /// <summary>
        /// 방 상태는 서버가 스폰해야 모두에게 동기화되고, 게임 후 룸으로 돌아오면 이미 있어 두 개가 되지 않도록 없을 때만 만든다.
        /// </summary>
        private void Start()
        {
            if (!InstanceFinder.IsServerStarted || FindAnyObjectByType<RoomState>() != null)
                return;

            NetworkObject instance = Instantiate(roomStatePrefab);
            InstanceFinder.ServerManager.Spawn(instance);
        }
    }
}
