using System.Linq;
using FishNet;
using GuardVsMannequin.Core;
using GuardVsMannequin.Mods;
using GuardVsMannequin.PropPhysics;
using GuardVsMannequin.Props;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Map
{
    /// <summary>모든 피어가 같은 순서로 같은 계산을 하도록 맵 씬을 불러오면 변신 사물을 판에 맞게 준비한다.</summary>
    public static class MapPropSetup
    {
        private static Scene pendingScene;

        /// <summary>방 정보를 받는 쪽이 미뤄 둔 준비를 실행해야 하는지 알 수 있도록 노출한다.</summary>
        public static bool HasPending => pendingScene.IsValid() && pendingScene.isLoaded;

        /// <summary>
        /// 도메인 재로드를 끈 경우 정적 구독이 겹쳐 두 번 준비되지 않도록, 플레이 모드에 들어갈 때마다 끊었다가 다시 구독한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            pendingScene = default;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>
        /// 맵이 아닌 씬(룸·메뉴)은 건너뛰고, 게스트가 방 정보를 아직 받지 못했으면 시드가 없으므로 준비를 미룬다.
        /// </summary>
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 게임 스크립트를 붙이기 전에 모드 맵을 검사해 걸린 맵은 준비하지 않는다.
            if (!ModSceneGuard.Approve(scene))
                return;

            GameObject[] roots = scene.GetRootGameObjects();
            if (!roots.Any(root => root.GetComponentInChildren<MapInfo>(true) != null))
                return;

            if (IsWaitingForRoom())
            {
                pendingScene = scene;
                return;
            }
            Prepare(scene, roots);
        }

        /// <summary>
        /// 재접속한 게스트는 맵을 먼저 불러오므로, 방 정보를 받은 뒤에야 같은 시드로 준비할 수 있다.
        /// </summary>
        public static void RunPending()
        {
            if (!HasPending)
                return;

            Scene scene = pendingScene;
            pendingScene = default;
            Prepare(scene, scene.GetRootGameObjects());
        }

        /// <summary>
        /// 호스트는 방 정보를 직접 갖고 있으므로, 게스트로 접속 중이면서 아직 방 정보가 없을 때만 기다린다.
        /// </summary>
        private static bool IsWaitingForRoom()
        {
            return GameServices.Room == null && InstanceFinder.IsClientStarted && !InstanceFinder.IsServerStarted;
        }

        /// <summary>
        /// 섞은 뒤의 모습을 외형으로 복사해야 PC마다 외형이 같고, 물리 몸체는 자리가 정해진 뒤에 붙여야 하므로 이 순서를 지킨다.
        /// </summary>
        private static void Prepare(Scene scene, GameObject[] roots)
        {
            PropLayoutShuffler.ShuffleForMatch(scene);

            int bodyCount = 0;
            foreach (GameObject root in roots)
            {
                foreach (TransformableProp prop in root.GetComponentsInChildren<TransformableProp>(true))
                {
                    // 외형은 처음 읽을 때 복사되므로 지금 읽어 둔다.
                    _ = prop.Appearance;
                    if (prop.Index == TransformableProp.None || !PropPhysicsRules.CanSimulate(prop))
                        continue;

                    PropBody.Attach(prop);
                    bodyCount++;
                }
            }
            Debug.Log($"[MapPropSetup] {scene.name}: 튕겨나갈 수 있는 사물 {bodyCount}개");
        }
    }
}
