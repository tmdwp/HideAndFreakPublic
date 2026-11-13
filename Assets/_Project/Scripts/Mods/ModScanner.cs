using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GuardVsMannequin.Mods
{
    /// <summary>모드 폴더의 mod.json을 읽고 모드 하나만 보고 알 수 있는 문제를 검사한다.</summary>
    public static class ModScanner
    {
        public const string ManifestFileName = "mod.json";

        /// <summary>없어도 되는 파일이다.</summary>
        public const string StringsFileName = "strings.csv";

        private static readonly Regex IdPattern = new("^[a-z0-9][a-z0-9_.-]{1,63}$");

        // 맵을 확인할 때마다 다시 계산하지 않도록 파일 크기·시각이 같으면 재사용하는 번들 지문.
        private static readonly Dictionary<string, (string signature, string hash)> HashCache = new();

        /// <summary>설치 폴더에 쓸 권한이 없는 사용자도 모드를 넣도록 사용자 폴더도 함께 본다.</summary>
        public static IReadOnlyList<string> SearchFolders => new[]
        {
            Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Mods"),
            Path.Combine(Application.persistentDataPath, "Mods")
        };

        /// <summary>PC마다 같은 순서로 읽도록 폴더 이름순으로 정렬한다.</summary>
        public static List<ModInfo> ScanAll()
        {
            var result = new List<ModInfo>();
            foreach (string rootFolder in SearchFolders)
            {
                if (!Directory.Exists(rootFolder))
                    continue;

                string[] folders = Directory.GetDirectories(rootFolder);
                Array.Sort(folders, StringComparer.OrdinalIgnoreCase);
                foreach (string folder in folders)
                    result.Add(Read(folder));
            }
            return result;
        }

        /// <summary>
        /// 망가진 모드도 모드 화면에서 이유를 보여 줘야 하므로, 예외를 던지지 않고 오류 상태의 모드로 돌려준다.
        /// </summary>
        public static ModInfo Read(string folder)
        {
            string manifestPath = Path.Combine(folder, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                var missingMod = new ModInfo(null, folder);
                missingMod.MarkError($"{ManifestFileName}이 없습니다.");
                return missingMod;
            }

            ModManifest manifest;
            try
            {
                manifest = JsonUtility.FromJson<ModManifest>(File.ReadAllText(manifestPath, System.Text.Encoding.UTF8));
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException || exception is UnauthorizedAccessException)
            {
                var brokenMod = new ModInfo(null, folder);
                brokenMod.MarkError($"{ManifestFileName}을 읽을 수 없습니다: {exception.Message}");
                return brokenMod;
            }

            var mod = new ModInfo(manifest, folder);
            string problem = Validate(mod);
            if (problem != null)
                mod.MarkError(problem);
            return mod;
        }

        /// <summary>잘못된 모드가 게임 중에 터지지 않도록 미리 검사해 첫 문제를 보여 줄 문장으로 돌려준다.</summary>
        private static string Validate(ModInfo mod)
        {
            ModManifest manifest = mod.Manifest;
            if (manifest == null)
                return $"{ManifestFileName} 형식이 잘못되었습니다.";
            if (string.IsNullOrEmpty(manifest.id) || !IdPattern.IsMatch(manifest.id))
                return $"모드 ID '{manifest.id}'가 규칙(영문 소문자·숫자·. _ -, 2~64자)에 맞지 않습니다.";
            if (string.IsNullOrEmpty(manifest.version))
                return "버전(version)이 없습니다.";

            // 번들은 만든 Unity 버전에서만 안전하게 열리므로 번들이 있을 때만 Unity 버전을 확인한다.
            bool hasBundle = !string.IsNullOrEmpty(manifest.assetBundle) || !string.IsNullOrEmpty(manifest.sceneBundle);
            if (hasBundle && !UnityVersionRules.IsCompatible(manifest.unityVersion, Application.unityVersion, out string unityProblem))
                return unityProblem;
            if (!string.IsNullOrEmpty(manifest.gameVersion) && CompareVersions(Application.version, manifest.gameVersion) < 0)
                return $"게임 버전 {manifest.gameVersion} 이상이 필요합니다. (현재 {Application.version})";

            foreach (string bundleName in new[] { manifest.assetBundle, manifest.sceneBundle })
            {
                if (!string.IsNullOrEmpty(bundleName) && !IsPlainFileName(bundleName))
                    return $"번들 파일 이름 '{bundleName}'이 잘못되었습니다. 모드 폴더 안의 파일 이름만 쓸 수 있습니다.";

                string bundlePath = mod.GetFilePath(bundleName);
                if (bundlePath != null && !File.Exists(bundlePath))
                    return $"번들 파일이 없습니다: {bundleName}";
            }

            string hashProblem = VerifyContentHash(mod);
            if (hashProblem != null)
                return hashProblem;

            manifest.maps ??= new List<MapManifest>();
            manifest.dependencies ??= new List<string>();
            if (manifest.maps.Count > 0 && string.IsNullOrEmpty(manifest.sceneBundle))
                return "맵이 있는데 씬 번들(sceneBundle)이 없습니다.";
            foreach (MapManifest map in manifest.maps)
            {
                if (map == null || string.IsNullOrEmpty(map.scene))
                    return "씬 이름이 없는 맵이 있습니다.";
                if (BuiltInScenes.Contains(map.scene))
                    return $"맵 씬 이름 '{map.scene}'이 게임에 들어 있는 씬과 같습니다. 씬 이름을 바꿔 다시 내보내야 합니다.";
                // 다른 모드의 맵 ID를 가로채지 못하도록 맵 ID는 자기 모드 ID로 시작해야 한다.
                if (string.IsNullOrEmpty(map.id) || !map.id.StartsWith(manifest.id + ".", StringComparison.Ordinal))
                    return $"맵 ID '{map?.id}'는 '{manifest.id}.'로 시작해야 합니다.";
                map.settings ??= new List<MapSettingRange>();
            }
            return null;
        }

        /// <summary>적힌 값이 아니라 실제 파일이 같아야 같은 모드임을 믿을 수 있어 지문을 다시 계산한다.</summary>
        private static string VerifyContentHash(ModInfo mod)
        {
            ModManifest manifest = mod.Manifest;
            if (string.IsNullOrEmpty(manifest.assetBundle) && string.IsNullOrEmpty(manifest.sceneBundle))
                return null;
            if (string.IsNullOrEmpty(manifest.contentHash))
                return "번들 지문(contentHash)이 없습니다. 모드를 다시 내보내야 합니다.";

            try
            {
                return string.Equals(GetContentHash(mod), manifest.contentHash, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : "번들 파일이 mod.json의 지문과 다릅니다. 파일이 손상되었거나 바뀌었으니 모드를 다시 설치하세요.";
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return $"번들 파일을 읽을 수 없습니다: {exception.Message}";
            }
        }

        /// <summary>
        /// 큰 번들의 지문 계산은 무거워서, 크기·수정 시각이 지난번과 같으면 캐시된 결과를 쓴다.
        /// </summary>
        private static string GetContentHash(ModInfo mod)
        {
            ModManifest manifest = mod.Manifest;
            string signature = string.Empty;
            foreach (string bundleName in new[] { manifest.assetBundle, manifest.sceneBundle })
            {
                string bundlePath = mod.GetFilePath(bundleName);
                if (bundlePath == null)
                    continue;

                var file = new FileInfo(bundlePath);
                signature += $"{bundleName}:{file.Length}:{file.LastWriteTimeUtc.Ticks};";
            }

            if (HashCache.TryGetValue(mod.FolderPath, out (string signature, string hash) cached) && cached.signature == signature)
                return cached.hash;

            string hash = ModContentHash.Compute(mod.FolderPath, manifest.assetBundle, manifest.sceneBundle);
            HashCache[mod.FolderPath] = (signature, hash);
            return hash;
        }

        /// <summary>
        /// mod.json에 "../" 같은 경로를 적어 모드 폴더 밖의 파일을 읽지 못하도록 경로 없는 파일 이름만 허용한다.
        /// </summary>
        private static bool IsPlainFileName(string fileName)
        {
            return fileName != "." && fileName != ".." && fileName.IndexOf(':') < 0
                && fileName.IndexOf('/') < 0 && fileName.IndexOf('\\') < 0
                && Path.GetFileName(fileName) == fileName;
        }

        /// <summary>문자열로는 "1.10"이 "1.9"보다 작게 나오므로 숫자 세 자리로 비교한다.</summary>
        public static int CompareVersions(string first, string second)
        {
            int[] firstParts = ParseVersion(first);
            int[] secondParts = ParseVersion(second);
            for (int i = 0; i < 3; i++)
            {
                if (firstParts[i] != secondParts[i])
                    return firstParts[i].CompareTo(secondParts[i]);
            }
            return 0;
        }

        /// <summary>
        /// "1.2"처럼 자리가 빠진 버전도 비교할 수 있도록 없는 자리는 0으로 채운다.
        /// </summary>
        private static int[] ParseVersion(string version)
        {
            var result = new int[3];
            Match match = Regex.Match(version ?? string.Empty, @"^(\d+)(?:\.(\d+))?(?:\.(\d+))?");
            for (int i = 0; i < 3; i++)
            {
                if (match.Success && match.Groups[i + 1].Success)
                    int.TryParse(match.Groups[i + 1].Value, out result[i]);
            }
            return result;
        }
    }
}
