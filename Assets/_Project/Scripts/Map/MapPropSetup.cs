using System.Linq;
using GuardVsMannequin.PropPhysics;
using GuardVsMannequin.Props;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Map
{
    /// <summary>
    /// 맵 씬(MapInfo가 있는 씬)을 불러오면 변신 사물을 판에 맞게 준비한다. 모든 피어가 같은 순서로 같은 계산을 한다.
    /// 1. 판마다 배치 섞기 (<see cref="PropLayoutShuffler"/>)
    /// 2. 모든 변신 사물의 외형을 지금(섞은 직후) 모습으로 복사해 둔다. 외형은 처음 쓸 때 복사되는데,
    ///    사물이 총에 맞아 넘어진 뒤 처음 복사한 PC만 기울어진 외형이 되어 PC마다 달라지는 것을 막는다.
    /// 3. 튕겨나갈 수 있는 사물에 물리 몸체를 붙인다 (<see cref="PropBody"/>, 대상 규칙 <see cref="PropPhysicsRules"/>)
    /// </summary>
    public static class MapPropSetup
    {
        /// <summary>
        /// 씬 불러오기 이벤트를 구독한다. (플레이 모드에 들어갈 때마다 한 번)
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>
        /// 맵 씬이면 섞기 → 외형 복사 → 물리 몸체 순서로 준비한다.
        /// </summary>
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            if (!roots.Any(root => root.GetComponentInChildren<MapInfo>(true) != null))
                return;

            PropLayoutShuffler.ShuffleForMatch(scene);

            int bodies = 0;
            foreach (GameObject root in roots)
            {
                foreach (TransformableProp prop in root.GetComponentsInChildren<TransformableProp>(true))
                {
                    _ = prop.Appearance;
                    if (prop.Index == TransformableProp.None || !PropPhysicsRules.CanSimulate(prop))
                        continue;

                    PropBody.Attach(prop);
                    bodies++;
                }
            }
            Debug.Log($"[MapPropSetup] {scene.name}: 튕겨나갈 수 있는 사물 {bodies}개");
        }
    }
}
