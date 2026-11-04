using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GuardVsMannequin.Mods;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// "모드 내보내기": 모드 설정(<see cref="ModProject"/>)으로 번들과 mod.json을 만든다.
    /// 1. 맵 씬마다 검사(<see cref="ModMapValidator"/>) → 오류가 있으면 중단
    /// 2. 변신 사물 번호를 매기고(<see cref="PropIndexer"/>) 씬 저장
    /// 3. 에셋 번들(공유 에셋)·씬 번들(맵 씬)을 Windows용으로 빌드
    /// 4. mod.json(번들 지문 포함)·strings.csv를 &lt;프로젝트&gt;/ModExports/&lt;모드 ID&gt;/에 쓰고, 지정했으면 게임 Mods 폴더로 복사
    /// </summary>
    public static class ModExporter
    {
        private const string ExportRoot = "ModExports";

        /// <summary>
        /// Project 창에서 고른 모드 설정을 내보낸다.
        /// </summary>
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

        /// <summary>
        /// 모드를 내보낸다. 성공하면 출력 폴더 경로, 실패하면 null.
        /// </summary>
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
            string[] sharedAssetPaths = project.sharedAssets.Where(a => a != null).Select(AssetDatabase.GetAssetPath).Distinct().ToArray();
            if (sharedAssetPaths.Length > 0)
                builds.Add(new AssetBundleBuild { assetBundleName = assetBundleName, assetNames = sharedAssetPaths });
            if (scenePaths.Count > 0)
                builds.Add(new AssetBundleBuild { assetBundleName = sceneBundleName, assetNames = scenePaths.ToArray() });
            if (builds.Count == 0)
            {
                EditorUtility.DisplayDialog("모드 내보내기", "내보낼 맵이나 공유 에셋이 없습니다.", "확인");
                return null;
            }

            AssetBundleManifest built = BuildPipeline.BuildAssetBundles(outputFolder, builds.ToArray(),
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, BuildTarget.StandaloneWindows64);
            if (built == null)
            {
                EditorUtility.DisplayDialog("모드 내보내기", "번들 빌드에 실패했습니다. Console을 확인하세요.", "확인");
                return null;
            }

            bool hasAssets = sharedAssetPaths.Length > 0;
            bool hasScenes = scenePaths.Count > 0;
            ModManifest manifest = CreateManifest(project, hasAssets ? assetBundleName : null, hasScenes ? sceneBundleName : null);
            manifest.contentHash = HashFiles(outputFolder, manifest.assetBundle, manifest.sceneBundle);
            File.WriteAllText(Path.Combine(outputFolder, "mod.json"), JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
            if (project.strings != null)
                File.WriteAllText(Path.Combine(outputFolder, "strings.csv"), project.strings.text, new UTF8Encoding(false));
            DeleteBuildLeftovers(outputFolder);

            string copied = CopyToGame(project, outputFolder);
            string message = $"내보냈습니다: {Path.GetFullPath(outputFolder)}" + (copied != null ? $"\n게임 폴더로 복사: {copied}" : string.Empty);
            Debug.Log($"[ModExporter] {message}");
            EditorUtility.DisplayDialog("모드 내보내기", message, "확인");
            return outputFolder;
        }

        /// <summary>
        /// 그래픽 설정이 게임과 다르면 적용할지 묻는다. 취소하면 false.
        /// (설정이 다르면 셰이더 변형이 빠져 게임에서 분홍색이나 잘못된 빛으로 보일 수 있다)
        /// </summary>
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

        /// <summary>
        /// 모드 정보와 맵 씬을 검사하고, 사물 번호를 매겨 저장한다. 오류가 있으면 false.
        /// </summary>
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
                // 게임 내장 씬과 이름이 같으면 게임이 모드를 거부한다. (PC마다 다른 씬이 열리거나 게스트가 씬을 찾지 못함)
                if (ModSdkSettings.IsGameSceneName(map.scene.name))
                    errors.AppendLine($"{map.scene.name}: 게임에 들어 있는 씬과 이름이 같습니다. 씬 파일 이름을 바꾸세요. (예: {project.modId.Replace('.', '_')}_{map.scene.name})");

                string path = AssetDatabase.GetAssetPath(map.scene);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (ModMapValidator.Issue issue in ModMapValidator.Validate(scene))
                {
                    if (issue.isError)
                        errors.AppendLine($"{scene.name}: {issue.message}");
                    else
                        Debug.LogWarning($"[ModExporter] {scene.name}: {issue.message}", issue.context);
                }
                PropIndexer.AssignIndices(scene);
                EditorSceneManager.SaveScene(scene);
                scenePaths.Add(path);
            }

            if (errors.Length == 0)
                return true;
            Debug.LogError($"[ModExporter] 내보낼 수 없습니다:\n{errors}");
            EditorUtility.DisplayDialog("모드 내보내기", $"오류가 있어 내보내지 않았습니다.\n\n{errors}", "확인");
            return false;
        }

        /// <summary>
        /// 다운로드 주소를 읽고, 게임에서 열 수 없는 주소면 제작자에게 경고한다.
        /// </summary>
        private static string GetCheckedUrl(ModProject project)
        {
            string url = (project.downloadUrl ?? string.Empty).Trim();
            if (url.Length > 0 && !ModLinkPolicy.TryGetSafeUrl(url, out _))
                Debug.LogWarning($"[모드 내보내기] 다운로드 주소 '{url}'는 게임에서 열 수 없습니다. https://github.com/... 또는 Steam 창작마당 주소를 쓰세요. 다른 플레이어에게는 모드 이름과 ID만 보입니다.");
            return url;
        }

        /// <summary>
        /// 모드 설정으로 mod.json 내용을 만든다.
        /// </summary>
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
                dependencies = project.dependencies.Where(d => !string.IsNullOrWhiteSpace(d)).ToList(),
                assetBundle = assetBundle,
                sceneBundle = sceneBundle
            };
            foreach (ModProject.MapSetting map in project.maps.Where(m => m.scene != null))
            {
                manifest.maps.Add(new MapManifest
                {
                    id = $"{project.modId}.{map.mapKey}",
                    scene = map.scene.name,
                    nameKey = $"mod.{project.modId}.{map.mapKey}",
                    name = map.displayName,
                    minPlayers = map.minPlayers,
                    maxPlayers = map.maxPlayers,
                    settings = map.settings.Select(s => new MapSettingRange { id = s.id, value = s.value, min = s.min, max = s.max }).ToList()
                });
            }
            return manifest;
        }

        /// <summary>
        /// 번들 파일 내용의 지문(SHA-256 앞 16자리). 같은 모드 파일인지 비교하는 데 쓴다.
        /// </summary>
        private static string HashFiles(string folder, params string[] fileNames)
        {
            using SHA256 sha = SHA256.Create();
            foreach (string fileName in fileNames.Where(f => !string.IsNullOrEmpty(f)))
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(folder, fileName));
                sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return BitConverter.ToString(sha.Hash, 0, 8).Replace("-", string.Empty).ToLowerInvariant();
        }

        /// <summary>
        /// 번들 빌드가 남기는 파일(폴더 이름 번들, .manifest)을 지운다. 게임에는 번들과 mod.json만 필요하다.
        /// </summary>
        private static void DeleteBuildLeftovers(string folder)
        {
            foreach (string file in Directory.GetFiles(folder, "*.manifest"))
                File.Delete(file);
            string folderBundle = Path.Combine(folder, Path.GetFileName(folder));
            if (File.Exists(folderBundle))
                File.Delete(folderBundle);
        }

        /// <summary>
        /// 지정한 게임 Mods 폴더로 복사한다. 복사한 경로를 반환한다. (지정하지 않았으면 null)
        /// </summary>
        private static string CopyToGame(ModProject project, string outputFolder)
        {
            if (string.IsNullOrWhiteSpace(project.copyToModsFolder))
                return null;

            string target = Path.Combine(project.copyToModsFolder, project.modId);
            if (Directory.Exists(target))
                Directory.Delete(target, true);
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(outputFolder))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            return target;
        }
    }
}
