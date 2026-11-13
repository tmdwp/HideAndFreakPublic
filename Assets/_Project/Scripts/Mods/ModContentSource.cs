using System.Collections.Generic;

namespace GuardVsMannequin.Mods
{
    /// <summary>켜진 모드 하나가 등록소에 제공하는 맵.</summary>
    public sealed class ModContentSource : IContentSource
    {
        private readonly ModInfo mod;

        /// <summary>등록소가 모드와 내장 콘텐츠를 같은 방식으로 다루도록 모드를 감싼다.</summary>
        /// <param name="mod">켜져 있고 오류가 없는 모드.</param>
        public ModContentSource(ModInfo mod)
        {
            this.mod = mod;
        }

        /// <inheritdoc />
        public string SourceId => mod.Id;

        /// <inheritdoc />
        public IReadOnlyList<MapEntry> LoadMaps()
        {
            // 한 모드의 맵들은 같은 씬 번들에 들어 있어서 경로를 한 번만 만든다.
            string sceneBundlePath = mod.GetFilePath(mod.Manifest.sceneBundle);
            var entries = new List<MapEntry>(mod.Manifest.maps.Count);
            foreach (MapManifest map in mod.Manifest.maps)
                entries.Add(new MapEntry(map, mod.Id, sceneBundlePath));
            return entries;
        }
    }
}
