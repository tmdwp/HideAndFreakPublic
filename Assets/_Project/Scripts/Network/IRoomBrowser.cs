using System;
using System.Collections.Generic;

namespace GuardVsMannequin.Network
{
    /// <summary>
    /// 공개방 찾기. 로비 UI는 이 인터페이스에만 의존한다(DIP).
    /// </summary>
    public interface IRoomBrowser
    {
        /// <summary>검색 결과를 기다리는 중인지 여부.</summary>
        bool IsRefreshing { get; }

        /// <summary>마지막 검색 결과.</summary>
        IReadOnlyList<RoomListing> Rooms { get; }

        /// <summary>검색이 끝났을 때.</summary>
        event Action ListUpdated;

        /// <summary>검색이 실패했을 때. (안내 문구 key)</summary>
        event Action<string> Failed;

        /// <summary>
        /// 검색을 시작한다. 진행 중이거나 방금 검색했으면 시작하지 않는다.
        /// </summary>
        /// <returns>새 검색을 시작했는지 여부.</returns>
        bool Refresh();
    }
}
