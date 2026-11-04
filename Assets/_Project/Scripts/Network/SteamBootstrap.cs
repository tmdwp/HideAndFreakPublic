using Steamworks;
using UnityEngine;

namespace GuardVsMannequin.Network
{
    /// <summary>
    /// Steam API를 초기화하고 콜백을 처리한다. 게임 전체에서 한 번만 초기화된다.
    /// Steam이 실행 중이 아니면 초기화에 실패하며, 이때는 직접 접속(Tugboat)만 사용할 수 있다.
    /// (Steam 초기화는 이 컴포넌트 하나만 맡는다. 다른 초기화 스크립트를 씬에 함께 두지 않는다)
    /// </summary>
    public class SteamBootstrap : MonoBehaviour
    {
        private static SteamBootstrap instance;

        /// <summary>Steam API가 초기화되어 사용할 수 있는지 여부.</summary>
        public static bool IsInitialized { get; private set; }

        /// <summary>
        /// 첫 인스턴스만 Steam을 초기화한다. (로비 씬을 다시 불러올 때 생기는 중복은 무시)
        /// </summary>
        private void Awake()
        {
            if (instance != null)
                return;

            instance = this;
            Initialize();
        }

        /// <summary>
        /// 매 프레임 Steam 콜백을 처리한다.
        /// </summary>
        private void Update()
        {
            if (instance == this && IsInitialized)
                SteamAPI.RunCallbacks();
        }

        /// <summary>
        /// 게임 종료 시 Steam API를 정리한다.
        /// </summary>
        private void OnApplicationQuit()
        {
            if (instance != this || !IsInitialized)
                return;

            SteamAPI.Shutdown();
            IsInitialized = false;
        }

        /// <summary>
        /// 이 인스턴스가 사라지면 다음 인스턴스가 초기화할 수 있게 한다.
        /// </summary>
        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        /// <summary>
        /// Steamworks.NET 권장 절차대로 Steam API를 초기화한다.
        /// </summary>
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
