using System.Collections.Generic;
using GuardVsMannequin.Map;
using GuardVsMannequin.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Mods
{
    /// <summary>SDK 검사는 제작자 PC에서만 돌기 때문에 번들에서 불러온 맵 씬을 받는 쪽이 다시 검사한다.</summary>
    public static class ModSceneGuard
    {
        // 로그가 너무 길어 콘솔이 멈추지 않도록 보여 줄 위반 개수 상한
        private const int MaxLoggedViolations = 20;

        private static Scene inspectedScene;
        private static bool inspectedResult;

        /// <summary>게임 코드가 컴포넌트를 더하기 전에 막도록 씬을 준비하는 쪽마다 먼저 부른다.</summary>
        public static bool Approve(Scene scene)
        {
            if (scene.buildIndex >= 0 || !ModBundleCache.HasLoadedBundles)
                return true;
            if (scene == inspectedScene)
                return inspectedResult;

            inspectedScene = scene;
            inspectedResult = Inspect(scene);
            return inspectedResult;
        }

        /// <summary>허용되지 않은 컴포넌트가 돌지 못하도록 걸리면 씬 전체를 끄고, 통과해도 애니메이션 이벤트는 끈다.</summary>
        private static bool Inspect(Scene scene)
        {
            var violations = new List<string>();
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                ModComponentPolicy.CollectViolations(root, violations);
                foreach (MapInfo mapInfo in root.GetComponentsInChildren<MapInfo>(true))
                    CheckSkins(mapInfo, violations);
            }

            if (violations.Count == 0)
            {
                DisableAnimationEvents(roots);
                return true;
            }

            foreach (GameObject root in roots)
                root.SetActive(false);

            int shownCount = Mathf.Min(violations.Count, MaxLoggedViolations);
            Debug.LogError($"[ModSceneGuard] 맵 씬 '{scene.name}'에 허용되지 않은 컴포넌트가 {violations.Count}개 있습니다:\n{string.Join("\n", violations.GetRange(0, shownCount))}");
            return false;
        }

        /// <summary>외형 모델은 씬 검사에서 빠지므로 프리팹을 따로 검사한다.</summary>
        private static void CheckSkins(MapInfo mapInfo, List<string> violations)
        {
            foreach (CharacterSkin skin in new[] { mapInfo.MannequinSkin, mapInfo.GuardSkin, mapInfo.MannequinHuntSkin })
            {
                if (skin == null)
                    continue;

                // 이벤트는 같은 오브젝트의 게임 스크립트 함수를 이름으로 부를 수 있어 모드 외형에서는 항상 끈다.
                skin.fireAnimationEvents = false;
                if (skin.modelPrefab != null)
                    ModComponentPolicy.CollectViolations(skin.modelPrefab, violations);
            }
        }

        /// <summary>
        /// 애니메이션 이벤트는 같은 오브젝트의 게임 스크립트 함수를 이름으로 부를 수 있으므로 모드 씬에서는 끈다.
        /// </summary>
        private static void DisableAnimationEvents(GameObject[] roots)
        {
            foreach (GameObject root in roots)
            {
                foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
                    animator.fireEvents = false;
            }
        }
    }
}
