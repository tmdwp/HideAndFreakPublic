using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Mods
{
    /// <summary>PC끼리 맵 ID로 같은 맵을 가리키도록 내장 맵과 켜진 모드 맵을 정해진 순서로 모은다.</summary>
    public sealed class ContentRegistry : IMapCatalog
    {
        private static ContentRegistry instance;

        private readonly IContentSource builtIn;
        private readonly List<IContentSource> modSources = new();
        private readonly List<MapEntry> maps = new();
        private bool isLoaded;

        /// <summary>씬 참조 없이 어디서든 같은 목록을 보도록 하나만 두고, 처음 쓸 때 내장 콘텐츠로 만든다.</summary>
        public static ContentRegistry Instance => instance ??= new ContentRegistry(new BuiltInContentSource());

        /// <summary>룸 UI의 맵 선택 등이 바뀐 목록을 다시 그리도록 알린다.</summary>
        public event Action Changed;

        /// <summary>테스트에서 가짜 내장 콘텐츠를 넣을 수 있도록 출처를 밖에서 받는다.</summary>
        public ContentRegistry(IContentSource builtIn)
        {
            this.builtIn = builtIn;
        }

        /// <summary>
        /// 도메인 재로드를 끈 경우 이전 플레이의 목록이 남으므로 플레이 모드에 들어갈 때 새로 만들게 한다.
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
        /// PC마다 맵 순서가 같도록 모드 출처를 ID 순으로 정렬한 뒤 목록을 다시 만든다.
        /// </summary>
        public void SetModSources(IEnumerable<IContentSource> sources)
        {
            modSources.Clear();
            modSources.AddRange(sources);
            modSources.Sort((first, second) => string.CompareOrdinal(first.SourceId, second.SourceId));
            Reload();
        }

        /// <summary>
        /// 저장된 번호가 범위를 벗어나도 예외가 나지 않도록 확인하고 꺼낸다.
        /// </summary>
        public bool TryGetMap(int index, out MapEntry map)
        {
            IReadOnlyList<MapEntry> mapList = Maps;
            map = index >= 0 && index < mapList.Count ? mapList[index] : null;
            return map != null;
        }

        /// <summary>호스트가 보낸 맵 ID를 내 목록의 번호로 바꾼다.</summary>
        public int IndexOf(string mapId)
        {
            IReadOnlyList<MapEntry> mapList = Maps;
            for (int i = 0; i < mapList.Count; i++)
            {
                if (mapList[i].Id == mapId)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 모드를 아직 읽지 않았어도 내장 맵은 바로 쓸 수 있도록 처음 쓸 때 한 번 목록을 만든다.
        /// </summary>
        private void EnsureLoaded()
        {
            if (!isLoaded)
                Reload();
        }

        /// <summary>
        /// 모드가 내장 맵을 덮어쓰지 못하도록 내장 → 모드 순으로 모으고, 맵 ID가 겹치면 먼저 온 것을 쓰고 경고한다.
        /// </summary>
        private void Reload()
        {
            isLoaded = true;
            maps.Clear();
            var seenIds = new HashSet<string>();
            AddFrom(builtIn, seenIds);
            foreach (IContentSource source in modSources)
                AddFrom(source, seenIds);
            Changed?.Invoke();
        }

        /// <summary>
        /// 어느 모드의 맵이 빠졌는지 알 수 있도록 겹친 맵은 출처 이름과 함께 경고한다.
        /// </summary>
        private void AddFrom(IContentSource source, HashSet<string> seenIds)
        {
            foreach (MapEntry map in source.LoadMaps())
            {
                if (seenIds.Add(map.Id))
                    maps.Add(map);
                else
                    Debug.LogWarning($"[ContentRegistry] 맵 ID '{map.Id}'가 겹쳐 '{source.SourceId}'의 맵을 건너뜁니다.");
            }
        }
    }
}
