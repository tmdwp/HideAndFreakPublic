using System;
using System.Collections.Generic;

namespace GuardVsMannequin.Network
{
    /// <summary>로비 UI가 Steam에 의존하지 않도록 공개방 찾기를 인터페이스로 둔다.</summary>
    public interface IRoomBrowser
    {
        /// <summary>새로 고침 버튼을 막고 진행 표시를 보여 주려고 쓴다.</summary>
        bool IsRefreshing { get; }

        /// <summary>새로 검색하는 동안에도 이전 목록을 보여 줄 수 있도록 마지막 결과를 남겨 둔다.</summary>
        IReadOnlyList<RoomListing> Rooms { get; }

        event Action ListUpdated;

        /// <summary>인자는 안내 문구 key.</summary>
        event Action<string> Failed;

        /// <summary>
        /// Steam 검색 요청 한도를 넘지 않도록 진행 중이거나 방금 검색했으면 시작하지 않는다.
        /// </summary>
        /// <returns>새 검색을 시작했는지 여부.</returns>
        bool Refresh();
    }
}
