using GuardVsMannequin.Core;

namespace GuardVsMannequin.UI
{
    /// <summary>새 HUD 영역을 GameHud 수정 없이 더하도록 영역 하나를 인터페이스로 둔다.</summary>
    public interface IHudSection
    {
        /// <summary>영역마다 직접 찾지 않도록 공용 정보를 처음 한 번 넘겨받는다.</summary>
        void Bind(HudContext context);

        /// <summary>네트워크 준비 전에는 표시할 정보가 없어 빈 HUD가 보이지 않도록 숨긴다.</summary>
        void Hide();

        /// <summary>영역마다 보일 단계가 달라서 보이기/숨기기 판단을 각 영역에 맡긴다.</summary>
        void Refresh(GameState state);
    }
}
