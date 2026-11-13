using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GuardVsMannequin.Localization;
using UnityEngine;

namespace GuardVsMannequin.Mods
{
    /// <summary>설치된 모드의 중복·켜기/끄기·의존성을 정하고 켜진 모드의 콘텐츠를 등록한다.</summary>
    public sealed class ModService
    {
        private static ModService instance;

        private readonly List<ModInfo> mods = new();

        // 고른 맵에 필요해 이번 접속 동안 켤 모드 ID(null이면 플레이어 설정).
        private HashSet<string> sessionModIds;

        /// <summary>씬과 관계없이 어디서든 같은 모드 상태를 보도록 하나만 둔다.</summary>
        public static ModService Instance => instance ??= new ModService();

        /// <summary>모드 화면이 문제 있는 모드도 보여 주도록 오류·꺼진 모드까지 담는다.</summary>
        public IReadOnlyList<ModInfo> Mods => mods;

        /// <summary>PC마다 같은 순서가 되도록 ID 순으로 준다.</summary>
        public IEnumerable<ModInfo> ActiveMods => mods.Where(mod => mod.Status == ModStatus.Active).OrderBy(mod => mod.Id, StringComparer.Ordinal);

        /// <summary>모드 화면이 다시 읽은 결과를 다시 그리도록 알린다.</summary>
        public event Action Changed;

        /// <summary>로비 맵 목록이 처음부터 모드 맵을 포함하도록 첫 씬이 열리기 전에 읽는다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadOnStart()
        {
            instance = null;
            Instance.Refresh();
        }

        /// <summary>
        /// 앞 단계 결과가 뒤 단계 판단에 쓰이므로 중복 → 켜기/끄기 → 씬 이름 겹침 → 의존성 순서로 정리한 뒤 등록소와 문장을 갱신한다.
        /// </summary>
        public void Refresh()
        {
            mods.Clear();
            mods.AddRange(ModScanner.ScanAll());
            ResolveDuplicates();
            ApplyPreferences();
            ResolveDuplicateScenes();
            ResolveDependencies();

            foreach (ModInfo mod in ActiveMods)
                LoadStrings(mod);

            ContentRegistry.Instance.SetModSources(ActiveMods.Select(mod => (IContentSource)new ModContentSource(mod)));

            foreach (ModInfo mod in mods.Where(mod => mod.Status == ModStatus.Error))
                Debug.LogWarning($"[ModService] 모드 '{mod.Id}'를 쓸 수 없습니다: {mod.Error} ({mod.FolderPath})");
            Debug.Log($"[ModService] 모드 {mods.Count}개 중 {ActiveMods.Count()}개 사용.");
            Changed?.Invoke();
        }

        /// <summary>플레이어가 직접 바꾼 설정이 우선이므로 접속용 임시 구성을 버리고 다시 읽는다.</summary>
        public void SetEnabled(string modId, bool enabled)
        {
            ModPreferences.SetEnabled(modId, enabled);
            sessionModIds = null;
            Refresh();
        }

        /// <summary>의존 모드가 빠지면 게스트가 맵을 열 수 없으므로 맵의 모드와 의존 모드를 모두 모은다.</summary>
        public List<RequiredMod> BuildRequirements(MapEntry map)
        {
            var result = new List<RequiredMod>();
            if (map == null || map.IsBuiltIn)
                return result;

            var visited = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>();
            pending.Push(map.SourceId);
            while (pending.Count > 0)
            {
                string modId = pending.Pop();
                if (!visited.Add(modId) || !TryGet(modId, out ModInfo mod) || mod.Manifest == null)
                    continue;

                result.Add(ToRequirement(mod));
                foreach (string dependencyId in mod.Manifest.dependencies)
                    pending.Push(dependencyId);
            }
            result.Sort((first, second) => string.CompareOrdinal(first.id, second.id));
            return result;
        }

        /// <summary>나만 가진 모드와 씬 이름이 겹치지 않도록 필요한 모드만 이번 접속 동안 켠다.</summary>
        /// <param name="required">호스트가 보낸 필요한 모드 목록.</param>
        /// <returns>비어 있으면 플레이할 수 있다.</returns>
        public List<MissingMod> UseRequiredMods(IReadOnlyList<RequiredMod> required)
        {
            if (required == null || required.Count == 0)
            {
                ClearSessionMods();
                return new List<MissingMod>();
            }

            sessionModIds = new HashSet<string>(required.Take(RequiredModComparer.MaxRequiredMods).Select(mod => mod.id), StringComparer.Ordinal);
            Refresh();
            List<MissingMod> missing = RequiredModComparer.FindMissing(required, modId => TryGet(modId, out ModInfo mod) ? mod : null, BundleSize);
            if (missing.Count > 0 || required.Count > RequiredModComparer.MaxRequiredMods)
                ClearSessionMods();
            return missing;
        }

        /// <summary>방을 나간 뒤 남의 맵 구성이 남지 않도록 플레이어 설정으로 돌아간다.</summary>
        public void ClearSessionMods()
        {
            if (sessionModIds == null)
                return;
            sessionModIds = null;
            Refresh();
        }

        /// <summary>
        /// 꺼 둔 모드의 맵도 그 판 동안 켜서 쓸 수 있으므로 방 목록에서 "있음"으로 보이도록 설치된 맵까지 찾는다.
        /// </summary>
        public bool HasMap(string mapId)
        {
            return ContentRegistry.Instance.IndexOf(mapId) >= 0 || FindInstalledMap(mapId) != null;
        }

        /// <summary>켜진 맵이면 번역된 이름을, 꺼진 모드의 맵이면 적힌 이름을 쓴다.</summary>
        public string FindMapName(string mapId)
        {
            ContentRegistry registry = ContentRegistry.Instance;
            if (registry.TryGetMap(registry.IndexOf(mapId), out MapEntry entry))
                return entry.DisplayName;

            MapManifest installedMap = FindInstalledMap(mapId);
            return installedMap == null ? null : string.IsNullOrEmpty(installedMap.name) ? installedMap.scene : installedMap.name;
        }

        /// <summary>등록소에는 켜진 모드만 있으므로 꺼진 모드까지 보려면 설치 목록을 뒤진다.</summary>
        private MapManifest FindInstalledMap(string mapId)
        {
            foreach (ModInfo mod in mods)
            {
                if (mod.Status == ModStatus.Error || mod.Manifest?.maps == null)
                    continue;
                MapManifest foundMap = mod.Manifest.maps.FirstOrDefault(map => map != null && map.id == mapId);
                if (foundMap != null)
                    return foundMap;
            }
            return null;
        }

        /// <summary>
        /// 게스트가 버전·파일까지 같은지 비교할 수 있도록 지문과 크기를 함께 담는다.
        /// </summary>
        private static RequiredMod ToRequirement(ModInfo mod)
        {
            return new RequiredMod
            {
                id = mod.Id,
                version = mod.Manifest.version,
                contentHash = mod.Manifest.contentHash ?? string.Empty,
                bundleSize = BundleSize(mod),
                name = mod.DisplayName,
                url = mod.Manifest.url ?? string.Empty
            };
        }

        /// <summary>
        /// 의존성·필요한 모드 확인이 ID로 모드를 찾아야 해서 연다.
        /// </summary>
        public bool TryGet(string modId, out ModInfo mod)
        {
            mod = mods.FirstOrDefault(candidate => candidate.Id == modId);
            return mod != null;
        }

        /// <summary>같은 ID가 두 곳에 있으면 헷갈리므로 먼저 찾은 것만 쓰고 나머지는 오류로 둔다.</summary>
        private void ResolveDuplicates()
        {
            var seenIds = new HashSet<string>();
            foreach (ModInfo mod in mods)
            {
                if (mod.Status == ModStatus.Error)
                    continue;
                if (!seenIds.Add(mod.Id))
                    mod.MarkError("같은 ID의 모드가 이미 있습니다. 한 곳에만 두세요.");
            }
        }

        /// <summary>씬 이름이 겹치면 다른 모드의 맵이 열리므로 ID 순서로 먼저인 모드만 쓴다.</summary>
        private void ResolveDuplicateScenes()
        {
            var sceneOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (ModInfo mod in ActiveMods.ToList())
            {
                string duplicateScene = mod.Manifest.maps.Select(map => map.scene).FirstOrDefault(sceneOwners.ContainsKey);
                if (duplicateScene != null)
                {
                    mod.MarkError($"맵 씬 이름 '{duplicateScene}'이 모드 '{sceneOwners[duplicateScene]}'와 겹칩니다. 씬 파일 이름을 바꿔 다시 내보내거나 다른 모드를 지우세요.");
                    continue;
                }

                foreach (MapManifest map in mod.Manifest.maps)
                    sceneOwners[map.scene] = mod.Id;
            }
        }

        /// <summary>
        /// 맵에 필요한 모드를 쓰는 중이면 호스트와 같은 구성이 되도록 그 밖의 모드를 끄고, 아니면 플레이어가 끈 모드를 끈다.
        /// </summary>
        private void ApplyPreferences()
        {
            if (sessionModIds != null)
            {
                foreach (ModInfo mod in mods)
                {
                    if (!sessionModIds.Contains(mod.Id))
                        mod.MarkDisabled();
                }
                return;
            }

            HashSet<string> disabledIds = ModPreferences.LoadDisabled();
            foreach (ModInfo mod in mods)
            {
                if (disabledIds.Contains(mod.Id))
                    mod.MarkDisabled();
            }
        }

        /// <summary>의존 모드 없이 켜지면 번들이 깨지므로 오류로 두고, 영향이 퍼지지 않을 때까지 반복한다.</summary>
        private void ResolveDependencies()
        {
            bool isChanged = true;
            while (isChanged)
            {
                isChanged = false;
                foreach (ModInfo mod in mods.Where(candidate => candidate.Status == ModStatus.Active))
                {
                    string missingId = mod.Manifest.dependencies.FirstOrDefault(dependencyId => !TryGet(dependencyId, out ModInfo dependency) || dependency.Status != ModStatus.Active);
                    if (missingId == null)
                        continue;

                    mod.MarkError($"필요한 모드 '{missingId}'가 없거나 꺼져 있습니다.");
                    isChanged = true;
                }
            }
        }

        /// <summary>모드가 게임 문장을 덮어쓰지 못하도록 모드 접두사 문장만 더한다.</summary>
        private static void LoadStrings(ModInfo mod)
        {
            string stringsPath = Path.Combine(mod.FolderPath, ModScanner.StringsFileName);
            if (!File.Exists(stringsPath))
                return;

            try
            {
                Loc.AddStrings(File.ReadAllText(stringsPath, Encoding.UTF8), $"mod.{mod.Id}.");
            }
            catch (IOException exception)
            {
                Debug.LogWarning($"[ModService] 모드 '{mod.Id}'의 문장 파일을 읽을 수 없습니다: {exception.Message}");
            }
        }

        /// <summary>
        /// 복사가 덜 된 파일은 지문이 같아 보여도 크기가 다르므로, 비교용으로 번들 파일 크기의 합을 구한다.
        /// </summary>
        private static long BundleSize(ModInfo mod)
        {
            long totalSize = 0;
            foreach (string bundleName in new[] { mod.Manifest.assetBundle, mod.Manifest.sceneBundle })
            {
                string bundlePath = mod.GetFilePath(bundleName);
                if (bundlePath != null && File.Exists(bundlePath))
                    totalSize += new FileInfo(bundlePath).Length;
            }
            return totalSize;
        }
    }
}
