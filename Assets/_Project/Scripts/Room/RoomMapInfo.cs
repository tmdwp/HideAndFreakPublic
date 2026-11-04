using GuardVsMannequin.Mods;

namespace GuardVsMannequin.Room
{
    /// <summary>
    /// 호스트가 고른 맵의 정보. 게스트마다 맵 목록이 달라 번호로는 같은 맵을 가리킬 수 없으므로, 맵 ID와 이름·필요한 모드를 함께 보낸다.
    /// </summary>
    public struct RoomMapInfo
    {
        /// <summary>전역 고유 맵 ID.</summary>
        public string mapId;

        /// <summary>호스트 언어의 맵 이름. (게스트에게 없는 맵을 표시할 때 정리해서 사용)</summary>
        public string displayName;

        /// <summary>이 맵에 필요한 모드(맵의 모드 + 의존 모드). 내장 맵이면 비어 있다.</summary>
        public RequiredMod[] requiredMods;

        /// <summary>확인 차수. 맵을 바꾸거나 시작을 누를 때마다 늘어나고, 게스트는 이 값이 바뀌면 모드를 다시 확인한다. 0은 아직 정해지지 않음.</summary>
        public int revision;
    }
}
