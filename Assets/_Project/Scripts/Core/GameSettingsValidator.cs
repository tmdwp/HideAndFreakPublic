using UnityEngine;

namespace GuardVsMannequin.Core
{
    /// <summary>설정값 유효성 규칙만 맡아 허용 범위로 보정한다.</summary>
    public static class GameSettingsValidator
    {
        // CSV 범위를 넓혀도 넘을 수 없는 최후의 안전선.
        public const float MinHuntDelay = 5f;
        public const float MaxHuntDelay = 300f;
        public const float MinPlayTime = 120f;
        public const float MaxPlayTime = 1800f;
        public const float MaxHuntTime = 900f;
        public const float MinMoveSpeed = 0.5f;
        public const float MaxMoveSpeed = 20f;

        /// <summary>
        /// 가드 수·목표 수처럼 인원에 따라 허용 범위가 달라지는 값이 있어서, 시작 시점 인원으로 보정한다.
        /// </summary>
        public static GameSettings Validate(GameSettings settings, int playerCount)
        {
            settings.huntDelay = Mathf.Clamp(settings.huntDelay, MinHuntDelay, MaxHuntDelay);
            settings.playTime = Mathf.Clamp(settings.playTime, MinPlayTime, MaxPlayTime);
            settings.huntTime = Mathf.Clamp(settings.huntTime, 1f, MaxHuntTime);

            settings.guardCount = Mathf.Clamp(settings.guardCount, 1, GetMaxGuardCount(playerCount));

            int mannequinCount = Mathf.Max(1, playerCount - settings.guardCount);
            settings.objectiveCount = Mathf.Clamp(settings.objectiveCount, 0, mannequinCount + 2);

            // 마네킹이 고를 여지가 있어야 하므로 전원 장치는 목표 수보다 하나 이상 많게 둔다.
            settings.generatorCount = Mathf.Max(settings.generatorCount, settings.objectiveCount + 1);
            settings.generatorDestroyTime = Mathf.Max(0.1f, settings.generatorDestroyTime);

            settings.gunCooldown = Mathf.Max(0f, settings.gunCooldown);
            settings.gunMagazineSize = Mathf.Max(1, settings.gunMagazineSize);
            settings.gunReloadTime = Mathf.Max(0f, settings.gunReloadTime);
            settings.grenadeCooldown = Mathf.Max(0f, settings.grenadeCooldown);
            settings.guardMaxHealth = Mathf.Max(1, settings.guardMaxHealth);
            settings.mannequinMaxHealth = Mathf.Max(1, settings.mannequinMaxHealth);
            settings.huntHealthMultiplier = Mathf.Max(1f, settings.huntHealthMultiplier);
            settings.mannequinAttackRange = Mathf.Max(0.1f, settings.mannequinAttackRange);
            settings.gunDamage = Mathf.Max(1, settings.gunDamage);
            settings.mannequinAttackDamage = Mathf.Max(1, settings.mannequinAttackDamage);
            settings.guardMoveSpeed = Mathf.Clamp(settings.guardMoveSpeed, MinMoveSpeed, MaxMoveSpeed);
            settings.mannequinMoveSpeed = Mathf.Clamp(settings.mannequinMoveSpeed, MinMoveSpeed, MaxMoveSpeed);
            return settings;
        }

        /// <summary>가드가 마네킹보다 많아지지 않도록 인원의 절반으로 묶는다.</summary>
        public static int GetMaxGuardCount(int playerCount)
        {
            return Mathf.Max(1, playerCount / 2);
        }

        /// <summary>
        /// 룸 UI와 서버 시작 검사가 같은 인원 기준을 쓰도록 한곳에 둔다.
        /// </summary>
        public static bool CanStart(int playerCount)
        {
            return playerCount >= GameConstants.MinPlayers && playerCount <= GameConstants.MaxPlayers;
        }
    }
}
