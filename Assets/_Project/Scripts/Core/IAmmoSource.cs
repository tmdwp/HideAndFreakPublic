namespace GuardVsMannequin.Core
{
    /// <summary>HUD가 남은 탄·장전 상태를 표시하도록 하는 탄창 있는 도구.</summary>
    public interface IAmmoSource
    {
        /// <summary>즉시 반응하도록 소유자 화면의 예측값을 쓴다.</summary>
        int Ammo { get; }

        int MagazineSize { get; }

        /// <summary>장전 남은 시간(초).</summary>
        float ReloadRemaining { get; }

        /// <summary>HUD가 장전과 구분하도록 따로 주는 발사 간격(초).</summary>
        float FireCooldownRemaining { get; }
    }
}
