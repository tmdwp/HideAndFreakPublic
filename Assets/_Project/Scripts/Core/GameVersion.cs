using UnityEngine;

namespace GuardVsMannequin.Core
{
    /// <summary>네트워크 형식이 다르면 게임이 깨지므로 버전이 다른 플레이어는 방에 들어갈 수 없게 하는 게임 버전.</summary>
    public static class GameVersion
    {
        /// <summary>빌드 버전을 올리지 않고 네트워크 형식만 바꿨을 때 섞여 접속하지 않도록 올리는 번호.</summary>
        public const int Protocol = 10;

        /// <summary>세 값 중 하나라도 다르면 호환되지 않으므로 하나의 문자열로 묶어 비교한다.</summary>
        public static string Current => $"{Application.version}-p{Protocol}-t{GameTuning.Fingerprint}";
    }
}
