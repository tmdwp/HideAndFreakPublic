using FishNet.Managing;
using FishNet.Managing.Scened;
using GuardVsMannequin.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Mods
{
    /// <summary>
    /// FishNet 씬 처리기 확장. 모든 PC(서버·클라이언트)가 씬을 불러오기 직전에 모드 맵의 번들을 불러오고,
    /// 씬 전환이 끝나면 쓰지 않는 모드 번들을 해제한다. 나머지 동작은 FishNet 기본 처리기와 같다.
    /// 씬을 불러올 수 없으면(번들 손상, 없는 씬 이름) 남은 씬을 건너뛰고, 씬 대기열이 끝난 뒤 방을 나간다.
    /// </summary>
    public class ModSceneProcessor : DefaultSceneProcessor
    {
        private const string LoadFailedNoticeKey = "notice.modLoadFailed";

        private NetworkManager networkManager;
        private bool loadFailed;

        /// <summary>
        /// 네트워크 관리자의 씬 처리기를 이것으로 바꾼다. (이미 바꿨으면 그대로)
        /// </summary>
        public static void Install(NetworkManager networkManager)
        {
            if (networkManager == null || networkManager.GetComponent<ModSceneProcessor>() != null)
                return;

            var processor = networkManager.gameObject.AddComponent<ModSceneProcessor>();
            processor.networkManager = networkManager;
            processor.Initialize(networkManager.SceneManager);
            networkManager.SceneManager.SetSceneProcessor(processor);
            networkManager.SceneManager.OnQueueEnd += processor.LeaveIfLoadFailed;
        }

        /// <summary>
        /// 씬 대기열 구독을 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            if (networkManager != null && networkManager.SceneManager != null)
                networkManager.SceneManager.OnQueueEnd -= LeaveIfLoadFailed;
        }

        /// <summary>
        /// 모드 맵이면 번들을 먼저 불러온 뒤 씬을 불러온다. 실패하면 이 씬을 건너뛴다.
        /// </summary>
        public override void BeginLoadAsync(string sceneName, LoadSceneParameters parameters)
        {
            if (loadFailed)
            {
                SkipCurrentLoad();
                return;
            }

            if (!ModBundleCache.EnsureForScene(sceneName))
            {
                FailLoad(sceneName);
                return;
            }

            // 없는 씬이면 기본 처리기에서 예외가 난다. 그대로 두면 FishNet 씬 대기열이 비워지지 않아
            // 다음 방에서도 씬을 불러오지 못하므로 여기서 받는다. (씬 이름은 호스트가 보낸 값이다)
            try
            {
                base.BeginLoadAsync(sceneName, parameters);
            }
            catch (System.NullReferenceException)
            {
                FailLoad(sceneName);
            }
        }

        /// <summary>
        /// 씬 불러오기가 끝나면(이전 씬은 내려간 뒤) 쓰지 않는 모드 번들을 해제한다.
        /// </summary>
        public override void LoadEnd(LoadQueueData queueData)
        {
            base.LoadEnd(queueData);
            ModBundleCache.UnloadIfUnused();
        }

        /// <summary>
        /// 씬 내리기가 끝나면 쓰지 않는 모드 번들을 해제한다.
        /// </summary>
        public override void UnloadEnd(UnloadQueueData queueData)
        {
            base.UnloadEnd(queueData);
            ModBundleCache.UnloadIfUnused();
        }

        /// <summary>
        /// 불러오기 실패를 기록하고 이 씬을 건너뛴다.
        /// </summary>
        private void FailLoad(string sceneName)
        {
            Debug.LogError($"[ModSceneProcessor] 씬 '{sceneName}'을 불러올 수 없어 방을 나갑니다.");
            loadFailed = true;
            SkipCurrentLoad();
        }

        /// <summary>
        /// 진행 중인 불러오기가 없는 상태로 만들어 FishNet이 다음 단계로 넘어가게 한다.
        /// </summary>
        private void SkipCurrentLoad()
        {
            LoadingAsyncOperations.RemoveAll(operation => operation == null);
            CurrentAsyncOperation = null;
        }

        /// <summary>
        /// 씬 대기열이 끝난 뒤 실패 기록이 있으면 방을 나간다. (대기열 도중에 나가면 씬 전환이 로비 씬과 겹친다)
        /// </summary>
        private void LeaveIfLoadFailed()
        {
            if (!loadFailed)
                return;

            loadFailed = false;
            SessionServices.Session?.LeaveWithNotice(LoadFailedNoticeKey);
        }
    }
}
