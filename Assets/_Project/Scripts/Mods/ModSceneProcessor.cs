using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using GuardVsMannequin.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Mods
{
    /// <summary>FishNet 기본 처리기는 번들을 모르므로 씬을 불러오기 직전에 모드 번들을 열고 전환 뒤 해제한다.</summary>
    public class ModSceneProcessor : DefaultSceneProcessor
    {
        private const string LoadFailedNoticeKey = "notice.modLoadFailed";
        private const string RejectedNoticeKey = "notice.modRejected";

        private NetworkManager networkManager;

        // 대기열이 끝난 뒤 방을 나가며 보여 줄 문장.
        private string failureNoticeKey;

        /// <summary>세션이 여러 번 시작돼도 처리기가 겹치지 않도록 이미 있으면 그대로 둔다.</summary>
        public static void Install(NetworkManager networkManager)
        {
            if (networkManager == null || networkManager.GetComponent<ModSceneProcessor>() != null)
                return;

            var processor = networkManager.gameObject.AddComponent<ModSceneProcessor>();
            processor.networkManager = networkManager;
            processor.Initialize(networkManager.SceneManager);
            networkManager.SceneManager.SetSceneProcessor(processor);
            networkManager.SceneManager.OnQueueEnd += processor.LeaveIfLoadFailed;
            networkManager.ClientManager.OnClientConnectionState += processor.OnClientConnectionState;
            networkManager.ServerManager.OnServerConnectionState += processor.OnServerConnectionState;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += processor.OnSceneLoaded;
        }

        /// <summary>파괴된 뒤 이벤트가 이 객체를 부르지 않도록 해제한다.</summary>
        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            if (networkManager == null)
                return;
            if (networkManager.SceneManager != null)
                networkManager.SceneManager.OnQueueEnd -= LeaveIfLoadFailed;
            if (networkManager.ClientManager != null)
                networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            if (networkManager.ServerManager != null)
                networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
        }

        /// <summary>
        /// 대기열 도중에 나가면 씬 전환이 로비 씬과 겹치므로, 검사에 걸린 것을 기록만 해 두고 대기열이 끝난 뒤 나간다.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!ModSceneGuard.Approve(scene))
                failureNoticeKey = RejectedNoticeKey;
        }

        /// <summary>
        /// 활성화를 기다리는 씬이 남아 있으면 로비로 돌아갈 때 게임이 멈추므로, 게스트 연결이 끊기면 풀어 준다.
        /// </summary>
        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Stopped && !networkManager.IsServerStarted)
                ReleasePendingLoads();
        }

        /// <summary>
        /// 활성화를 기다리는 씬이 남아 있으면 로비로 돌아갈 때 게임이 멈추므로, 서버가 멈추면 풀어 준다.
        /// </summary>
        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Stopped)
                ReleasePendingLoads();
        }

        /// <summary>끊긴 뒤 동기 로비 로딩이 남은 비활성 로딩을 영원히 기다려 게임이 멈추지 않도록 미리 풀어 준다.</summary>
        private void ReleasePendingLoads()
        {
            foreach (AsyncOperation operation in LoadingAsyncOperations)
            {
                if (operation != null && !operation.isDone)
                    operation.allowSceneActivation = true;
            }
        }

        /// <summary>씬이 번들 안에 있으므로 번들을 먼저 연 뒤 기본 처리기로 불러온다.</summary>
        public override void BeginLoadAsync(string sceneName, LoadSceneParameters parameters)
        {
            // 실패했거나 끊겼으면 남은 씬을 불러오지 않는다.
            bool isDisconnected = networkManager != null && !networkManager.IsClientStarted && !networkManager.IsServerStarted;
            if (failureNoticeKey != null || isDisconnected)
            {
                SkipCurrentLoad();
                return;
            }

            if (!ModBundleCache.EnsureForScene(sceneName))
            {
                FailLoad(sceneName);
                return;
            }

            // 예외가 나면 FishNet 씬 대기열이 비워지지 않아 다음 방도 막히므로 여기서 받는다.
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
        /// 이전 씬이 내려간 뒤라야 더 쓰지 않는 번들을 알 수 있으므로 불러오기가 끝난 뒤 해제를 시도한다.
        /// </summary>
        public override void LoadEnd(LoadQueueData queueData)
        {
            base.LoadEnd(queueData);
            ModBundleCache.UnloadIfUnused();
        }

        /// <summary>
        /// 모드 맵에서 나온 뒤 번들이 메모리에 남지 않도록 씬 내리기가 끝나면 해제를 시도한다.
        /// </summary>
        public override void UnloadEnd(UnloadQueueData queueData)
        {
            base.UnloadEnd(queueData);
            ModBundleCache.UnloadIfUnused();
        }

        /// <summary>대기열 도중에 나가면 씬 전환이 겹치므로 실패만 기록하고 나가기는 대기열 끝에서 한다.</summary>
        private void FailLoad(string sceneName)
        {
            Debug.LogError($"[ModSceneProcessor] 씬 '{sceneName}'을 불러올 수 없어 방을 나갑니다.");
            failureNoticeKey = LoadFailedNoticeKey;
            SkipCurrentLoad();
        }

        /// <summary>
        /// FishNet은 진행 중인 불러오기가 끝나기를 기다리므로, 없는 상태로 만들어 다음 단계로 넘어가게 한다.
        /// </summary>
        private void SkipCurrentLoad()
        {
            LoadingAsyncOperations.RemoveAll(operation => operation == null);
            CurrentAsyncOperation = null;
        }

        /// <summary>
        /// 대기열 도중에 나가면 씬 전환이 로비 씬과 겹치므로, 대기열이 끝난 뒤에 실패 기록이 있으면 방을 나간다.
        /// </summary>
        private void LeaveIfLoadFailed()
        {
            if (failureNoticeKey == null)
                return;

            string noticeKey = failureNoticeKey;
            failureNoticeKey = null;
            SessionServices.Session?.LeaveWithNotice(noticeKey);
        }
    }
}
