using GuardVsMannequin.Core;
using GuardVsMannequin.Rendering;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Cameras
{
    /// <summary>시작하자마자 멀리 있는 상대를 찾지 못하도록 판 시작 구간 동안 모든 플레이어 화면에 검은 안개를 걸고, 끝나면 천천히 걷는다.</summary>
    public class MatchOpeningVision : MonoBehaviour
    {
        private enum Phase
        {
            Off,
            Active,
            Fading
        }

        private Phase phase;
        private FogSettings originalFog;
        private FogSettings openingFog;
        private readonly SkyboxDimmer skyboxDimmer = new();
        private float fadeStartTime;

        /// <summary>
        /// 아바타·맵 프리팹을 고치지 않고 모든 씬에서 동작하도록 어느 씬에도 속하지 않는 오브젝트로 한 번 만든다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateInstance()
        {
            var instance = new GameObject(nameof(MatchOpeningVision));
            DontDestroyOnLoad(instance);
            instance.AddComponent<MatchOpeningVision>();
        }

        /// <summary>
        /// 안개는 씬마다 따로라, 씬이 바뀐 뒤에 이전 맵 값을 새 씬에 덮어쓰지 않도록 전환을 듣는다.
        /// </summary>
        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        /// <summary>
        /// 꺼진 뒤 이벤트가 이 객체를 부르지 않도록 끊는다.
        /// </summary>
        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        /// <summary>시작 신호가 따로 없고 늦게 들어온 피어도 같은 화면이어야 해서 매 프레임 동기화된 타이머로 판단한다.</summary>
        private void Update()
        {
            if (MatchOpeningRules.IsActive(GameServices.State))
            {
                EnterOpening();
                return;
            }

            if (phase == Phase.Active)
                BeginFade();
            if (phase == Phase.Fading)
                UpdateFade();
        }

        /// <summary>
        /// 테스트 방처럼 걷히는 도중에 탐색이 다시 시작될 수 있어서, 이미 저장한 원래 안개는 덮어쓰지 않는다.
        /// </summary>
        private void EnterOpening()
        {
            if (phase == Phase.Active)
                return;
            if (phase == Phase.Off)
                originalFog = FogSettings.Capture();

            openingFog = FogSettings.Linear(Color.black, GameTuning.OpeningVisionFogStart, GameTuning.OpeningVisionFogEnd);
            openingFog.Apply();
            skyboxDimmer.Dim(GameTuning.OpeningVisionSkyBrightness);
            phase = Phase.Active;
        }

        /// <summary>걷히는 시간이 0이면 기다릴 필요가 없어 바로 되돌린다.</summary>
        private void BeginFade()
        {
            if (GameTuning.OpeningVisionFadeTime <= 0f)
            {
                Restore();
                return;
            }
            fadeStartTime = Time.time;
            phase = Phase.Fading;
        }

        /// <summary>
        /// 탐색이 끝나 사냥 시야 같은 다른 안개 연출이 시작되면 겹치지 않도록, 탐색이 아닐 때는 걷히기를 끊고 바로 되돌린다.
        /// </summary>
        private void UpdateFade()
        {
            float progress = (Time.time - fadeStartTime) / GameTuning.OpeningVisionFadeTime;
            if (progress >= 1f || GameServices.CurrentStateOrWaiting != GameState.Playing)
            {
                Restore();
                return;
            }
            openingFog.FadeToward(originalFog, GameTuning.OpeningVisionClearDistance, progress).Apply();
            // 안개와 하늘이 따로 밝아지면 어색하므로 안개 걷힘과 같은 곡선으로 하늘을 되살린다.
            skyboxDimmer.Dim(Mathf.Lerp(GameTuning.OpeningVisionSkyBrightness, 1f, Mathf.SmoothStep(0f, 1f, progress)));
        }

        /// <summary>시작 구간이 끝나면 맵 원래 안개와 하늘로 돌려놓는다.</summary>
        private void Restore()
        {
            originalFog.Apply();
            skyboxDimmer.Restore();
            phase = Phase.Off;
        }

        /// <summary>새 씬은 자기 안개·하늘 설정을 가지고 열리므로 되돌리지 않고 상태만 버린다.</summary>
        private void OnActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            skyboxDimmer.Forget();
            phase = Phase.Off;
        }
    }
}
