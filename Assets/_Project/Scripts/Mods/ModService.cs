using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GuardVsMannequin.Localization;
using UnityEngine;

namespace GuardVsMannequin.Mods
{
    /// <summary>
    /// 설치된 모드를 관리한다.
    /// 1. 모드 폴더를 읽고(<see cref="ModScanner"/>), ID 중복·켜기/끄기·의존성을 정한다.
    /// 2. 켜진 모드의 맵을 등록소(<see cref="ContentRegistry"/>)에, 문장을 <see cref="Loc"/>에 더한다.
    /// 3. 켜진 모드 목록의 지문(<see cref="ModSetHash"/>)을 만든다. 방에 들어가려면 호스트와 지문이 같아야 한다.
    ///    접속자는 <see cref="TryMatchHost"/>로 이번 접속 동안만 호스트가 켠 모드 구성을 따른다.
    ///    (호스트가 끈 모드는 잠시 끄고, 내가 끈 모드라도 호스트가 켰으면 잠시 켠다. 저장된 켜기/끄기 설정은 바꾸지 않는다)
    /// 게임 시작 때 한 번 읽고, 로비 "모드" 화면에서 켜고 끌 때 다시 읽는다. (방에 있는 동안은 바꾸지 않는다)
    /// </summary>
    public sealed class ModService
    {
        private static ModService instance;

        private readonly List<ModInfo> mods = new();

        // 이번 접속 동안 켤 모드 ID(호스트의 구성). null이면 플레이어의 켜기/끄기 설정을 따른다.
        private HashSet<string> sessionModIds;

        /// <summary>게임 전체가 쓰는 모드 관리자.</summary>
        public static ModService Instance => instance ??= new ModService();

        /// <summary>설치된 모드 목록(오류·꺼진 모드 포함).</summary>
        public IReadOnlyList<ModInfo> Mods => mods;

        /// <summary>켜져 있고 오류가 없는 모드. (ID 순)</summary>
        public IEnumerable<ModInfo> ActiveMods => mods.Where(m => m.Status == ModStatus.Active).OrderBy(m => m.Id, StringComparer.Ordinal);

        /// <summary>켜진 모드 목록 설명. (예: "author.maps@1.0.0#ab12cd34, ...", 없으면 빈 문자열)</summary>
        public string ModSetDescription { get; private set; } = string.Empty;

        /// <summary>
        /// 켜진 모드 ID 목록. (예: "author.maps, author.skins", 없으면 빈 문자열)
        /// Steam 로비 데이터처럼 크기 제한이 있는 곳에 쓴다. 버전·파일 비교는 접속 후 서버가 지문과 <see cref="ModSetDescription"/>으로 한다.
        /// </summary>
        public string ModSetIds { get; private set; } = string.Empty;

        /// <summary>켜진 모드 목록 + 내장 맵 목록의 지문(8자리 16진수). 호스트와 같아야 입장할 수 있다.</summary>
        public string ModSetHash { get; private set; } = string.Empty;

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
        /// 모드 폴더를 다시 읽고 등록소·문장·지문을 갱신한다.
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
            UpdateSignature();

            foreach (ModInfo mod in mods.Where(m => m.Status == ModStatus.Error))
                Debug.LogWarning($"[ModService] 모드 '{mod.Id}'를 쓸 수 없습니다: {mod.Error} ({mod.FolderPath})");
            Debug.Log($"[ModService] 모드 {mods.Count}개 중 {ActiveMods.Count()}개 사용. 지문 {ModSetHash}");
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
        /// 호스트가 켠 모드 구성에 맞춘다. 지문이 이미 같으면 그대로 두고,
        /// 다르면 이번 접속 동안만 호스트의 모드 ID 목록대로 켜고 끈 뒤 지문을 다시 비교한다.
        /// 호스트의 모드가 없거나 버전·파일이 달라 지문이 맞지 않으면 원래 구성으로 되돌리고 false.
        /// </summary>
        /// <param name="hostModList">호스트의 <see cref="ModSetDescription"/>("author.maps@1.0.0#ab12cd34, ...") 또는 <see cref="ModSetIds"/>("author.maps, ...").</param>
        /// <param name="hostModHash">호스트의 <see cref="ModSetHash"/>.</param>
        public bool TryMatchHost(string hostModList, string hostModHash)
        {
            if (hostModHash == ModSetHash)
                return true;

            sessionModIds = ParseModIds(hostModList);
            Refresh();
            if (ModSetHash == hostModHash)
            {
                Debug.Log($"[ModService] 이번 접속 동안 호스트의 모드 구성을 따릅니다: {(hostModList?.Length > 0 ? hostModList : "없음")}");
                return true;
            }

            ClearSessionMods();
            return false;
        }

        /// <summary>
        /// 접속 동안 쓰던 호스트 모드 구성을 버리고 플레이어의 켜기/끄기 설정으로 돌아간다. (방을 나가거나 접속에 실패했을 때)
        /// </summary>
        public void ClearSessionMods()
        {
            if (sessionModIds == null)
                return;
            sessionModIds = null;
            Refresh();
        }

        /// <summary>
        /// 호스트가 켠 모드 중 내 PC에서 맞출 수 없는 모드(없음, 버전·파일 다름, 오류)를 찾는다. (접속 거부 안내용)
        /// </summary>
        /// <param name="hostModList">호스트의 <see cref="ModSetDescription"/>.</param>
        public List<MissingMod> FindMissingFor(string hostModList)
        {
            return ModSetComparer.FindMissing(hostModList, id => TryGet(id, out ModInfo mod) ? mod : null);
        }

        /// <summary>
        /// 모드 목록 설명("id@버전#지문, ...")에서 모드 ID만 뽑는다.
        /// </summary>
        private static HashSet<string> ParseModIds(string modList)
        {
            return new HashSet<string>(ModSetComparer.Parse(modList).Select(entry => entry.id), StringComparer.Ordinal);
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
        /// 플레이어가 끈 모드를 꺼진 상태로 둔다. 호스트 구성을 따르는 중이면 호스트가 켜지 않은 모드를 끈다.
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
        /// 켜진 모드(ID·버전·번들 지문·번들 크기)와 맵 목록(ID 순서)으로 지문을 만든다.
        /// 맵 목록을 포함하므로 내장 맵 목록 파일(maps.json)을 고친 PC도 걸러진다.
        /// </summary>
        private void UpdateSignature()
        {
            var parts = new List<string>();
            foreach (ModInfo mod in ActiveMods)
                parts.Add($"{mod.Id}@{mod.Manifest.version}#{mod.Manifest.contentHash}:{BundleSize(mod)}");
            ModSetDescription = string.Join(", ", parts.Select(p => p.Split(':')[0]));
            ModSetIds = string.Join(", ", ActiveMods.Select(mod => mod.Id));

            string maps = string.Join("|", ContentRegistry.Instance.Maps.Select(m => m.Id));
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join(";", parts) + "/" + maps));
            ModSetHash = BitConverter.ToString(hash, 0, 4).Replace("-", string.Empty).ToLowerInvariant();
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
