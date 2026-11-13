using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Mods
{
    /// <summary>모드 맵 씬 이름이 겹치면 PC마다 다른 씬이 열릴 수 있어 막으려고 내장 씬 이름 목록을 모은다.</summary>
    public static class BuiltInScenes
    {
        private static HashSet<string> names;

        /// <summary>
        /// 도메인 재로드를 끈 채 빌드 설정을 바꾸면 옛 목록이 남으므로 플레이 모드에 들어갈 때 다시 읽게 한다.
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            names = null;
        }

        /// <summary>네트워크 씬 조회가 대소문자를 구분하지 않으므로 여기서도 무시하고 비교한다.</summary>
        public static bool Contains(string sceneName)
        {
            if (names == null)
            {
                names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                    names.Add(Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i)));
            }
            return !string.IsNullOrEmpty(sceneName) && names.Contains(sceneName);
        }
    }
}
