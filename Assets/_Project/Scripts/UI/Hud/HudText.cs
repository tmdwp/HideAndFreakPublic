using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using UnityEngine;

namespace GuardVsMannequin.UI
{
    /// <summary>HUD 영역들이 함께 쓰는 문구 도우미.</summary>
    public static class HudText
    {
        /// <summary>목표 안내 문구 종류.</summary>
        public const string ObjectivePart = "objective";

        /// <summary>역할 안내 문구 종류: 좌클릭 동작.</summary>
        public const string PrimaryPart = "primary";

        /// <summary>역할 안내 문구 종류: 우클릭 동작.</summary>
        public const string SecondaryPart = "secondary";

        /// <summary>역할 안내 문구 종류: Shift 동작. (사냥 시간 구울은 달리기 대신 은신)</summary>
        public const string SprintPart = "sprint";

        /// <summary>역할 안내 문구 종류: 단계가 바뀔 때 화면 가운데에 띄우는 한 줄.</summary>
        public const string AnnouncePart = "announce";

        /// <summary>
        /// 역할을 추가해도 HUD를 고치지 않도록 이름 문구 key는 역할 정의에서 꺼낸다.
        /// </summary>
        public static string GetRoleName(PlayerRole role)
        {
            return Loc.Get(RoleDefinitions.Get(role).NameKey);
        }

        /// <summary>사냥 시간에는 같은 역할도 할 일이 바뀌므로 단계별로 다른 문구를 고른다.</summary>
        /// <param name="part">안내 문구 종류.</param>
        public static string GetRoleText(PlayerRole role, string part, bool isHunting)
        {
            string keyPrefix = RoleDefinitions.Get(role).GuideKeyPrefix;
            if (string.IsNullOrEmpty(keyPrefix))
                return string.Empty;
            return Loc.Get($"{keyPrefix}.{part}{(isHunting ? ".hunting" : ".playing")}");
        }

        /// <summary>
        /// 대기 상태는 아직 판이 시작되지 않아 보여 줄 이름이 없으므로 빈 문자열을 돌려준다.
        /// </summary>
        public static string GetPhaseName(GameState state)
        {
            return state switch
            {
                GameState.Preparation => Loc.Get("hud.phase.preparation"),
                GameState.Playing => Loc.Get("hud.phase.playing"),
                GameState.Hunting => Loc.Get("hud.phase.hunting"),
                GameState.Ended => Loc.Get("hud.phase.ended"),
                _ => string.Empty
            };
        }

        /// <summary>조금 남았는데 "0:00"으로 보여 끝난 것으로 오해하지 않도록 올림한다.</summary>
        public static string FormatTime(float seconds)
        {
            int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return $"{totalSeconds / 60}:{totalSeconds % 60:00}";
        }
    }
}
