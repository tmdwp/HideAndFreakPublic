using Steamworks;
using UnityEngine;

namespace GuardVsMannequin.Network
{
    /// <summary>Steam이 없어도 직접 접속은 되도록 Steam API를 한 번만 초기화하고 콜백을 처리한다.</summary>
    public class SteamBootstrap : MonoBehaviour
    {
        private static SteamBootstrap instance;

        /// <summary>Steam 기능을 쓰는 곳이 초기화 실패 시 직접 접속으로 물러날 수 있도록 노출한다.</summary>
        public static bool IsInitialized { get; private set; }

        /// <summary>
        /// 로비 씬을 다시 불러오면 인스턴스가 또 생기는데, Steam을 두 번 초기화하면 오류가 나므로 첫 인스턴스만 초기화한다.
        /// </summary>
        private void Awake()
        {
            if (instance != null)
                return;

            instance = this;
            Initialize();
        }

        /// <summary>Steam 콜백은 직접 돌려야 오므로 매 프레임 처리한다.</summary>
        private void Update()
        {
            if (instance == this && IsInitialized)
                SteamAPI.RunCallbacks();
        }

        /// <summary>
        /// 정리하지 않으면 Steam이 게임을 계속 실행 중으로 보이므로 종료할 때 API를 닫는다.
        /// </summary>
        private void OnApplicationQuit()
        {
            if (instance != this || !IsInitialized)
                return;

            SteamAPI.Shutdown();
            IsInitialized = false;
        }

        /// <summary>
        /// 파괴된 인스턴스가 남아 있으면 다음 인스턴스가 콜백을 돌리지 못하므로 비운다.
        /// </summary>
        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        /// <summary>잘못된 DLL로 원인 모를 충돌이 나지 않도록 권장 절차대로 검사한 뒤 초기화하고, 실패해도 게임은 계속 돌게 한다.</summary>
        private static void Initialize()
        {
            if (IsInitialized)
                return;

            if (!Packsize.Test())
                Debug.LogError("[Steam] Packsize 검사 실패: 플랫폼에 맞지 않는 Steamworks.NET이 사용되고 있습니다.");
            if (!DllCheck.Test())
                Debug.LogError("[Steam] DllCheck 실패: steam_api 라이브러리가 올바르지 않습니다.");

            try
            {
                IsInitialized = SteamAPI.Init();
            }
            catch (System.DllNotFoundException exception)
            {
                Debug.LogError("[Steam] steam_api 라이브러리를 찾을 수 없습니다: " + exception.Message);
                IsInitialized = false;
            }

            if (!IsInitialized)
                Debug.LogWarning("[Steam] 초기화 실패. Steam이 실행 중인지, steam_appid.txt가 있는지 확인하세요. 직접 접속만 사용할 수 있습니다.");
        }
    }
}
