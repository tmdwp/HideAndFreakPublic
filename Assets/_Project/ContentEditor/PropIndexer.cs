using GuardVsMannequin.Props;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>변신 상태를 번호로 동기화하도록 씬의 변신 사물에 계층 순서대로 겹치지 않는 번호를 매긴다.</summary>
    public sealed class PropIndexer : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        /// <summary>
        /// 저장 파일에도 번호가 남아야 검사 도구와 인스펙터가 맞는 값을 보므로, 에디터가 켜질 때 씬 저장에 연결한다.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void HookSceneSaving()
        {
            EditorSceneManager.sceneSaving += (scene, _) => AssignIndices(scene);
        }

        /// <summary>
        /// 저장을 잊고 빌드하거나 플레이해도 실제 게임은 항상 올바른 번호를 쓰도록 씬 처리 단계에서도 매긴다.
        /// </summary>
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            AssignIndices(scene);
        }

        /// <summary>모든 PC가 같은 번호를 얻도록 계층 순서대로 매기고 바뀐 개수를 돌려준다.</summary>
        public static int AssignIndices(Scene scene)
        {
            int nextIndex = 0;
            int changedCount = 0;
            foreach (UnityEngine.GameObject root in scene.GetRootGameObjects())
            {
                foreach (TransformableProp prop in root.GetComponentsInChildren<TransformableProp>(true))
                {
                    if (prop.Index != nextIndex)
                    {
                        prop.SetIndex(nextIndex);
                        if (!EditorApplication.isPlayingOrWillChangePlaymode)
                            EditorUtility.SetDirty(prop);
                        changedCount++;
                    }
                    nextIndex++;
                }
            }
            return changedCount;
        }
    }
}
