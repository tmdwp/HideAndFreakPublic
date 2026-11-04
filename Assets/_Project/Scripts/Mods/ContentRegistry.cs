using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Mods
{
    /// <summary>
    /// 게임이 쓸 수 있는 맵 목록을 모은다. 내장 콘텐츠가 먼저, 그 뒤에 켜진 모드의 맵이 모드 ID 순으로 붙는다.
    /// 목록의 번호는 호스트의 룸 설정에만 쓰고(게스트마다 목록이 다르다), PC끼리는 맵 ID(문자열)로 같은 맵을 가리킨다.
    /// 모드 목록은 로비에서 켜고 끌 때와 게스트가 맵의 모드를 확인할 때 바뀐다(<see cref="SetModSources"/>). 호스트는 방에 들어간 뒤 바뀌지 않는다.
    /// </summary>
    public sealed class ContentRegistry : IMapCatalog
    {
        private static ContentRegistry instance;

        private readonly IContentSource builtIn;
        private readonly List<IContentSource> modSources = new();
        private readonly List<MapEntry> maps = new();
        private bool loaded;

        /// <summary>게임 전체가 쓰는 등록소. 처음 쓸 때 내장 콘텐츠로 만든다.</summary>
        public static ContentRegistry Instance => instance ??= new ContentRegistry(new BuiltInContentSource());

        /// <summary>맵 목록이 바뀌었을 때. (모드를 켜고 끈 뒤)</summary>
        public event Action Changed;

        /// <param name="builtIn">내장 콘텐츠 출처.</param>
        public ContentRegistry(IContentSource builtIn)
        {
            this.builtIn = builtIn;
        }

        /// <summary>
        /// 플레이 모드에 들어갈 때 등록소를 새로 만든다. (도메인 재로드를 끈 경우 이전 목록이 남지 않도록)
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            instance = null;
        }

        /// <inheritdoc />
        public IReadOnlyList<MapEntry> Maps
        {
            get
            {
                EnsureLoaded();
                return maps;
            }
        }

        /// <summary>
        /// 켜진 모드의 콘텐츠 출처를 정하고 목록을 다시 만든다.
        /// </summary>
        public void SetModSources(IEnumerable<IContentSource> sources)
        {
            modSources.Clear();
            modSources.AddRange(sources);
            modSources.Sort((a, b) => string.CompareOrdinal(a.SourceId, b.SourceId));
            Reload();
        }

        /// <summary>
        /// 번호에 해당하는 맵을 찾는다.
        /// </summary>
        public bool TryGetMap(int index, out MapEntry map)
        {
            IReadOnlyList<MapEntry> list = Maps;
            map = index >= 0 && index < list.Count ? list[index] : null;
            return map != null;
        }

        /// <summary>
        /// 맵 ID의 번호를 찾는다. 없으면 -1.
        /// </summary>
        public int IndexOf(string mapId)
        {
            IReadOnlyList<MapEntry> list = Maps;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Id == mapId)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 처음 쓸 때 한 번 목록을 만든다.
        /// </summary>
        private void EnsureLoaded()
        {
            if (!loaded)
                Reload();
        }

        /// <summary>
        /// 내장 → 모드 순으로 맵을 모은다. 맵 ID가 겹치면 먼저 온 것을 쓰고 경고한다.
        /// </summary>
        private void Reload()
        {
            loaded = true;
            maps.Clear();
            var ids = new HashSet<string>();
            AddFrom(builtIn, ids);
            foreach (IContentSource source in modSources)
                AddFrom(source, ids);
            Changed?.Invoke();
        }

        /// <summary>
        /// 출처 하나의 맵을 목록에 더한다.
        /// </summary>
        private void AddFrom(IContentSource source, HashSet<string> ids)
        {
            foreach (MapEntry map in source.LoadMaps())
            {
                if (ids.Add(map.Id))
                    maps.Add(map);
                else
                    Debug.LogWarning($"[ContentRegistry] 맵 ID '{map.Id}'가 겹쳐 '{source.SourceId}'의 맵을 건너뜁니다.");
            }
        }
    }
}
