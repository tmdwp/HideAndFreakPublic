using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GuardVsMannequin.Localization;
using UnityEngine;

namespace GuardVsMannequin.Mods
{
    /// <summary>
    /// 설치된 모드를 관리한다.
    /// 1. 모드 폴더를 읽고(<see cref="ModScanner"/>), ID 중복·켜기/끄기·의존성을 정한다.
    /// 2. 켜진 모드의 맵을 등록소(<see cref="ContentRegistry"/>)에, 문장을 <see cref="Loc"/>에 더한다.
    /// 3. 호스트는 고른 맵에 필요한 모드 목록을 만든다(<see cref="BuildRequirements"/>).
    ///    게스트는 <see cref="UseRequiredMods"/>로 그 모드만 이번 접속 동안 켠다.
    ///    (내가 꺼 둔 모드라도 필요하면 잠시 켜고, 필요 없는 모드는 잠시 끈다. 저장된 켜기/끄기 설정은 바꾸지 않는다)
    /// 게임 시작 때 한 번 읽고, 로비 "모드" 화면에서 켜고 끌 때와 게스트가 맵의 모드를 확인할 때 다시 읽는다.
    /// </summary>
    public sealed class ModService
    {
        private static ModService instance;

        private readonly List<ModInfo> mods = new();

        // 이번 접속 동안 켤 모드 ID(고른 맵에 필요한 모드). null이면 플레이어의 켜기/끄기 설정을 따른다.
        private HashSet<string> sessionModIds;

        /// <summary>게임 전체가 쓰는 모드 관리자.</summary>
        public static ModService Instance => instance ??= new ModService();

        /// <summary>설치된 모드 목록(오류·꺼진 모드 포함).</summary>
        public IReadOnlyList<ModInfo> Mods => mods;

        /// <summary>켜져 있고 오류가 없는 모드. (ID 순)</summary>
        public IEnumerable<ModInfo> ActiveMods => mods.Where(m => m.Status == ModStatus.Active).OrderBy(m => m.Id, StringComparer.Ordinal);

        /// <summary>모드 목록이 바뀌었을 때. (다시 읽은 뒤)</summary>
        public event Action Changed;

        /// <summary>
        /// 게임 시작 때 모드를 읽는다. (첫 씬이 열리기 전)
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadOnStart()
        {
            instance = null;
            Instance.Refresh();
        }

        /// <summary>
        /// 모드 폴더를 다시 읽고 등록소·문장을 갱신한다.
        /// </summary>
        public void Refresh()
        {
            mods.Clear();
            mods.AddRange(ModScanner.ScanAll());
            ResolveDuplicates();
            ApplyPreferences();
            ResolveDependencies();

            foreach (ModInfo mod in ActiveMods)
                LoadStrings(mod);

            ContentRegistry.Instance.SetModSources(ActiveMods.Select(mod => (IContentSource)new ModContentSource(mod)));

            foreach (ModInfo mod in mods.Where(m => m.Status == ModStatus.Error))
                Debug.LogWarning($"[ModService] 모드 '{mod.Id}'를 쓸 수 없습니다: {mod.Error} ({mod.FolderPath})");
            Debug.Log($"[ModService] 모드 {mods.Count}개 중 {ActiveMods.Count()}개 사용.");
            Changed?.Invoke();
        }

        /// <summary>
        /// 모드를 켜거나 끄고 다시 읽는다. (로비에서만)
        /// </summary>
        public void SetEnabled(string modId, bool enabled)
        {
            ModPreferences.SetEnabled(modId, enabled);
            sessionModIds = null;
            Refresh();
        }

        /// <summary>
        /// 맵에 필요한 모드(맵의 모드 + 의존 모드 전부) 목록을 만든다. 내장 맵이면 빈 목록. (호스트)
        /// </summary>
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
            result.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return result;
        }

        /// <summary>
        /// 필요한 모드만 이번 접속 동안 켠다. 폴더를 다시 읽으므로 방에 있는 동안 설치한 모드도 찾는다. (게스트)
        /// 씬 이름으로 맵을 찾으므로, 나만 가진 모드의 씬 이름이 겹치지 않도록 필요 없는 모드는 끈다.
        /// 쓸 수 없는 모드가 있으면 내 설정으로 되돌리고 그 목록을 돌려준다.
        /// </summary>
        /// <param name="required">호스트가 보낸 필요한 모드 목록. 내장 맵이면 비어 있다.</param>
        /// <returns>쓸 수 없는 모드 목록. 비어 있으면 플레이할 수 있다.</returns>
        public List<MissingMod> UseRequiredMods(IReadOnlyList<RequiredMod> required)
        {
            if (required == null || required.Count == 0)
            {
                ClearSessionMods();
                return new List<MissingMod>();
            }

            sessionModIds = new HashSet<string>(required.Take(RequiredModComparer.MaxRequiredMods).Select(mod => mod.id), StringComparer.Ordinal);
            Refresh();
            List<MissingMod> missing = RequiredModComparer.FindMissing(required, id => TryGet(id, out ModInfo mod) ? mod : null, BundleSize);
            if (missing.Count > 0 || required.Count > RequiredModComparer.MaxRequiredMods)
                ClearSessionMods();
            return missing;
        }

        /// <summary>
        /// 접속 동안 쓰던 모드 구성을 버리고 플레이어의 켜기/끄기 설정으로 돌아간다. (방을 나가거나 접속에 실패했을 때)
        /// </summary>
        public void ClearSessionMods()
        {
            if (sessionModIds == null)
                return;
            sessionModIds = null;
            Refresh();
        }

        /// <summary>
        /// 맵을 내 PC에서 쓸 수 있어 보이는지 확인한다. 꺼 둔 모드의 맵도 포함한다. (필요하면 그 판 동안 켜진다)
        /// </summary>
        public bool HasMap(string mapId)
        {
            return ContentRegistry.Instance.IndexOf(mapId) >= 0 || FindInstalledMap(mapId) != null;
        }

        /// <summary>
        /// 맵의 표시 이름을 찾는다. 없는 맵이면 null.
        /// </summary>
        public string FindMapName(string mapId)
        {
            ContentRegistry registry = ContentRegistry.Instance;
            if (registry.TryGetMap(registry.IndexOf(mapId), out MapEntry entry))
                return entry.DisplayName;

            MapManifest installed = FindInstalledMap(mapId);
            return installed == null ? null : string.IsNullOrEmpty(installed.name) ? installed.scene : installed.name;
        }

        /// <summary>
        /// 설치된 모드(오류 제외, 꺼진 모드 포함)에서 맵을 찾는다. 없으면 null.
        /// </summary>
        private MapManifest FindInstalledMap(string mapId)
        {
            foreach (ModInfo mod in mods)
            {
                if (mod.Status == ModStatus.Error || mod.Manifest?.maps == null)
                    continue;
                MapManifest map = mod.Manifest.maps.FirstOrDefault(m => m != null && m.id == mapId);
                if (map != null)
                    return map;
            }
            return null;
        }

        /// <summary>
        /// 모드 하나의 필요한 모드 항목을 만든다.
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
        /// ID로 모드를 찾는다.
        /// </summary>
        public bool TryGet(string modId, out ModInfo mod)
        {
            mod = mods.FirstOrDefault(m => m.Id == modId);
            return mod != null;
        }

        /// <summary>
        /// 같은 ID가 여러 폴더에 있으면 먼저 찾은 것(실행 파일 옆 폴더 우선)을 쓰고 나머지는 오류로 둔다.
        /// </summary>
        private void ResolveDuplicates()
        {
            var seen = new HashSet<string>();
            foreach (ModInfo mod in mods)
            {
                if (mod.Status == ModStatus.Error)
                    continue;
                if (!seen.Add(mod.Id))
                    mod.MarkError("같은 ID의 모드가 이미 있습니다. 한 곳에만 두세요.");
            }
        }

        /// <summary>
        /// 플레이어가 끈 모드를 꺼진 상태로 둔다. 맵에 필요한 모드를 쓰는 중이면 그 밖의 모드를 끈다.
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

            HashSet<string> disabled = ModPreferences.LoadDisabled();
            foreach (ModInfo mod in mods)
            {
                if (disabled.Contains(mod.Id))
                    mod.MarkDisabled();
            }
        }

        /// <summary>
        /// 필요한 모드가 없거나 꺼졌거나 오류인 모드를 오류로 둔다. 연쇄적으로 바뀌므로 바뀌지 않을 때까지 반복한다.
        /// </summary>
        private void ResolveDependencies()
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (ModInfo mod in mods.Where(m => m.Status == ModStatus.Active))
                {
                    string missing = mod.Manifest.dependencies.FirstOrDefault(id => !TryGet(id, out ModInfo dependency) || dependency.Status != ModStatus.Active);
                    if (missing == null)
                        continue;

                    mod.MarkError($"필요한 모드 '{missing}'가 없거나 꺼져 있습니다.");
                    changed = true;
                }
            }
        }

        /// <summary>
        /// 모드 문장 파일(strings.csv)이 있으면 "mod.&lt;ID&gt;."로 시작하는 문장을 더한다.
        /// </summary>
        private static void LoadStrings(ModInfo mod)
        {
            string path = Path.Combine(mod.FolderPath, ModScanner.StringsFileName);
            if (!File.Exists(path))
                return;

            try
            {
                Loc.AddStrings(File.ReadAllText(path, Encoding.UTF8), $"mod.{mod.Id}.");
            }
            catch (IOException exception)
            {
                Debug.LogWarning($"[ModService] 모드 '{mod.Id}'의 문장 파일을 읽을 수 없습니다: {exception.Message}");
            }
        }

        /// <summary>
        /// 모드 번들 파일 크기의 합. (복사가 덜 된 파일을 걸러내는 용도)
        /// </summary>
        private static long BundleSize(ModInfo mod)
        {
            long total = 0;
            foreach (string bundle in new[] { mod.Manifest.assetBundle, mod.Manifest.sceneBundle })
            {
                string path = mod.GetFilePath(bundle);
                if (path != null && File.Exists(path))
                    total += new FileInfo(path).Length;
            }
            return total;
        }
    }
}
