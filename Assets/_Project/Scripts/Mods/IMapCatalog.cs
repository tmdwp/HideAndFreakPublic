using System.Collections.Generic;

namespace GuardVsMannequin.Mods
{
    /// <summary>같은 모드 세트의 PC끼리만 플레이하므로 번호가 같은 맵을 가리키는 읽기 전용 맵 목록.</summary>
    public interface IMapCatalog
    {
        /// <summary>번호가 PC마다 같도록 항상 내장 맵 → 모드 맵 순으로 둔다.</summary>
        IReadOnlyList<MapEntry> Maps { get; }
    }
}
