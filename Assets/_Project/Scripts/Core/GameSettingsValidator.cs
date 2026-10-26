using UnityEngine;

namespace GuardVsMannequin.Core
{
    /// <summary>
    /// 게임 설정값을 허용 범위로 보정한다.
    /// 설정값의 유효성 규칙만 담당한다(SRP).
    /// </summary>
    public static class GameSettingsValidator
    {
        // 최후의 안전선. 룸에서 고를 수 있는 범위는 game_settings.csv에서 정하며, 이 값보다 넓힐 수 없다.
        public const float MinHuntDelay = 5f;
        public const float MaxHuntDelay = 300f;
        public const float MinPlayTime = 120f;
        public const float MaxPlayTime = 1800f;
        public const float MaxHuntTime = 900f;
        public const float MinMoveSpeed = 0.5f;
        public const float MaxMoveSpeed = 20f;

        /// <summary>
        /// 현재 참가 인원을 기준으로 설정값을 보정한 결과를 반환한다.
        /// </summary>
        /// <param name="settings">보정할 설정.</param>
        /// <param name="playerCount">현재 참가 인원.</param>
        public static GameSettings Validate(GameSettings settings, int playerCount)
        {
            settings.huntDelay = Mathf.Clamp(settings.huntDelay, MinHuntDelay, MaxHuntDelay);
            settings.playTime = Mathf.Clamp(settings.playTime, MinPlayTime, MaxPlayTime);
            settings.huntTime = Mathf.Clamp(settings.huntTime, 1f, MaxHuntTime);

            settings.guardCount = Mathf.Clamp(settings.guardCount, 1, GetMaxGuardCount(playerCount));

            int mannequinCount = Mathf.Max(1, playerCount - settings.guardCount);
            settings.objectiveCount = Mathf.Clamp(settings.objectiveCount, 0, mannequinCount + 2);

            // 전원 장치는 목표 수보다 반드시 많아야 한다.
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

        /// <summary>
        /// 참가 인원에 따른 최대 가드 수(인원의 절반, 최소 1)를 반환한다.
        /// </summary>
        public static int GetMaxGuardCount(int playerCount)
        {
            return Mathf.Max(1, playerCount / 2);
        }

        /// <summary>
        /// 게임을 시작할 수 있는 인원인지 확인한다.
        /// </summary>
        public static bool CanStart(int playerCount)
        {
            return playerCount >= GameConstants.MinPlayers && playerCount <= GameConstants.MaxPlayers;
        }
    }
}
