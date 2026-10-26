using System;

namespace GuardVsMannequin.Core
{
    /// <summary>
    /// 호스트가 룸 씬에서 조정하는 게임 설정값.
    /// 네트워크로 동기화되므로 직렬화 가능한 값 타입 필드만 사용한다.
    /// </summary>
    [Serializable]
    public struct GameSettings
    {
        public int mapId;

        /// <summary>목표 0회 판에서 탐색 시작부터 헌트 타임까지의 가드 보호 시간(초).</summary>
        public float huntDelay;

        public float playTime;
        public float huntTime;
        public int guardCount;
        public int objectiveCount;
        public int generatorCount;
        public float generatorDestroyTime;
        /// <summary>총 발사 간격(초). 탄창 안에서 한 발을 쏜 뒤 다음 발까지 기다리는 시간.</summary>
        public float gunCooldown;

        /// <summary>총 탄창 크기(발). 다 쏘면 <see cref="gunReloadTime"/> 동안 장전한다.</summary>
        public int gunMagazineSize;

        /// <summary>총 장전 시간(초). 탄창을 다 쓰면 자동으로 장전한다.</summary>
        public float gunReloadTime;

        /// <summary>가드 수류탄 쿨타임(초).</summary>
        public float grenadeCooldown;

        public int guardMaxHealth;
        public int mannequinMaxHealth;
        public float huntHealthMultiplier;
        public float mannequinAttackRange;

        /// <summary>가드 이동 속도(m/s).</summary>
        public float guardMoveSpeed;

        /// <summary>마네킹 기본 이동 속도(m/s). 사냥 시간에는 <see cref="GameTuning.MannequinHuntSpeedMultiplier"/>배가 된다.</summary>
        public float mannequinMoveSpeed;

        /// <summary>가드 총 한 발의 피해량.</summary>
        public int gunDamage;

        /// <summary>마네킹 공격 한 번의 피해량. (사냥 시간)</summary>
        public int mannequinAttackDamage;

        /// <summary>
        /// 코드에 적힌 기본값으로 채운 설정을 만든다.
        /// 실제 게임의 초기 설정은 Resources/Data/default_game_settings.json(<see cref="DefaultGameSettings"/>)을 따르며,
        /// 파일에 없는 항목은 이 값을 쓴다.
        /// </summary>
        public static GameSettings CreateDefault()
        {
            return new GameSettings
            {
                mapId = 0,
                huntDelay = 35f,
                playTime = 180f,
                huntTime = 60f,
                guardCount = 1,
                objectiveCount = 3,
                generatorCount = 5,
                generatorDestroyTime = 10f,
                gunCooldown = 1f,
                gunMagazineSize = 4,
                gunReloadTime = 5f,
                grenadeCooldown = 15f,
                guardMaxHealth = 3,
                mannequinMaxHealth = 1,
                huntHealthMultiplier = 10f,
                mannequinAttackRange = 3f,
                guardMoveSpeed = 2.2f,
                mannequinMoveSpeed = 2.4f,
                gunDamage = GameConstants.Damage,
                mannequinAttackDamage = GameConstants.Damage
            };
        }
    }
}
