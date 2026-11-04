namespace GuardVsMannequin.Core
{
    /// <summary>
    /// 쿨타임을 세로 바 대신 조준점 둘레의 원형 고리로 보여주는 도구(구울 공격 등).
    /// 조준해서 쓰는 주 공격처럼 시선을 떼지 않고 봐야 하는 쿨타임에 붙인다.
    /// </summary>
    public interface ICrosshairCooldown : ICooldownGauge
    {
    }
}
