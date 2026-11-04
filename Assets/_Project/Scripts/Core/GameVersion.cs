using UnityEngine;

namespace GuardVsMannequin.Core
{
    /// <summary>
    /// 게임 버전. 호스트와 버전이 다른 플레이어는 방에 들어갈 수 없다. (네트워크 데이터 형식이 달라 게임이 깨지므로)
    /// 버전 = 빌드 버전(Player Settings > Version) + 네트워크 규약 번호 + 조정 파일 지문.
    /// - 빌드를 배포할 때는 Player Settings의 Version을 올린다. (같은 버전 빌드끼리만 함께 플레이)
    /// - 네트워크로 주고받는 것(동기화 값, RPC, 네트워크 프리팹, 맵 목록)을 바꿨는데 빌드 버전을 올리지 않았다면
    ///   <see cref="Protocol"/>을 1 올린다.
    /// - 조정 파일(game_tuning.csv) 값이 다르면 지문(<see cref="GameTuning.Fingerprint"/>)이 달라져 자동으로 막힌다.
    /// </summary>
    public static class GameVersion
    {
        /// <summary>네트워크 규약 번호.</summary>
        public const int Protocol = 9;

        /// <summary>이 게임의 버전 문자열. (예: 0.3.0-p2-t1a2b3c4d)</summary>
        public static string Current => $"{Application.version}-p{Protocol}-t{GameTuning.Fingerprint}";
    }
}
