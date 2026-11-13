using System.Collections.Generic;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace GuardVsMannequin.Mods
{
    /// <summary>모드 맵 씬이 참조하는 번들을 필요할 때 열고 쓰지 않을 때 해제한다.</summary>
    public static class ModBundleCache
    {
        private static readonly Dictionary<string, AssetBundle> loaded = new();

        /// <summary>모드 번들을 하나도 열지 않았으면 씬 검사를 건너뛸 수 있어서 노출한다.</summary>
        public static bool HasLoadedBundles => loaded.Count > 0;

        /// <summary>
        /// 도메인 재로드를 끈 경우 이미 해제된 번들 기록이 남으므로 플레이 모드에 들어갈 때 비운다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            loaded.Clear();
        }

        /// <summary>씬이 에셋 번들 안의 것을 참조하므로 에셋 번들을 먼저, 씬 번들을 마지막에 연다.</summary>
        public static bool EnsureForScene(string sceneName)
        {
            MapEntry map = FindModMap(sceneName);
            if (map == null)
                return true;
            if (!ModService.Instance.TryGet(map.SourceId, out ModInfo mod) || mod.Status != ModStatus.Active)
            {
                Debug.LogError($"[ModBundleCache] 맵 '{map.Id}'의 모드 '{map.SourceId}'를 쓸 수 없습니다.");
                return false;
            }

            foreach (string dependencyId in mod.Manifest.dependencies)
            {
                if (ModService.Instance.TryGet(dependencyId, out ModInfo dependency) && !Load(dependency.GetFilePath(dependency.Manifest.assetBundle)))
                    return false;
            }
            return Load(mod.GetFilePath(mod.Manifest.assetBundle)) && Load(mod.GetFilePath(mod.Manifest.sceneBundle));
        }

        /// <summary>번들은 메모리를 많이 쓰므로 모드 맵 씬이 하나도 없을 때 모두 해제한다.</summary>
        public static void UnloadIfUnused()
        {
            if (loaded.Count == 0)
                return;

            foreach (MapEntry map in ContentRegistry.Instance.Maps)
            {
                if (!map.IsBuiltIn && UnitySceneManager.GetSceneByName(map.SceneName).isLoaded)
                    return;
            }

            foreach (AssetBundle bundle in loaded.Values)
            {
                if (bundle != null)
                    bundle.Unload(true);
            }
            loaded.Clear();
        }

        /// <summary>같은 번들을 두 번 열면 Unity가 오류를 내므로 이미 열었으면 그대로 쓴다.</summary>
        private static bool Load(string bundlePath)
        {
            if (string.IsNullOrEmpty(bundlePath) || loaded.ContainsKey(bundlePath))
                return true;

            AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
            if (bundle == null)
            {
                Debug.LogError($"[ModBundleCache] 번들을 불러올 수 없습니다: {bundlePath} (Unity 버전·빌드 대상이 다르거나 파일이 손상됨)");
                return false;
            }
            loaded[bundlePath] = bundle;
            return true;
        }

        /// <summary>FishNet은 씬 이름만 넘기므로 이름으로 어느 모드의 맵인지 찾는다.</summary>
        private static MapEntry FindModMap(string sceneName)
        {
            foreach (MapEntry map in ContentRegistry.Instance.Maps)
            {
                if (!map.IsBuiltIn && map.SceneName == sceneName)
                    return map;
            }
            return null;
        }
    }
}
