namespace GuardVsMannequin.Core
{
    /// <summary>
    /// 탄창이 있는 도구. HUD는 <see cref="ITool"/>의 쿨타임과 함께 남은 탄·장전 상태를 표시한다.
    /// </summary>
    public interface IAmmoSource
    {
        /// <summary>남은 탄 수. (소유자 화면 기준)</summary>
        int Ammo { get; }

        /// <summary>탄창 크기.</summary>
        int MagazineSize { get; }

        /// <summary>장전 남은 시간(초). 장전 중이 아니면 0.</summary>
        float ReloadRemaining { get; }
    }
}
