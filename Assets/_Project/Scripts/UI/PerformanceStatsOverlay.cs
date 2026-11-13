using FishNet;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>로비·룸·게임 어디서든 성능과 접속 상태를 바로 확인하도록 화면 우측 상단에 FPS와 핑을 띄운다.</summary>
    public class PerformanceStatsOverlay : MonoBehaviour
    {
        // 로딩 화면(30000)에는 가려지고 일반 UI 위에는 보이도록 그 아래 순서를 쓴다.
        private const int SortingOrder = 29000;

        // 한 프레임 값은 흔들려 읽기 어려우므로 이 시간 동안의 평균으로 표시한다.
        private const float SampleInterval = 0.5f;

        private const int FontSize = 22;
        private static readonly Vector2 Margin = new(16f, 12f);
        private static readonly Vector2 Size = new(360f, 60f);

        private Text statsText;
        private int frameCount;
        private float sampleStartTime;
        private bool showFps;
        private bool showPing;
        private (int fps, long ping, bool isHost, bool isConnected, string language) shown;
        private bool isShownValid;

        /// <summary>씬 전환에도 남도록 어느 씬에도 속하지 않는 오브젝트로 한 번 만든다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateInstance()
        {
            var instance = new GameObject(nameof(PerformanceStatsOverlay));
            DontDestroyOnLoad(instance);
            instance.AddComponent<PerformanceStatsOverlay>();
        }

        /// <summary>씬이나 프리팹에 UI를 배치하지 않아도 되도록 화면을 코드로 만든다.</summary>
        private void Awake()
        {
            statsText = CreateText(transform);
            sampleStartTime = Time.unscaledTime;
            ApplyOptions();
        }

        /// <summary>설정 화면에서 바꾸면 다음 표본을 기다리지 않고 바로 반영되도록 변경 알림을 듣는다.</summary>
        private void OnEnable()
        {
            OptionsStore.OptionsChanged += ApplyOptions;
        }

        /// <summary>
        /// 꺼진 뒤 이벤트가 이 객체를 부르지 않도록 끊는다.
        /// </summary>
        private void OnDisable()
        {
            OptionsStore.OptionsChanged -= ApplyOptions;
        }

        /// <summary>둘 다 끄면 글자가 남지 않게 숨기고, 다음 표본에서 새 항목으로 다시 쓰도록 표시 기록을 지운다.</summary>
        private void ApplyOptions()
        {
            (showFps, showPing) = StatsDisplaySettings.Get(OptionsStore.Current);
            statsText.enabled = showFps || showPing;
            isShownValid = false;
            // 꺼져 있던 동안은 프레임을 세지 않으므로, 다시 켰을 때 그 시간이 평균에 섞이지 않게 표본을 새로 시작한다.
            frameCount = 0;
            sampleStartTime = Time.unscaledTime;
        }

        /// <summary>문자열을 매 프레임 만들지 않도록 프레임 수만 세고, 표본 구간이 끝났을 때 값이 바뀌었으면 갱신한다.</summary>
        private void Update()
        {
            if (!statsText.enabled)
                return;

            frameCount++;
            float elapsed = Time.unscaledTime - sampleStartTime;
            if (elapsed < SampleInterval)
                return;

            int fps = Mathf.RoundToInt(frameCount / elapsed);
            frameCount = 0;
            sampleStartTime = Time.unscaledTime;

            bool isConnected = InstanceFinder.IsClientStarted;
            bool isHost = isConnected && InstanceFinder.IsServerStarted;
            long ping = isConnected && !isHost ? InstanceFinder.TimeManager.RoundTripTime : 0;

            var current = (fps, ping, isHost, isConnected, Loc.CurrentLanguage);
            if (isShownValid && current.Equals(shown))
                return;
            shown = current;
            isShownValid = true;
            statsText.text = BuildText(fps, ping, isHost, isConnected);
        }

        /// <summary>
        /// 호스트는 자기 자신과 통신해 핑이 의미 없어서 따로 표시하고, 접속 전이거나 핑을 끈 설정이면 핑 줄을 뺀다.
        /// </summary>
        private string BuildText(int fps, long ping, bool isHost, bool isConnected)
        {
            string fpsLine = showFps ? Loc.Format("hud.stats.fps", fps) : null;
            string pingLine = !showPing || !isConnected ? null
                : isHost ? Loc.Get("hud.stats.pingHost") : Loc.Format("hud.stats.ping", ping);

            if (fpsLine == null)
                return pingLine ?? string.Empty;
            return pingLine == null ? fpsLine : fpsLine + "\n" + pingLine;
        }

        /// <summary>
        /// 해상도가 바뀌어도 같은 크기로 보이도록 기준 해상도로 맞추고, 클릭을 가로채지 않도록 레이캐스트 없이 만든다.
        /// </summary>
        private static Text CreateText(Transform parent)
        {
            var canvasObject = new GameObject("Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(parent, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var textObject = new GameObject("Stats", typeof(RectTransform));
            textObject.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-Margin.x, -Margin.y);
            rect.sizeDelta = Size;

            var text = textObject.AddComponent<Text>();
            text.font = UiFont.Default;
            text.fontSize = FontSize;
            text.alignment = TextAnchor.UpperRight;
            text.color = Color.white;
            text.raycastTarget = false;

            // 밝은 배경에서도 읽히도록 외곽선을 둔다.
            var outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }
    }
}
