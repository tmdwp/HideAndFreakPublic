using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>Steam 멀티플레이 빌드가 동작하도록 Run In Background와 steam_appid.txt 복사를 자동으로 처리한다.</summary>
    public class SteamBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string SteamAppIdFileName = "steam_appid.txt";

        /// <summary>Steamworks.NET의 빌드 후처리가 끝난 뒤 파일을 놓아야 덮어쓰이지 않으므로 다른 처리기보다 뒤에 실행한다.</summary>
        public int callbackOrder => 100;

        /// <summary>
        /// 누가 설정을 꺼 두었어도 빌드된 호스트가 창 전환으로 멈추지 않도록 빌드할 때마다 확인한다.
        /// </summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            EnsureRunInBackground();
        }

        /// <summary>이 파일이 없으면 Steam 밖에서 실행한 빌드가 초기화에 실패하므로 실행 파일 옆에 복사한다.</summary>
        public void OnPostprocessBuild(BuildReport report)
        {
            BuildTarget buildTarget = report.summary.platform;
            if (buildTarget != BuildTarget.StandaloneWindows64 && buildTarget != BuildTarget.StandaloneWindows && buildTarget != BuildTarget.StandaloneLinux64)
                return;

            string sourcePath = Path.Combine(Directory.GetCurrentDirectory(), SteamAppIdFileName);
            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning($"[SteamBuild] 프로젝트 루트에 {SteamAppIdFileName}이 없어 복사하지 못했습니다. 빌드에서 Steam 초기화가 실패할 수 있습니다.");
                return;
            }

            string buildDirectory = Path.GetDirectoryName(report.summary.outputPath);
            string destinationPath = Path.Combine(buildDirectory, SteamAppIdFileName);
            File.Copy(sourcePath, destinationPath, overwrite: true);
            Debug.Log($"[SteamBuild] {SteamAppIdFileName} 복사 완료: {destinationPath}");
        }

        /// <summary>
        /// 에디터에서 여러 창으로 시험할 때도 같은 문제가 생기므로 테스트 환경 생성 도구에서도 부를 수 있게 공개한다.
        /// </summary>
        public static void EnsureRunInBackground()
        {
            if (PlayerSettings.runInBackground)
                return;

            PlayerSettings.runInBackground = true;
            Debug.Log("[SteamBuild] Run In Background를 켰습니다. (창이 포커스를 잃어도 네트워크 게임이 멈추지 않도록)");
        }
    }
}
