using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// FishNet 로컬 수정이 남아 있는지 확인한다. (FishNet을 업데이트하면 덮어써져 사라진다)
    /// 수정 내용: <c>SceneLookupData(Scene)</c>가 빌드 인덱스로 경로를 못 찾으면(AssetBundle 모드 맵 씬) <c>scene.name</c>을 쓴다.
    /// 빠지면 게스트가 모드 맵을 불러오지 못한다. (빈 씬 이름이 전송됨)
    /// - 에디터 시작·스크립트 다시 불러오기 때: 빠졌으면 오류 로그를 남기고, 에디터를 켤 때 한 번 알림 창을 띄운다.
    /// - 빌드 전: 빠졌으면 빌드를 멈춘다.
    /// </summary>
    [InitializeOnLoad]
    public static class FishNetPatchCheck
    {
        /// <summary>수정한 FishNet 파일. (프로젝트 기준 경로)</summary>
        public const string PatchedFilePath = "Assets/FishNet/Runtime/Managing/Scened/SceneLookupData.cs";

        // 수정 위치를 찾는 표시 주석과, 실제로 바뀐 코드
        private const string PatchMarker = "[Hide&Freak 수정]";
        private const string PatchedCode = "string.IsNullOrEmpty(fullPath) ? scene.name";

        // 에디터를 켠 동안 알림 창을 한 번만 띄우기 위한 키
        private const string DialogShownKey = "GuardVsMannequin.FishNetPatchCheck.DialogShown";

        /// <summary>
        /// 에디터가 스크립트를 불러온 뒤 검사한다. (에셋 불러오기가 끝난 다음에 실행되도록 미룬다)
        /// </summary>
        static FishNetPatchCheck()
        {
            EditorApplication.delayCall += CheckOnLoad;
        }

        /// <summary>
        /// 수정이 빠졌으면 오류 로그를 남기고, 이번 에디터 실행 중 처음이면 알림 창을 띄운다.
        /// </summary>
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

        /// <summary>
        /// FishNet 파일에 수정이 남아 있는지 확인한다. 없으면 problem에 이유를 담는다.
        /// </summary>
        public static bool IsPatchPresent(out string problem)
        {
            if (!File.Exists(PatchedFilePath))
            {
                problem = $"{PatchedFilePath} 파일을 찾을 수 없습니다. FishNet 폴더 구조가 바뀌었을 수 있습니다.";
                return false;
            }

            string source = File.ReadAllText(PatchedFilePath, Encoding.UTF8);
            if (!source.Contains(PatchedCode))
            {
                problem = source.Contains(PatchMarker)
                    ? $"{PatchedFilePath}에 표시 주석은 있지만 수정한 코드가 없습니다."
                    : $"{PatchedFilePath}에서 모드 맵 씬 이름 수정이 사라졌습니다. (FishNet을 업데이트했을 수 있음)";
                return false;
            }

            problem = null;
            return true;
        }

        /// <summary>다시 넣는 방법 안내.</summary>
        public static string HowToFix =>
            "SceneLookupData(Scene scene) 생성자에서 FullName을 정하는 줄을 아래처럼 바꾸세요. 빠진 채로 빌드하면 게스트가 모드 맵을 불러오지 못합니다.\n" +
            "string fullPath = SceneUtility.GetScenePathByBuildIndex(scene.buildIndex);\n" +
            "// [Hide&Freak 수정] AssetBundle에서 불러온 씬(모드 맵)은 경로가 비므로 씬 이름을 쓴다.\n" +
            "FullName = string.IsNullOrEmpty(fullPath) ? scene.name : Path.GetFileNameWithoutExtension(fullPath);";
    }

    /// <summary>
    /// 빌드 전에 FishNet 로컬 수정을 확인하고, 빠졌으면 빌드를 멈춘다. (<see cref="FishNetPatchCheck"/>)
    /// </summary>
    public class FishNetPatchBuildCheck : IPreprocessBuildWithReport
    {
        /// <summary>다른 빌드 처리기보다 먼저 실행해 일찍 멈춘다.</summary>
        public int callbackOrder => -100;

        /// <summary>
        /// 수정이 빠졌으면 빌드를 실패시킨다.
        /// </summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            if (!FishNetPatchCheck.IsPatchPresent(out string problem))
                throw new BuildFailedException($"[FishNetPatchCheck] {problem}\n{FishNetPatchCheck.HowToFix}");
        }
    }
}
