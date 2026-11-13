using GuardVsMannequin.Localization;

namespace GuardVsMannequin.Mods
{
    /// <summary>등록소에 오른 맵 하나와 그 출처.</summary>
    public sealed class MapEntry
    {
        /// <summary>map.json 내용.</summary>
        public MapManifest Manifest { get; }

        /// <summary>필요한 모드 목록을 만들려고 기억하는 출처 모드 ID.</summary>
        public string SourceId { get; }

        /// <summary>씬을 불러오기 전에 번들을 열어야 해서 기억하는 경로.</summary>
        public string BundlePath { get; }

        /// <summary>PC마다 목록 번호가 달라도 같은 맵을 가리키는 전역 고유 ID.</summary>
        public string Id => Manifest.id;

        public string SceneName => Manifest.scene;

        /// <summary>내장 맵은 번들 없이 빌드 설정의 씬을 쓰므로 번들 경로가 없는지로 구분한다.</summary>
        public bool IsBuiltIn => BundlePath == null;

        /// <summary>내장 맵과 모드 맵을 같은 방식으로 다루려고 출처 정보를 함께 묶는다.</summary>
        /// <param name="bundlePath">내장 맵이면 null.</param>
        public MapEntry(MapManifest manifest, string sourceId, string bundlePath)
        {
            Manifest = manifest;
            SourceId = sourceId;
            BundlePath = bundlePath;
        }

        /// <summary>언어를 바꾸면 바로 반영되도록 매번 찾고, 번역이 없으면 빈 이름이 되지 않게 씬 이름까지 쓴다.</summary>
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(Manifest.nameKey) && Loc.HasKey(Manifest.nameKey))
                    return Loc.Get(Manifest.nameKey);
                return string.IsNullOrEmpty(Manifest.name) ? Manifest.scene : Manifest.name;
            }
        }
    }
}
