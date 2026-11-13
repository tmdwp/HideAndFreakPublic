#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using System.IO;
using System.Text;
using FishNet;
using FishNet.Component.Utility;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Network
{
    /// <summary>화면 표시만으로는 판 전체 흐름을 비교하기 어려워서, 개발 중에만 1초마다 전송량을 파일에 남긴다.</summary>
    public class NetworkTrafficLogger : MonoBehaviour
    {
        private const string FileName = "NetworkTraffic.csv";
        private const float LogInterval = 1f;

        private BandwidthDisplay display;
        private StreamWriter writer;
        private float nextLogTime;

        /// <summary>씬마다 따로 붙이지 않아도 되도록 어느 씬에도 속하지 않는 오브젝트로 한 번 만든다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateInstance()
        {
            var instance = new GameObject(nameof(NetworkTrafficLogger));
            DontDestroyOnLoad(instance);
            instance.AddComponent<NetworkTrafficLogger>();
        }

        /// <summary>창 두 개로 시험할 때 서로 덮어쓰지 않도록 프로세스 번호를 파일 이름에 붙인다.</summary>
        public static string FilePath => Path.Combine(Application.persistentDataPath,
            $"{Path.GetFileNameWithoutExtension(FileName)}_{System.Diagnostics.Process.GetCurrentProcess().Id}{Path.GetExtension(FileName)}");

        /// <summary>파일이 열린 채 남으면 다음 실행에서 쓸 수 없으므로 닫는다.</summary>
        private void OnDestroy()
        {
            writer?.Dispose();
            writer = null;
        }

        /// <summary>
        /// FishNet의 원본 통계는 외부에서 읽을 수 없어서 대역폭 표시가 모은 최근 1초 합계를 읽고, 접속 중일 때만 기록한다.
        /// </summary>
        private void Update()
        {
            if (Time.unscaledTime < nextLogTime)
                return;
            nextLogTime = Time.unscaledTime + LogInterval;

            bool isServer = InstanceFinder.IsServerStarted;
            bool isClient = InstanceFinder.IsClientStarted;
            if (!isServer && !isClient)
                return;
            if (display == null && !TryFindDisplay())
                return;

            WriteRow(isServer, isClient);
        }

        /// <summary>표시 컴포넌트는 네트워크 매니저와 같은 오브젝트에 있어 찾을 때까지만 검색한다.</summary>
        private bool TryFindDisplay()
        {
            display = FindAnyObjectByType<BandwidthDisplay>();
            return display != null && display.ServerAverages != null && display.ClientAverages != null;
        }

        /// <summary>엑셀에서 바로 열리도록 단계·인원과 함께 초당 바이트를 쉼표로 적는다.</summary>
        private void WriteRow(bool isServer, bool isClient)
        {
            if (writer == null)
            {
                writer = new StreamWriter(FilePath, false, new UTF8Encoding(true)) { AutoFlush = true };
                writer.WriteLine("time,role,state,connections,serverInBytes,serverOutBytes,clientInBytes,clientOutBytes");
                Debug.Log($"[NetworkTrafficLogger] 전송량 기록: {FilePath}");
            }

            string role = isServer ? (isClient ? "host" : "server") : "client";
            int connections = isServer ? InstanceFinder.ServerManager.Clients.Count : 0;
            writer.WriteLine(string.Join(",",
                Time.unscaledTime.ToString("0.0", CultureInfo.InvariantCulture),
                role,
                GameServices.CurrentStateOrWaiting,
                connections,
                display.ServerAverages.GetTotal(inBuffer: true),
                display.ServerAverages.GetTotal(inBuffer: false),
                display.ClientAverages.GetTotal(inBuffer: true),
                display.ClientAverages.GetTotal(inBuffer: false)));
        }
    }
}
#endif
