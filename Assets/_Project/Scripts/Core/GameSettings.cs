using System;

namespace GuardVsMannequin.Core
{
    /// <summary>네트워크로 동기화되므로 값 타입 필드만 쓰는 호스트가 조정하는 게임 설정.</summary>
    [Serializable]
    public struct GameSettings
    {
        /// <summary>게스트마다 목록이 달라 호스트만 쓰는 맵 목록 번호.</summary>
        public int mapId;

        /// <summary>목표 0회 판에서 탐색 시작부터 헌트 타임까지의 가드 보호 시간(초).</summary>
        public float huntDelay;

        public float playTime;
        public float huntTime;
        public int guardCount;
        public int objectiveCount;
        public int generatorCount;
        public float generatorDestroyTime;
        /// <summary>한 발을 쏜 뒤 다음 발까지의 총 발사 간격(초).</summary>
        public float gunCooldown;

        /// <summary>총 탄창 크기(발).</summary>
        public int gunMagazineSize;

        /// <summary>총 발당 장전 시간(초).</summary>
        public float gunReloadTime;

        /// <summary>단위는 초.</summary>
        public float grenadeCooldown;

        public int guardMaxHealth;
        public int mannequinMaxHealth;
        public float huntHealthMultiplier;
        public float mannequinAttackRange;

        /// <summary>단위는 m/s.</summary>
        public float guardMoveSpeed;

        /// <summary>사냥 시간 배율 적용 전 마네킹 기본 이동 속도(m/s).</summary>
        public float mannequinMoveSpeed;

        public int gunDamage;

        public int mannequinAttackDamage;

        /// <summary>파일이 깨졌거나 새 항목이 없을 때도 값이 비지 않도록 코드에도 기본값을 둔다.</summary>
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
                gunReloadTime = 2f,
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
