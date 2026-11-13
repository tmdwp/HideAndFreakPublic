using FishNet.Object;

namespace GuardVsMannequin.Core
{
    /// <summary>FishNet은 초기화 전 내부 캐시가 null이라 네트워크 초기화 전에도 안전하게 조회하는 확장 메서드.</summary>
    public static class NetworkBehaviourExtensions
    {
        /// <summary>
        /// 초기화 전에 FishNet 상태 프로퍼티를 읽으면 예외가 나므로, 읽기 전에 이것으로 먼저 거른다.
        /// </summary>
        public static bool IsNetworkReady(this NetworkBehaviour behaviour)
        {
            return behaviour != null && behaviour.NetworkObject != null;
        }

        /// <summary>
        /// IsClientInitialized를 바로 읽으면 초기화 전에 예외가 나므로 대신 쓴다.
        /// </summary>
        public static bool IsClientReady(this NetworkBehaviour behaviour)
        {
            return behaviour.IsNetworkReady() && behaviour.IsClientInitialized;
        }

        /// <summary>
        /// IsServerInitialized를 바로 읽으면 초기화 전에 예외가 나므로 대신 쓴다.
        /// </summary>
        public static bool IsServerReady(this NetworkBehaviour behaviour)
        {
            return behaviour.IsNetworkReady() && behaviour.IsServerInitialized;
        }
    }
}
