using GuardVsMannequin.Mods;

namespace GuardVsMannequin.Room
{
    /// <summary>게스트마다 맵 목록이 달라 번호로는 같은 맵을 가리킬 수 없으므로 맵 ID와 이름·필요한 모드를 함께 보낸다.</summary>
    public struct RoomMapInfo
    {
        /// <summary>목록 번호는 PC마다 다르므로 전역 고유 ID로 맵을 가리킨다.</summary>
        public string mapId;

        /// <summary>게스트에게 없는 맵도 이름은 보여 줄 수 있도록 호스트 언어의 이름을 함께 보낸다.</summary>
        public string displayName;

        /// <summary>게스트가 무엇을 받아야 하는지 알도록 맵의 모드와 의존 모드를 담는다.</summary>
        public RequiredMod[] requiredMods;

        /// <summary>같은 맵을 다시 골라도 게스트가 모드를 다시 확인하도록 늘리는 차수.</summary>
        public int revision;
    }
}
