using System.IO;
using GuardVsMannequin.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>빌드 후에도 사용자가 문장·설정을 고칠 수 있도록 데이터 파일을 빌드 폴더로 복사한다.</summary>
    public class DataFilesBuildProcessor : IPostprocessBuildWithReport
    {
        private const string ResourcesRoot = "Assets/_Project/Resources";
        private const string ReadmeFileName = "README.txt";

        public int callbackOrder => 0;

        /// <summary>외부 데이터 폴더 구조는 독립 실행 빌드에만 있으므로 그때만 복사한다.</summary>
        public void OnPostprocessBuild(BuildReport report)
        {
            BuildTarget buildTarget = report.summary.platform;
            if (buildTarget != BuildTarget.StandaloneWindows && buildTarget != BuildTarget.StandaloneWindows64
                && buildTarget != BuildTarget.StandaloneLinux64)
            {
                Debug.LogWarning($"[DataFilesBuildProcessor] {buildTarget} 빌드는 데이터 파일 복사를 지원하지 않습니다. 내장 원본만 사용됩니다.");
                return;
            }

            string outputPath = report.summary.outputPath;
            string dataFolder = Path.Combine(Path.GetDirectoryName(outputPath), Path.GetFileNameWithoutExtension(outputPath) + "_Data");
            string destinationRoot = Path.Combine(dataFolder, "StreamingAssets", DataFiles.ExternalFolderName);

            int copiedCount = 0;
            foreach (string folderName in DataFiles.EditableFolders)
                copiedCount += CopyFolder(Path.Combine(ResourcesRoot, folderName), Path.Combine(destinationRoot, folderName));

            WriteReadme(destinationRoot);
            Debug.Log($"[DataFilesBuildProcessor] 데이터 파일 {copiedCount}개를 복사했습니다: {destinationRoot}");
        }

        /// <summary>
        /// 사용자가 편집할 파일만 남기려고 Unity 전용 .meta는 빼고, 폴더 구조는 Resources 경로와 같게 유지한다.
        /// </summary>
        private static int CopyFolder(string sourceFolder, string destinationFolder)
        {
            if (!Directory.Exists(sourceFolder))
                return 0;

            int copiedCount = 0;
            foreach (string sourceFile in Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories))
            {
                if (sourceFile.EndsWith(".meta"))
                    continue;

                string relativePath = sourceFile.Substring(sourceFolder.Length).TrimStart('/', '\\');
                string targetPath = Path.Combine(destinationFolder, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                File.Copy(sourceFile, targetPath, overwrite: true);
                copiedCount++;
            }
            return copiedCount;
        }

        /// <summary>저장 형식을 모르고 고치면 반영되지 않으므로 안내문을 함께 둔다.</summary>
        private static void WriteReadme(string destinationRoot)
        {
            const string readme =
                "[한국어]\n" +
                "이 폴더의 파일을 고치고 게임을 다시 실행하면 반영됩니다. 반드시 UTF-8로 저장하세요.\n" +
                "- Localization/strings.csv : 화면 문장 (열 = key, ko, en, ... / 열을 추가하면 새 언어)\n" +
                "- Credits/credits.<언어 코드>.txt : 크레딧\n" +
                "- Credits/assets.txt : 크레딧의 '이용한 에셋' 페이지 (언어 공통)\n" +
                "- Data/default_game_settings.json : 방을 만들 때의 게임 설정 기본값\n" +
                "- Data/game_settings.csv : 룸 설정 항목의 조정 단위와 범위\n" +
                "- Data/game_tuning.csv : 밸런스 수치 (이동 배율, 스태미나, 시야, 쿨타임 등 / 다르면 함께 플레이 불가)\n" +
                "- Data/maps.json : 내장 맵 목록과 맵별 설정 기본값·범위 (맵 순서가 다르면 함께 플레이 불가)\n" +
                "파일을 지우거나 형식이 깨지면 게임에 내장된 원본을 사용합니다.\n\n" +
                "[English]\n" +
                "Edit the files in this folder and restart the game to apply changes. Save them as UTF-8.\n" +
                "If a file is missing or broken, the built-in original is used.\n";

            Directory.CreateDirectory(destinationRoot);
            File.WriteAllText(Path.Combine(destinationRoot, ReadmeFileName), readme, new System.Text.UTF8Encoding(true));
        }
    }
}
