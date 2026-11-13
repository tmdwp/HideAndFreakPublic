using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GuardVsMannequin.Mods;
using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>게임 코드와 유료 에셋 없이 맵 제작에 필요한 것만 담은 모드 SDK 패키지를 만든다.</summary>
    public static class ModSdkPackageExporter
    {
        private const string OutputFolder = "Builds/ModSDK";
        private const string GuidePath = "Docs/ModdingGuide.md";
        private static readonly string[] PackagedFolders = { "Assets/_Project/Content", "Assets/_Project/ContentEditor", ModSdkSettings.Root };
        private static readonly string[] RequiredPackages = { "com.unity.render-pipelines.universal", "com.unity.ai.navigation" };

        /// <summary>SDK가 게임과 같은 규칙으로 검사하도록 게임 설정·Unity 버전·씬 이름을 파일로 함께 담는다.</summary>
        [MenuItem(EditorMenus.Root + "모드/모드 SDK 패키지 만들기")]
        public static void Export()
        {
            // 그래픽 설정은 게임 설정이 바뀌었을 수 있으므로 매번 새로 복사하고, 예제 템플릿은 없을 때만 만든다.
            if (!ModSdkGraphicsExporter.Export())
                return;
            // SDK의 모드 내보내기가 제작 Unity 버전을 확인할 수 있게 게임의 Unity 버전을 적어 둔다.
            File.WriteAllText(ModSdkSettings.GameUnityVersionPath, Application.unityVersion, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ModSdkSettings.GameUnityVersionPath);
            // 모드 맵 씬 이름이 게임 내장 씬과 겹치지 않는지 SDK에서 확인할 수 있게 게임 씬 목록을 적어 둔다.
            string[] sceneNames = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                .Select(scene => Path.GetFileNameWithoutExtension(scene.path)).ToArray();
            File.WriteAllLines(ModSdkSettings.GameSceneNamesPath, sceneNames, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ModSdkSettings.GameSceneNamesPath);
            if (!File.Exists(ModSdkTemplateBuilder.ScenePath))
                ModSdkTemplateBuilder.Build();

            Directory.CreateDirectory(OutputFolder);
            string packagePath = Path.Combine(OutputFolder, $"HideAndFreak_ModSDK_{Application.version}.unitypackage");
            AssetDatabase.ExportPackage(PackagedFolders, packagePath, ExportPackageOptions.Recurse);

            if (File.Exists(GuidePath))
                File.Copy(GuidePath, Path.Combine(OutputFolder, "ModdingGuide.md"), true);
            File.WriteAllText(Path.Combine(OutputFolder, "requirements.txt"), BuildRequirements(), new UTF8Encoding(false));

            string fullPath = Path.GetFullPath(OutputFolder);
            Debug.Log($"[ModSdkPackageExporter] 모드 SDK를 만들었습니다: {fullPath}");
            EditorUtility.RevealInFinder(packagePath);
        }

        /// <summary>제작자가 맞는 Unity·패키지 버전을 미리 준비하도록 지금 게임의 manifest.json에서 필요 환경을 적는다.</summary>
        private static string BuildRequirements()
        {
            var requirements = new StringBuilder();
            requirements.AppendLine("Hide&Freak Mod SDK - System Requirements ");
            requirements.AppendLine($"Game Version: {Application.version}");
            requirements.AppendLine($"Unity Version: {UnityVersionRules.DescribeRange(Application.unityVersion)} (same {Application.unityVersion.Split('.')[0]}.{Application.unityVersion.Split('.')[1]} line, below game version. Newer versions are not allowed)");
            requirements.AppendLine($"  Recommended: {Application.unityVersion} (same as game version)");
            requirements.AppendLine("Render Pipeline: URP");
            requirements.AppendLine("Packages (install same versions in Package Manager):");

            string manifest = File.Exists("Packages/manifest.json") ? File.ReadAllText("Packages/manifest.json") : string.Empty;
            foreach (string packageName in RequiredPackages)
            {
                Match versionMatch = Regex.Match(manifest, $"\"{Regex.Escape(packageName)}\"\\s*:\\s*\"([^\"]+)\"");
                requirements.AppendLine($"  - {packageName} {(versionMatch.Success ? versionMatch.Groups[1].Value : "(same as game version)")}");
            }
            requirements.AppendLine("Build Target: Windows (StandaloneWindows64)");
            return requirements.ToString();
        }
    }
}
