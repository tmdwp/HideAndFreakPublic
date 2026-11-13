using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>FishNet을 업데이트하면 모드 맵 로딩용 로컬 수정이 사라지므로 남아 있는지 확인한다.</summary>
    [InitializeOnLoad]
    public static class FishNetPatchCheck
    {
        /// <summary>프로젝트 기준 경로.</summary>
        public const string PatchedFilePath = "Assets/FishNet/Runtime/Managing/Scened/SceneLookupData.cs";

        // 수정 위치를 찾는 표시 주석과, 실제로 바뀐 코드
        private const string PatchMarker = "[Hide&Freak 수정]";
        private const string PatchedCode = "string.IsNullOrEmpty(fullPath) ? scene.name";

        // 에디터를 켠 동안 알림 창을 한 번만 띄우기 위한 키
        private const string DialogShownKey = "GuardVsMannequin.FishNetPatchCheck.DialogShown";

        /// <summary>업데이트 직후 바로 알아야 빌드 전에 고칠 수 있으므로 스크립트를 불러올 때마다 검사한다.</summary>
        static FishNetPatchCheck()
        {
            EditorApplication.delayCall += CheckOnLoad;
        }

        /// <summary>창이 매번 뜨면 방해되므로 창은 에디터 실행 중 한 번만 띄우고 로그는 매번 남긴다.</summary>
        private static void CheckOnLoad()
        {
            if (IsPatchPresent(out string problem))
                return;

            Debug.LogError($"[FishNetPatchCheck] {problem}\n{HowToFix}");
            if (SessionState.GetBool(DialogShownKey, false) || Application.isBatchMode)
                return;

            SessionState.SetBool(DialogShownKey, true);
            EditorUtility.DisplayDialog("FishNet 수정이 빠졌습니다", $"{problem}\n\n{HowToFix}", "확인");
        }

        /// <summary>표시 주석만 남고 코드가 되돌려진 경우도 있어 실제 코드로 판단한다.</summary>
        public static bool IsPatchPresent(out string problem)
        {
            if (!File.Exists(PatchedFilePath))
            {
                problem = $"{PatchedFilePath} 파일을 찾을 수 없습니다. FishNet 폴더 구조가 바뀌었을 수 있습니다.";
                return false;
            }

            string sourceCode = File.ReadAllText(PatchedFilePath, Encoding.UTF8);
            if (!sourceCode.Contains(PatchedCode))
            {
                problem = sourceCode.Contains(PatchMarker)
                    ? $"{PatchedFilePath}에 표시 주석은 있지만 수정한 코드가 없습니다."
                    : $"{PatchedFilePath}에서 모드 맵 씬 이름 수정이 사라졌습니다. (FishNet을 업데이트했을 수 있음)";
                return false;
            }

            problem = null;
            return true;
        }

        /// <summary>패치 내용을 따로 문서에서 찾지 않아도 되도록 로그와 창에 바로 붙일 코드를 함께 보여 준다.</summary>
        public static string HowToFix =>
            "SceneLookupData(Scene scene) 생성자에서 FullName을 정하는 줄을 아래처럼 바꾸세요. 빠진 채로 빌드하면 게스트가 모드 맵을 불러오지 못합니다.\n" +
            "string fullPath = SceneUtility.GetScenePathByBuildIndex(scene.buildIndex);\n" +
            "// [Hide&Freak 수정] AssetBundle에서 불러온 씬(모드 맵)은 경로가 비므로 씬 이름을 쓴다.\n" +
            "FullName = string.IsNullOrEmpty(fullPath) ? scene.name : Path.GetFileNameWithoutExtension(fullPath);";
    }

    /// <summary>수정이 빠진 빌드는 게스트가 모드 맵을 못 불러오므로 빌드를 멈춘다.</summary>
    public class FishNetPatchBuildCheck : IPreprocessBuildWithReport
    {
        /// <summary>긴 빌드를 다 기다린 뒤 실패하지 않도록 다른 빌드 처리기보다 먼저 실행한다.</summary>
        public int callbackOrder => -100;

        /// <summary>
        /// 패치가 빠진 빌드를 배포하면 게스트가 모드 맵에 못 들어가므로 경고가 아니라 빌드 실패로 막는다.
        /// </summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            if (!FishNetPatchCheck.IsPatchPresent(out string problem))
                throw new BuildFailedException($"[FishNetPatchCheck] {problem}\n{FishNetPatchCheck.HowToFix}");
        }
    }
}
