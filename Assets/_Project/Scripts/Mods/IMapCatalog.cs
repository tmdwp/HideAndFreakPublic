using System.Collections.Generic;

namespace GuardVsMannequin.Mods
{
    /// <summary>
    /// 고를 수 있는 맵 목록(읽기 전용). 호스트에서는 목록의 번호가 룸 설정의 맵 번호(GameSettings.mapId)다.
    /// 같은 모드 세트를 가진 PC끼리만 함께 플레이하므로(모드 일치 확인) 모든 PC에서 번호가 같은 맵을 가리킨다.
    /// </summary>
    public interface IMapCatalog
    {
        /// <summary>맵 목록. (내장 맵 → 모드 맵 순)</summary>
        IReadOnlyList<MapEntry> Maps { get; }
    }
}
