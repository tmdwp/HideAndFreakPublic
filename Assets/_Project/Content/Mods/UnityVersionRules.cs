using System;
using System.Text.RegularExpressions;

namespace GuardVsMannequin.Mods
{
    /// <summary>새 버전이나 다른 라인의 번들은 읽을 수 없거나 셰이더가 깨지므로 같은 라인의 같거나 옛 패치만 허용한다.</summary>
    public static class UnityVersionRules
    {
        /// <summary>불러온 뒤 깨지면 원인을 알기 어려우므로 미리 막고 다시 내보낼 범위를 이유에 적는다.</summary>
        /// <param name="modUnity">모드를 만든 Unity 버전.</param>
        /// <param name="gameUnity">게임의 Unity 버전(읽을 수 없으면 확인하지 않음).</param>
        /// <param name="problem">호환되지 않을 때의 이유.</param>
        public static bool IsCompatible(string modUnity, string gameUnity, out string problem)
        {
            problem = null;
            int[] modVersion = Parse(modUnity);
            int[] gameVersion = Parse(gameUnity);
            if (modVersion == null)
            {
                problem = $"모드를 만든 Unity 버전을 알 수 없습니다({modUnity}). 다시 내보내세요.";
                return false;
            }
            if (gameVersion == null)
                return true;

            string versionLine = $"{gameVersion[0]}.{gameVersion[1]}";
            if (modVersion[0] != gameVersion[0] || modVersion[1] != gameVersion[1])
            {
                problem = $"다른 Unity 버전 라인({modUnity})으로 만든 모드입니다. 게임과 같은 {versionLine}.x 중 {gameUnity} 이하로 다시 내보내야 합니다.";
                return false;
            }
            if (Compare(modVersion, gameVersion) > 0)
            {
                problem = $"게임({gameUnity})보다 새 Unity({modUnity})로 만든 모드입니다. {versionLine}.x 중 {gameUnity} 이하로 다시 내보내야 합니다.";
                return false;
            }
            return true;
        }

        /// <summary>모드 제작자에게 설치할 Unity를 안내하도록 허용 범위를 문구로 만든다.</summary>
        public static string DescribeRange(string gameUnity)
        {
            int[] gameVersion = Parse(gameUnity);
            return gameVersion == null ? gameUnity : $"{gameVersion[0]}.{gameVersion[1]}.0 ~ {gameUnity}";
        }

        /// <summary>문자열 비교로는 "6000.4.10"이 "6000.4.2"보다 작게 나오므로 숫자 배열로 읽는다.</summary>
        private static int[] Parse(string version)
        {
            Match match = Regex.Match(version ?? string.Empty, @"^(\d+)\.(\d+)\.(\d+)(?:([abfp])(\d+))?");
            if (!match.Success)
                return null;

            int releaseStage = match.Groups[4].Success ? "abfp".IndexOf(match.Groups[4].Value[0]) : 2;
            int releaseNumber = match.Groups[5].Success ? int.Parse(match.Groups[5].Value) : 0;
            return new[] { int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value), releaseStage, releaseNumber };
        }

        /// <summary>앞자리일수록 중요하므로 앞에서부터 처음 다른 자리로 순서를 정한다.</summary>
        private static int Compare(int[] first, int[] second)
        {
            for (int i = 0; i < Math.Min(first.Length, second.Length); i++)
            {
                if (first[i] != second[i])
                    return first[i].CompareTo(second[i]);
            }
            return 0;
        }
    }
}
