using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GuardVsMannequin.Mods
{
    /// <summary>
    /// 모드 폴더를 찾아 정보 파일(mod.json)을 읽고, 모드 하나만 보고 알 수 있는 문제를 검사한다.
    /// (모드끼리의 관계 — ID 중복, 의존성, 켜기/끄기 — 는 <see cref="ModService"/>가 정한다)
    /// 검색 위치: 실행 파일 옆 Mods/ → 사용자 폴더(persistentDataPath)/Mods/. 폴더 하나가 모드 하나다.
    /// </summary>
    public static class ModScanner
    {
        /// <summary>모드 정보 파일 이름.</summary>
        public const string ManifestFileName = "mod.json";

        /// <summary>모드 문장 파일 이름. (선택)</summary>
        public const string StringsFileName = "strings.csv";

        private static readonly Regex IdPattern = new("^[a-z0-9][a-z0-9_.-]{1,63}$");

        /// <summary>
        /// 모드를 찾는 폴더 목록. (에디터에서는 프로젝트 폴더의 Mods/)
        /// </summary>
        public static IReadOnlyList<string> SearchFolders => new[]
        {
            Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Mods"),
            Path.Combine(Application.persistentDataPath, "Mods")
        };

        /// <summary>
        /// 모든 검색 폴더의 모드를 읽는다. 폴더 이름순이며, 같은 ID의 정리는 호출한 쪽이 한다.
        /// </summary>
        public static List<ModInfo> ScanAll()
        {
            var result = new List<ModInfo>();
            foreach (string root in SearchFolders)
            {
                if (!Directory.Exists(root))
                    continue;

                string[] folders = Directory.GetDirectories(root);
                Array.Sort(folders, StringComparer.OrdinalIgnoreCase);
                foreach (string folder in folders)
                    result.Add(Read(folder));
            }
            return result;
        }

        /// <summary>
        /// 모드 폴더 하나를 읽고 검사한다. 문제가 있으면 오류 상태의 모드를 반환한다.
        /// </summary>
        public static ModInfo Read(string folder)
        {
            string manifestPath = Path.Combine(folder, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                var missing = new ModInfo(null, folder);
                missing.MarkError($"{ManifestFileName}이 없습니다.");
                return missing;
            }

            ModManifest manifest;
            try
            {
                manifest = JsonUtility.FromJson<ModManifest>(File.ReadAllText(manifestPath, System.Text.Encoding.UTF8));
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException || exception is UnauthorizedAccessException)
            {
                var broken = new ModInfo(null, folder);
                broken.MarkError($"{ManifestFileName}을 읽을 수 없습니다: {exception.Message}");
                return broken;
            }

            var mod = new ModInfo(manifest, folder);
            string problem = Validate(mod);
            if (problem != null)
                mod.MarkError(problem);
            return mod;
        }

        /// <summary>
        /// 모드 하나의 형식·버전·파일을 검사한다. 문제가 없으면 null.
        /// </summary>
        private static string Validate(ModInfo mod)
        {
            ModManifest manifest = mod.Manifest;
            if (manifest == null)
                return $"{ManifestFileName} 형식이 잘못되었습니다.";
            if (string.IsNullOrEmpty(manifest.id) || !IdPattern.IsMatch(manifest.id))
                return $"모드 ID '{manifest.id}'가 규칙(영문 소문자·숫자·. _ -, 2~64자)에 맞지 않습니다.";
            if (string.IsNullOrEmpty(manifest.version))
                return "버전(version)이 없습니다.";

            bool hasBundle = !string.IsNullOrEmpty(manifest.assetBundle) || !string.IsNullOrEmpty(manifest.sceneBundle);
            if (hasBundle && !UnityVersionRules.IsCompatible(manifest.unityVersion, Application.unityVersion, out string unityProblem))
                return unityProblem;
            if (!string.IsNullOrEmpty(manifest.gameVersion) && CompareVersions(Application.version, manifest.gameVersion) < 0)
                return $"게임 버전 {manifest.gameVersion} 이상이 필요합니다. (현재 {Application.version})";

            foreach (string bundle in new[] { manifest.assetBundle, manifest.sceneBundle })
            {
                if (!string.IsNullOrEmpty(bundle) && !IsPlainFileName(bundle))
                    return $"번들 파일 이름 '{bundle}'이 잘못되었습니다. 모드 폴더 안의 파일 이름만 쓸 수 있습니다.";

                string path = mod.GetFilePath(bundle);
                if (path != null && !File.Exists(path))
                    return $"번들 파일이 없습니다: {bundle}";
            }

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
                if (string.IsNullOrEmpty(map.id) || !map.id.StartsWith(manifest.id + ".", StringComparison.Ordinal))
                    return $"맵 ID '{map?.id}'는 '{manifest.id}.'로 시작해야 합니다.";
                map.settings ??= new List<MapSettingRange>();
            }
            return null;
        }

        /// <summary>
        /// 경로 없는 파일 이름인지 확인한다. (mod.json으로 모드 폴더 밖의 파일을 읽지 못하게 한다)
        /// </summary>
        private static bool IsPlainFileName(string fileName)
        {
            return fileName != "." && fileName != ".." && fileName.IndexOf(':') < 0
                && fileName.IndexOf('/') < 0 && fileName.IndexOf('\\') < 0
                && Path.GetFileName(fileName) == fileName;
        }

        /// <summary>
        /// "1.2.3" 형식 버전을 비교한다. 숫자가 아닌 뒤쪽(예: "-p6")은 무시한다. a &lt; b면 음수.
        /// </summary>
        public static int CompareVersions(string a, string b)
        {
            int[] left = ParseVersion(a);
            int[] right = ParseVersion(b);
            for (int i = 0; i < 3; i++)
            {
                if (left[i] != right[i])
                    return left[i].CompareTo(right[i]);
            }
            return 0;
        }

        /// <summary>
        /// 버전 문자열의 앞쪽 숫자 세 개를 읽는다. 없는 자리는 0.
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
