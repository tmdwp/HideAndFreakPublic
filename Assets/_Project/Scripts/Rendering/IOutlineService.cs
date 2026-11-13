using UnityEngine;

namespace GuardVsMannequin.Rendering
{
    /// <summary>종류마다 색이 다른 외곽선 종류.</summary>
    public enum OutlineKind : byte
    {
        /// <summary>마네킹 본인 화면에서 조준한 변신 사물.</summary>
        Aim = 0,

        /// <summary>가드 화면에서 벽 너머로도 보이는 수류탄에 맞은 마네킹.</summary>
        Reveal = 1
    }

    /// <summary>게임 코드가 그리는 방법을 몰라도 되도록 외곽선 켜고 끄기를 인터페이스로 두고, 요청자마다 따로 관리한다.</summary>
    public interface IOutlineService
    {
        /// <summary>
        /// 요청자마다 외곽선 하나만 두도록, 같은 요청자가 다시 부르면 이전 대상을 대체한다.
        /// </summary>
        void Show(Object owner, Renderer[] renderers, OutlineKind kind);

        /// <summary>
        /// 다른 요청자의 외곽선은 그대로 두고 자기 것만 지운다.
        /// </summary>
        void Hide(Object owner);
    }
}
