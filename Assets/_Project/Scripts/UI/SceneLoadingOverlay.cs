using FishNet;
using FishNet.Managing.Scened;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Room;
using UnityEngine;

namespace GuardVsMannequin.UI
{
    /// <summary>게스트는 로딩 지시가 늦게 오므로 시작 버튼이 눌린 순간부터 씬 전환 로딩 화면을 덮는다.</summary>
    public class SceneLoadingOverlay : MonoBehaviour
    {
        /// <summary>에디터 미리 굽기가 만드는 프리팹 경로(Resources 기준).</summary>
        public const string PrefabPath = "UI/SceneLoadingOverlay";

        [Tooltip("로딩 화면 모양. 비우면 실행 중에 기본 모양으로 만든다.")]
        [SerializeField] private SceneLoadingOverlayView view;

        private SceneManager subscribedSceneManager;
        private bool isVisible;
        private bool hasLoadStarted;
        private float loadPercent;
        private float shownTime;
        private bool hasShownTexts;
        private (bool isInGame, bool isWaitingForHost, int percent, int elapsedSeconds, string language) shownTexts;

        /// <summary>
        /// 씬 전환 도중에도 화면이 사라지지 않아야 하므로 어느 씬에도 속하지 않는 오브젝트로 한 번 만든다.
        /// 미리 만든 프리팹이 있으면 에디터에서 고친 모양을 쓰고, 없으면 기본 모양을 코드로 만든다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateInstance()
        {
            var prefab = Resources.Load<SceneLoadingOverlay>(PrefabPath);
            GameObject instance = prefab != null
                ? Instantiate(prefab).gameObject
                : new GameObject(nameof(SceneLoadingOverlay), typeof(SceneLoadingOverlay));
            instance.name = nameof(SceneLoadingOverlay);
            DontDestroyOnLoad(instance);
        }

        /// <summary>프리팹 없이 만들어졌으면 화면을 코드로 채운다.</summary>
        private void Awake()
        {
            EnsureView();
        }

        /// <summary>에디터 미리 굽기도 같은 기본 모양으로 프리팹을 만들도록 공개한다.</summary>
        public void EnsureView()
        {
            if (view == null)
                view = GetComponentInChildren<SceneLoadingOverlayView>(true);
            if (view == null)
                view = SceneLoadingOverlayView.Create(transform);
        }

        /// <summary>
        /// 가속이 켜진 채 남으면 게임 프레임이 떨어지므로 파괴될 때도 되돌린다.
        /// </summary>
        private void OnDestroy()
        {
            Unsubscribe();
            LoadingPerformanceBoost.End();
        }

        /// <summary>
        /// 전환 시작을 알리는 이벤트가 따로 없어서 매 프레임 룸 상태를 보고 켜고 끈다.
        /// </summary>
        private void Update()
        {
            RoomState room = RoomState.Instance;
            bool isPending = room != null && room.IsSceneTransitionPending;
            if (isPending != isVisible)
                SetVisible(isPending);

            if (isVisible)
                RefreshTexts(room);
        }

        /// <summary>
        /// 이전 전환의 진행률이 남지 않도록 켤 때 초기화하고, 보이는 동안에만 로딩 이벤트와 가속을 유지한다.
        /// </summary>
        private void SetVisible(bool visible)
        {
            isVisible = visible;
            view.SetVisible(visible);

            if (visible)
            {
                hasLoadStarted = false;
                hasShownTexts = false;
                loadPercent = 0f;
                shownTime = Time.unscaledTime;
                Subscribe();
                LoadingPerformanceBoost.Begin();
            }
            else
            {
                Unsubscribe();
                LoadingPerformanceBoost.End();
            }
        }

        /// <summary>
        /// 게스트는 호스트를 기다리는 동안 진행률이 0이라 멈춘 것처럼 보이므로, 그때는 대기 중임을 따로 알린다.
        /// </summary>
        private void RefreshTexts(RoomState room)
        {
            int elapsedSeconds = Mathf.FloorToInt(Time.unscaledTime - shownTime);
            bool isWaitingForHost = !hasLoadStarted && !InstanceFinder.IsServerStarted;
            int percent = Mathf.RoundToInt(loadPercent * 100f);

            // 로딩 중에 가비지 수집이 겹치면 더 끊기므로, 보이는 값이 바뀔 때만 문구를 새로 만든다.
            var shown = (room.IsInGame, isWaitingForHost, percent, elapsedSeconds, Loc.CurrentLanguage);
            if (hasShownTexts && shown.Equals(shownTexts))
                return;
            hasShownTexts = true;
            shownTexts = shown;

            string title = Loc.Get(room.IsInGame ? "loading.toGame" : "loading.toRoom");
            string detail = isWaitingForHost
                ? Loc.Format("loading.waitingHost", elapsedSeconds)
                : Loc.Format("loading.progress", percent, elapsedSeconds);

            view.SetTexts(title, detail);
        }

        /// <summary>
        /// NetworkManager가 바뀌었을 수 있어 켤 때마다 지금 것을 다시 구독한다.
        /// </summary>
        private void Subscribe()
        {
            Unsubscribe();
            subscribedSceneManager = InstanceFinder.SceneManager;
            if (subscribedSceneManager == null)
                return;
            subscribedSceneManager.OnLoadStart += OnLoadStart;
            subscribedSceneManager.OnLoadPercentChange += OnLoadPercentChange;
        }

        /// <summary>
        /// 화면이 꺼진 뒤 이벤트가 이 객체를 부르지 않도록 끊는다.
        /// </summary>
        private void Unsubscribe()
        {
            if (subscribedSceneManager == null)
                return;
            subscribedSceneManager.OnLoadStart -= OnLoadStart;
            subscribedSceneManager.OnLoadPercentChange -= OnLoadPercentChange;
            subscribedSceneManager = null;
        }

        /// <summary>
        /// 게스트의 호스트 대기 문구를 진행률 문구로 바꾸기 위해 로딩이 시작된 것을 기록한다.
        /// </summary>
        private void OnLoadStart(SceneLoadStartEventArgs args)
        {
            hasLoadStarted = true;
        }

        /// <summary>
        /// 여러 씬을 차례로 불러오면 진행률이 되돌아갈 수 있어 가장 큰 값만 남긴다.
        /// </summary>
        private void OnLoadPercentChange(SceneLoadPercentEventArgs args)
        {
            hasLoadStarted = true;
            loadPercent = Mathf.Max(loadPercent, args.Percent);
        }
    }
}
