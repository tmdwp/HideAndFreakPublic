using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 로비 메인 화면의 버튼: 방 만들기 / 참가 / 설정 / 크레딧 / 종료.
    /// </summary>
    public class LobbyMainMenu : MonoBehaviour
    {
        [SerializeField] private Button createMenuButton;
        [SerializeField] private Button joinMenuButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button quitButton;

        /// <summary>메뉴가 화면 구성을 몰라도 되도록 화면 이동과 패널 열기를 인터페이스에 맡긴다.</summary>
        private void Start()
        {
            ILobbyNavigator navigator = UiLookup.FindInParent<ILobbyNavigator>(this);
            ILobbyOverlays overlays = UiLookup.FindInParent<ILobbyOverlays>(this);
            createMenuButton.onClick.AddListener(() => navigator?.Show(LobbyScreen.Create));
            joinMenuButton.onClick.AddListener(() => navigator?.Show(LobbyScreen.Join));
            settingsButton.onClick.AddListener(() => overlays?.OpenSettings());
            creditsButton.onClick.AddListener(() => overlays?.OpenCredits());
            quitButton.onClick.AddListener(Quit);
        }

        /// <summary>
        /// 에디터에서는 Application.Quit이 아무 일도 하지 않아 종료 버튼을 시험할 수 없으므로 플레이 모드를 끝낸다.
        /// </summary>
        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
