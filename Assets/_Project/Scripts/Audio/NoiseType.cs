namespace GuardVsMannequin.Audio
{
    /// <summary>번호가 에셋에 저장되므로 바꾸거나 다시 쓰면 안 되는 소음 종류.</summary>
    public enum NoiseType : byte
    {
        /// <summary>가드 사격.</summary>
        GunShot = 0,

        // 1, 2번은 예전 탐지기 소음, 3번은 예전 변신 소음 자리라 다시 쓰지 않는다.

        /// <summary>전원 장치 파괴.</summary>
        GeneratorDestroyed = 4,

        /// <summary>사냥 시간 진입 시 모두에게 들리는 전원 차단 소리.</summary>
        PowerDown = 5,

        /// <summary>사냥 시간 마네킹의 공격.</summary>
        MannequinAttack = 6,

        /// <summary>손전등 스위치.</summary>
        FlashlightClick = 7,

        /// <summary>가드 수류탄 폭발.</summary>
        GrenadeExplosion = 8,

        /// <summary>구울 주변에서 반복되는 숨소리·으르렁.</summary>
        GhoulBreath = 9,

        /// <summary>구울이 손발로 바닥을 짚는 소리.</summary>
        GhoulStep = 10,

        /// <summary>구울 비명 (사냥 시간 시작).</summary>
        GhoulShriek = 11,

        /// <summary>가까운 구울이 보일 때 가드 본인에게만 들리는 심장 소리.</summary>
        GuardHeartbeat = 12
    }
}
