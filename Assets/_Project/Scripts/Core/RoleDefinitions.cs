using System;
using System.Collections.Generic;

namespace GuardVsMannequin.Core
{
    /// <summary>이번 판 설정에서 역할에 맞는 값만 골라 담은 역할 능력치.</summary>
    public readonly struct RoleStats
    {
        public readonly int MaxHealth;

        /// <summary>사냥 시간 배율 적용 전 이동 속도(m/s).</summary>
        public readonly float MoveSpeed;

        /// <summary>가드는 총, 마네킹은 사냥 시간 공격에 쓰인다.</summary>
        public readonly int AttackDamage;

        /// <summary>설정 구조체에서 역할마다 다른 필드를 고르는 일을 한 번만 하도록 값을 묶어 둔다.</summary>
        public RoleStats(int maxHealth, float moveSpeed, int attackDamage)
        {
            MaxHealth = maxHealth;
            MoveSpeed = moveSpeed;
            AttackDamage = attackDamage;
        }
    }

    /// <summary>스폰 위치를 정하는 방식.</summary>
    public enum SpawnPlacement : byte
    {
        /// <summary>맵에 놓인 역할별 스폰 지점을 차례로 쓴다.</summary>
        FixedPoints = 0,

        /// <summary>맵 안에서 갈 수 있는 무작위 위치. 판 시작 검은 안개가 시야를 가려서 마네킹 스폰과의 거리는 따지지 않는다.</summary>
        RandomInMap = 1
    }

    /// <summary>
    /// HUD 목표 영역에서 목표 문구 아래에 보여줄 남은 수.
    /// </summary>
    public enum ObjectiveCounter : byte
    {
        /// <summary>표시하지 않음.</summary>
        None = 0,

        /// <summary>남은 전원 장치 수.</summary>
        RemainingGenerators = 1,

        /// <summary>살아 있는 가드 수.</summary>
        RemainingGuards = 2,

        /// <summary>살아 있는 마네킹 수.</summary>
        RemainingMannequins = 3
    }

    /// <summary>
    /// 역할 하나의 정의: 표시 이름·안내 문구 key, 소속 진영, 단계별 이동 속도 배율, HUD 목표 표시, 설정에서 능력치를 고르는 방법.
    /// </summary>
    public sealed class RoleDefinition
    {
        private readonly Func<GameSettings, RoleStats> selectStats;
        private readonly Func<float> huntSpeedMultiplier;
        private readonly ObjectiveCounter playingCounter;
        private readonly ObjectiveCounter huntingCounter;

        public PlayerRole Role { get; }

        /// <summary>strings.csv의 key.</summary>
        public string NameKey { get; }

        /// <summary>역할·단계마다 key를 따로 적지 않도록 앞부분만 두고 뒤를 붙인다.</summary>
        public string GuideKeyPrefix { get; }

        /// <summary>역할과 진영이 1:1이 아닐 수 있어서 따로 둔다.</summary>
        public MatchWinner Side { get; }

        public SpawnPlacement Placement { get; }

        /// <summary>조정 파일 값을 다시 읽어도 반영되도록 캐시하지 않고 매번 읽는다.</summary>
        public float HuntSpeedMultiplier => huntSpeedMultiplier();

        /// <summary>역할마다 다른 값을 한 객체에 모아, 다른 코드가 역할로 분기하지 않게 한다.</summary>
        /// <param name="huntSpeedMultiplier">조정 파일 값을 매번 읽도록 값 대신 받는 함수.</param>
        public RoleDefinition(PlayerRole role, string nameKey, string guideKeyPrefix, MatchWinner side, Func<float> huntSpeedMultiplier,
            ObjectiveCounter playingCounter, ObjectiveCounter huntingCounter, SpawnPlacement placement, Func<GameSettings, RoleStats> selectStats)
        {
            Placement = placement;
            Role = role;
            NameKey = nameKey;
            GuideKeyPrefix = guideKeyPrefix;
            Side = side;
            this.huntSpeedMultiplier = huntSpeedMultiplier;
            this.playingCounter = playingCounter;
            this.huntingCounter = huntingCounter;
            this.selectStats = selectStats;
        }

        /// <summary>
        /// 관전처럼 능력치가 없는 역할도 호출할 수 있도록, 고르는 함수가 없으면 기본값을 준다.
        /// </summary>
        public RoleStats GetStats(GameSettings settings)
        {
            return selectStats != null ? selectStats(settings) : default;
        }

        /// <summary>
        /// 이동 코드와 서버 검증이 같은 속도를 쓰도록 사냥 시간 배율 적용을 여기서 한다.
        /// </summary>
        public float GetMoveSpeed(GameSettings settings, GameState state)
        {
            float multiplier = state == GameState.Hunting ? HuntSpeedMultiplier : 1f;
            return GetStats(settings).MoveSpeed * multiplier;
        }

        /// <summary>
        /// 같은 역할이라도 단계에 따라 신경 쓸 대상이 바뀌어서, HUD가 보여 줄 남은 수를 단계별로 고른다.
        /// </summary>
        public ObjectiveCounter GetObjectiveCounter(bool isHunting)
        {
            return isHunting ? huntingCounter : playingCounter;
        }

        /// <summary>
        /// 진영이 없는 역할(관전 등)이 None 승자와 같다고 잘못 판정되지 않도록 먼저 거른다.
        /// </summary>
        public bool IsWinner(MatchWinner winner)
        {
            return Side != MatchWinner.None && Side == winner;
        }
    }

    /// <summary>다른 코드가 역할로 분기하지 않도록 역할별 정의를 한곳에 모은다.</summary>
    public static class RoleDefinitions
    {
        private static readonly RoleDefinition None =
            new(PlayerRole.None, "role.none", string.Empty, MatchWinner.None, () => 1f, ObjectiveCounter.None, ObjectiveCounter.None,
                SpawnPlacement.FixedPoints, null);

        private static readonly Dictionary<PlayerRole, RoleDefinition> definitions = new()
        {
            // 가드: 탐색은 "포획", 사냥은 "생존".
            [PlayerRole.Guard] = new RoleDefinition(PlayerRole.Guard, "role.guard", "hud.guide.guard", MatchWinner.Guard, () => 1f,
                ObjectiveCounter.None, ObjectiveCounter.None, SpawnPlacement.RandomInMap,
                settings => new RoleStats(settings.guardMaxHealth, settings.guardMoveSpeed, settings.gunDamage)),
            // 마네킹: 탐색은 "전원 장치 파괴", 사냥은 "사냥".
            [PlayerRole.Mannequin] = new RoleDefinition(PlayerRole.Mannequin, "role.mannequin", "hud.guide.mannequin", MatchWinner.Mannequin,
                () => GameTuning.MannequinHuntSpeedMultiplier,
                ObjectiveCounter.RemainingGenerators, ObjectiveCounter.RemainingGuards, SpawnPlacement.FixedPoints,
                settings => new RoleStats(settings.mannequinMaxHealth, settings.mannequinMoveSpeed, settings.mannequinAttackDamage)),
        };

        /// <summary>
        /// 호출하는 쪽이 null 검사를 하지 않도록, 정의가 없는 역할(없음/관전)에는 이름만 있는 빈 정의를 준다.
        /// </summary>
        public static RoleDefinition Get(PlayerRole role)
        {
            return definitions.TryGetValue(role, out RoleDefinition definition) ? definition : None;
        }

        /// <summary>
        /// 설정에서 능력치를 꺼낼 때마다 정의를 먼저 찾는 코드를 반복하지 않도록 확장 메서드로 연다.
        /// </summary>
        public static RoleStats GetStats(this GameSettings settings, PlayerRole role)
        {
            return Get(role).GetStats(settings);
        }
    }
}
