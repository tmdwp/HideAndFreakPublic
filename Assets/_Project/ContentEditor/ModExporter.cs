using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GuardVsMannequin.Mods;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>모드 설정으로 검사·번호 매기기·번들 빌드·mod.json 작성을 한 번에 해 내보낸다.</summary>
    public static class ModExporter
    {
        private const string ExportRoot = "ModExports";
        private const string ExampleSceneName = "Example_TemplateMap";
        private const string ExampleModId = "example.template";

        /// <summary>모드 설정이 여러 개일 수 있어 Project 창에서 고른 것을 내보낸다.</summary>
        [MenuItem(EditorMenus.Mod + "모드 내보내기 (선택한 Mod Project)")]
        public static void ExportSelected()
        {
            if (Selection.activeObject is not ModProject project)
            {
                EditorUtility.DisplayDialog("모드 내보내기", "Project 창에서 Mod Project 에셋을 선택하세요.\n(없으면 Create > GameSettingTools > Mod Project로 만듭니다)", "확인");
                return;
            }
            Export(project);
        }

        /// <summary>게임에서 거부될 모드를 만들지 않도록 번들 빌드 전에 검사하고, 지문은 실제 번들 파일로 계산한다.</summary>
        public static string Export(ModProject project)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return null;
            if (!ConfirmGraphicsSettings())
                return null;
            if (!ValidateProject(project, out List<string> scenePaths))
                return null;

            string outputFolder = Path.Combine(ExportRoot, project.modId);
            Directory.CreateDirectory(outputFolder);
            string assetBundleName = $"{project.modId}.assets.bundle";
            string sceneBundleName = $"{project.modId}.scenes.bundle";

            var builds = new List<AssetBundleBuild>();
            string[] sharedAssetPaths = project.sharedAssets.Where(asset => asset != null).Select(AssetDatabase.GetAssetPath).Distinct().ToArray();
            if (sharedAssetPaths.Length > 0)
                builds.Add(new AssetBundleBuild { assetBundleName = assetBundleName, assetNames = sharedAssetPaths });
            if (scenePaths.Count > 0)
                builds.Add(new AssetBundleBuild { assetBundleName = sceneBundleName, assetNames = scenePaths.ToArray() });
            if (builds.Count == 0)
            {
                EditorUtility.DisplayDialog("모드 내보내기", "내보낼 맵이나 공유 에셋이 없습니다.", "확인");
                return null;
            }

            AssetBundleManifest buildManifest = BuildPipeline.BuildAssetBundles(outputFolder, builds.ToArray(),
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, BuildTarget.StandaloneWindows64);
            if (buildManifest == null)
            {
                EditorUtility.DisplayDialog("모드 내보내기", "번들 빌드에 실패했습니다. Console을 확인하세요.", "확인");
                return null;
            }

            bool hasAssets = sharedAssetPaths.Length > 0;
            bool hasScenes = scenePaths.Count > 0;
            ModManifest manifest = CreateManifest(project, hasAssets ? assetBundleName : null, hasScenes ? sceneBundleName : null);
            manifest.contentHash = ModContentHash.Compute(outputFolder, manifest.assetBundle, manifest.sceneBundle);
            File.WriteAllText(Path.Combine(outputFolder, "mod.json"), JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
            WriteStringsFile(project, outputFolder);
            DeleteBuildLeftovers(outputFolder);

            string copiedFolder = CopyToGame(project, outputFolder);
            string message = $"내보냈습니다: {Path.GetFullPath(outputFolder)}" + (copiedFolder != null ? $"\n게임 폴더로 복사: {copiedFolder}" : string.Empty);
            Debug.Log($"[ModExporter] {message}");
            EditorUtility.DisplayDialog("모드 내보내기", message, "확인");
            return outputFolder;
        }

        /// <summary>그래픽 설정이 다르면 셰이더 변형이 빠져 게임에서 다르게 보이므로 내보내기 전에 맞출지 묻는다.</summary>
        private static bool ConfirmGraphicsSettings()
        {
            if (ModSdkSettings.IsGameGraphicsApplied())
                return true;

            int choice = EditorUtility.DisplayDialogComplex("모드 내보내기",
                "그래픽 설정이 게임과 다릅니다. 이대로 내보내면 게임에서 일부 재질이 분홍색이나 잘못된 빛으로 보일 수 있습니다.\n" +
                "'GameSettingTools > 모드 제작 > 게임 그래픽 설정 적용'을 지금 실행할까요?",
                "적용 후 내보내기", "취소", "그대로 내보내기");
            if (choice == 1)
                return false;
            if (choice == 0)
                ModSdkSettings.ApplyGameGraphics();
            return true;
        }

        /// <summary>제작자가 게임을 켜 보기 전에 모든 문제를 한 번에 보도록 오류를 모아 보여 준다.</summary>
        private static bool ValidateProject(ModProject project, out List<string> scenePaths)
        {
            scenePaths = new List<string>();
            var errors = new StringBuilder();
            if (string.IsNullOrEmpty(project.modId) || !System.Text.RegularExpressions.Regex.IsMatch(project.modId, "^[a-z0-9][a-z0-9_.-]{1,63}$"))
                errors.AppendLine("모드 ID는 영문 소문자·숫자·. _ - 로 2~64자여야 합니다.");
            if (string.IsNullOrEmpty(project.version))
                errors.AppendLine("버전이 비어 있습니다.");
            // 게임이 읽을 수 없는 Unity 버전으로 만들면 번들이 쓸모없으므로 미리 막는다.
            if (!UnityVersionRules.IsCompatible(Application.unityVersion, ModSdkSettings.GameUnityVersion, out string unityProblem))
                errors.AppendLine($"지금 Unity({Application.unityVersion})로는 내보낼 수 없습니다. {unityProblem} (허용: {UnityVersionRules.DescribeRange(ModSdkSettings.GameUnityVersion)})");

            var mapKeys = new HashSet<string>();
            foreach (ModProject.MapSetting map in project.maps)
            {
                if (map.scene == null)
                {
                    errors.AppendLine("씬이 비어 있는 맵 항목이 있습니다.");
                    continue;
                }
                if (string.IsNullOrEmpty(map.mapKey) || !mapKeys.Add(map.mapKey))
                    errors.AppendLine($"{map.scene.name}: mapKey가 비었거나 겹칩니다.");
                // 게임 내장 씬과 이름이 같으면 게임이 모드를 거부한다.
                if (ModSdkSettings.IsGameSceneName(map.scene.name))
                    errors.AppendLine($"{map.scene.name}: 게임에 들어 있는 씬과 이름이 같습니다. 씬 파일 이름을 바꾸세요. (예: {project.modId.Replace('.', '_')}_{map.scene.name})");

                if (map.scene.name == ExampleSceneName && project.modId != ExampleModId)
                    Debug.LogWarning($"[ModExporter] 씬 이름이 예제와 같습니다. 예제 모드와 함께 켜면 이 모드를 쓸 수 없으니 씬 파일 이름을 바꾸세요. (예: {project.modId.Replace('.', '_')}_Map)");

                string scenePath = AssetDatabase.GetAssetPath(map.scene);
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                foreach (ModMapValidator.Issue issue in ModMapValidator.Validate(scene))
                {
                    if (issue.isError)
                        errors.AppendLine($"{scene.name}: {issue.message}");
                    else
                        Debug.LogWarning($"[ModExporter] {scene.name}: {issue.message}", issue.context);
                }
                PropIndexer.AssignIndices(scene);
                EditorSceneManager.SaveScene(scene);
                scenePaths.Add(scenePath);
            }

            var sharedViolations = new List<string>();
            foreach (GameObject prefab in project.sharedAssets.OfType<GameObject>())
                ModComponentPolicy.CollectViolations(prefab, sharedViolations);
            foreach (string violation in sharedViolations)
                errors.AppendLine($"공유 에셋의 허용되지 않은 컴포넌트 - {violation}");

            if (errors.Length == 0)
                return true;
            Debug.LogError($"[ModExporter] 내보낼 수 없습니다:\n{errors}");
            EditorUtility.DisplayDialog("모드 내보내기", $"오류가 있어 내보내지 않았습니다.\n\n{errors}", "확인");
            return false;
        }

        /// <summary>번역을 모두 지웠을 때 옛 이름이 남지 않도록 지난 파일을 지우고, 게임이 읽는 BOM 없는 UTF-8로 쓴다.</summary>
        private static void WriteStringsFile(ModProject project, string outputFolder)
        {
            string stringsPath = Path.Combine(outputFolder, "strings.csv");
            string content = ModStringsFile.Build(project.modId, project.maps.Where(map => map.scene != null));
            if (content != null)
                File.WriteAllText(stringsPath, content, new UTF8Encoding(false));
            else if (File.Exists(stringsPath))
                File.Delete(stringsPath);
        }

        /// <summary>다른 플레이어가 링크를 볼 수 없는 주소면 제작자에게 미리 경고한다.</summary>
        private static string GetCheckedUrl(ModProject project)
        {
            string url = (project.downloadUrl ?? string.Empty).Trim();
            if (url.Length > 0 && !ModLinkPolicy.TryGetSafeUrl(url, out _))
                Debug.LogWarning($"[모드 내보내기] 다운로드 주소 '{url}'는 게임에서 열 수 없습니다. GitHub 저장소 주소(https://github.com/소유자/저장소, 뒤에 /releases 가능)나 Steam 창작마당 주소를 쓰세요. 파일을 바로 받는 주소는 열리지 않습니다. 다른 플레이어에게는 모드 이름과 ID만 보입니다.");
            return url;
        }

        /// <summary>제작자가 JSON을 직접 쓰지 않도록 설정 값으로 mod.json 내용을 만든다.</summary>
        private static ModManifest CreateManifest(ModProject project, string assetBundle, string sceneBundle)
        {
            var manifest = new ModManifest
            {
                id = project.modId,
                name = project.modName,
                version = project.version,
                author = project.author,
                description = project.description,
                url = GetCheckedUrl(project),
                gameVersion = project.minGameVersion,
                unityVersion = Application.unityVersion,
                dependencies = project.dependencies.Where(dependency => !string.IsNullOrWhiteSpace(dependency)).ToList(),
                assetBundle = assetBundle,
                sceneBundle = sceneBundle
            };
            foreach (ModProject.MapSetting map in project.maps.Where(mapSetting => mapSetting.scene != null))
            {
                manifest.maps.Add(new MapManifest
                {
                    id = $"{project.modId}.{map.mapKey}",
                    scene = map.scene.name,
                    nameKey = $"mod.{project.modId}.{map.mapKey}",
                    name = map.displayName,
                    minPlayers = map.minPlayers,
                    maxPlayers = map.maxPlayers,
                    settings = map.settings.Select(range => new MapSettingRange { id = range.id, value = range.value, min = range.min, max = range.max }).ToList()
                });
            }
            return manifest;
        }

        /// <summary>
        /// 게임에는 번들과 mod.json만 필요하므로, 배포할 때 헷갈리지 않게 빌드가 남기는 파일(폴더 이름 번들, .manifest)을 지운다.
        /// </summary>
        private static void DeleteBuildLeftovers(string folder)
        {
            foreach (string manifestFile in Directory.GetFiles(folder, "*.manifest"))
                File.Delete(manifestFile);
            string folderBundlePath = Path.Combine(folder, Path.GetFileName(folder));
            if (File.Exists(folderBundlePath))
                File.Delete(folderBundlePath);
        }

        /// <summary>내보낸 뒤 바로 시험하도록 대상 폴더를 비운 뒤 게임 Mods 폴더로 복사한다.</summary>
        private static string CopyToGame(ModProject project, string outputFolder)
        {
            if (string.IsNullOrWhiteSpace(project.copyToModsFolder))
                return null;

            string targetFolder = Path.Combine(project.copyToModsFolder, project.modId);
            if (Directory.Exists(targetFolder))
                Directory.Delete(targetFolder, true);
            Directory.CreateDirectory(targetFolder);
            foreach (string exportedFile in Directory.GetFiles(outputFolder))
                File.Copy(exportedFile, Path.Combine(targetFolder, Path.GetFileName(exportedFile)), true);
            return targetFolder;
        }
    }
}
