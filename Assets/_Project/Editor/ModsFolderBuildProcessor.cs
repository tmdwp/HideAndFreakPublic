using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>플레이어가 모드를 어디에 넣을지 바로 알도록 빌드 옆에 Mods 폴더와 안내문을 만든다.</summary>
    public class ModsFolderBuildProcessor : IPostprocessBuildWithReport
    {
        private const string FolderName = "Mods";
        private const string ReadmeFileName = "README.txt";

        public int callbackOrder => 10;

        /// <summary>모드는 실행 파일 옆 폴더에서 읽으므로 그 구조가 있는 독립 실행 빌드에서만 만든다.</summary>
        public void OnPostprocessBuild(BuildReport report)
        {
            BuildTarget buildTarget = report.summary.platform;
            if (buildTarget != BuildTarget.StandaloneWindows && buildTarget != BuildTarget.StandaloneWindows64 && buildTarget != BuildTarget.StandaloneLinux64)
                return;

            string modsFolder = Path.Combine(Path.GetDirectoryName(report.summary.outputPath) ?? ".", FolderName);
            Directory.CreateDirectory(modsFolder);
            File.WriteAllText(Path.Combine(modsFolder, ReadmeFileName), BuildReadme(), new UTF8Encoding(true));
            Debug.Log($"[ModsFolderBuildProcessor] 모드 폴더를 만들었습니다: {modsFolder}");
        }

        /// <summary>게임을 켜지 않고도 설치 방법을 알 수 있게 한국어·영어 안내문을 만든다.</summary>
        private static string BuildReadme()
        {
            return
                "[한국어]\n" +
                "모드 폴더입니다. 받은 모드 폴더(mod.json이 들어 있는 폴더)를 통째로 이 폴더 안에 넣으세요.\n" +
                "  예) Mods/myname.spookymaps/mod.json\n" +
                "- 게임을 켜거나 로비의 '모드' 화면에서 '다시 읽기'를 누르면 적용됩니다.\n" +
                "- '모드' 화면에서 모드를 켜고 끌 수 있고, 쓸 수 없는 모드는 이유가 표시됩니다.\n" +
                "- 함께 플레이하려면 방의 모두가 같은 모드(같은 버전)를 켜야 합니다.\n" +
                "- 모드는 코드를 실행하지 않지만, 믿을 수 있는 곳에서 받은 모드만 설치하세요.\n" +
                $"- 이 폴더에 쓸 수 없으면 사용자 폴더(%USERPROFILE%/AppData/LocalLow/{PlayerSettings.companyName}/{PlayerSettings.productName}/Mods)를 써도 됩니다.\n\n" +
                "[English]\n" +
                "This is the mods folder. Put each downloaded mod folder (the one containing mod.json) inside it.\n" +
                "  e.g. Mods/myname.spookymaps/mod.json\n" +
                "- Restart the game or press 'Reload' on the lobby 'Mods' screen.\n" +
                "- Turn mods on or off on the 'Mods' screen. Mods that cannot be used show the reason.\n" +
                "- Everyone in a room must enable the same mods (same versions) to play together.\n" +
                "- Mods cannot run code, but only install mods from sources you trust.\n";
        }
    }
}
