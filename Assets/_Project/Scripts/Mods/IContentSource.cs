using System.Collections.Generic;

namespace GuardVsMannequin.Mods
{
    /// <summary>내장 콘텐츠와 모드가 같은 방식으로 등록되도록 맵을 제공하는 출처를 인터페이스로 둔다.</summary>
    public interface IContentSource
    {
        /// <summary>내장: "builtin", 모드: 모드 ID.</summary>
        string SourceId { get; }

        /// <summary>모든 PC에서 맵 번호가 같도록 출처 안의 순서를 지켜서 준다.</summary>
        IReadOnlyList<MapEntry> LoadMaps();
    }
}
