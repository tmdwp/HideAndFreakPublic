using System.Collections;
using System.Collections.Generic;
using GuardVsMannequin.Audio;
using GuardVsMannequin.Core;
using GuardVsMannequin.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Map
{
    /// <summary>사냥 시간 진입을 알림 대신 전원 차단음과 꺼지는 조명으로 연출한다.</summary>
    public class HuntingLightingController : MonoBehaviour
    {
        [SerializeField] private NoiseLibrary noiseLibrary;

        [Tooltip("조명이 하나씩 꺼지는 데 걸리는 최대 지연(초). 한꺼번에 꺼지는 것보다 전원이 나가는 느낌을 준다.")]
        [SerializeField] private float maxOffDelay = 0.5f;

        [Tooltip("사냥 시간 환경광의 색조. 밝기는 game_tuning.csv의 HuntAmbientBrightness를 곱한다. (가장 밝은 채널 = 1 기준)")]
        [SerializeField] private Color huntingAmbientTint = new Color(0.67f, 0.67f, 1f);

        private MapLight[] mapLights;
        private readonly List<FlickeringLight> huntFlickers = new();
        private AudioSource audioSource;
        private bool isDark;

        private AmbientMode originalAmbientMode;
        private Color originalAmbientLight;
        private float originalReflectionIntensity;
        private Material originalSkybox;
        private Material darkSkybox;

        /// <summary>어두워지는 순간 끊김이 없도록 하늘과 오디오 소스를 미리 만들고, 맵 조명은 그 순간에 읽는다.</summary>
        private void Awake()
        {
            darkSkybox = CreateDarkSkybox();

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        /// <summary>
        /// RenderSettings는 씬 전환 후에도 남아서 다음 화면이 어두운 채 열릴 수 있으므로 떠날 때 되돌린다.
        /// </summary>
        private void OnDestroy()
        {
            if (isDark)
                RestoreEnvironment();
            if (darkSkybox != null)
                Destroy(darkSkybox);
        }

        /// <summary>이벤트를 놓친 피어도 같은 화면이 되도록 동기화된 상태를 매 프레임 보고 연출한다.</summary>
        private void Update()
        {
            GameState state = GameServices.CurrentStateOrWaiting;
            bool shouldBeDark = state == GameState.Hunting || (state == GameState.Ended && isDark);

            if (shouldBeDark && !isDark)
                BlackOut();
            else if (!shouldBeDark && isDark)
                RestoreLights();
        }

        /// <summary>
        /// 되돌릴 수 있도록 원래 환경 설정을 저장한 뒤, 전원 차단음과 함께 조명을 조금씩 어긋나게 끈다.
        /// </summary>
        private void BlackOut()
        {
            isDark = true;
            mapLights = FindObjectsByType<MapLight>();
            originalAmbientMode = RenderSettings.ambientMode;
            originalAmbientLight = RenderSettings.ambientLight;
            originalReflectionIntensity = RenderSettings.reflectionIntensity;
            originalSkybox = RenderSettings.skybox;

            if (noiseLibrary != null)
                audioSource.PlayOneShot(noiseLibrary.GetClip(NoiseType.PowerDown), noiseLibrary.Get(NoiseType.PowerDown).volume * AudioVolumes.Sfx);

            HashSet<MapLight> flickeringLights = HuntFlickerSelector.Choose(mapLights, GameTuning.HuntFlickerLightRatio);
            foreach (MapLight mapLight in mapLights)
            {
                float delay = Random.Range(0f, maxOffDelay);
                StartCoroutine(flickeringLights.Contains(mapLight) ? StartFlickerAfter(mapLight, delay) : TurnOffAfter(mapLight, delay));
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = GetHuntingAmbient();
            RenderSettings.reflectionIntensity = 0f;
            RenderSettings.skybox = darkSkybox;
        }

        /// <summary>
        /// 완전히 검으면 손전등 밖이 아무것도 안 보이므로, 색조 × 조정 파일 밝기(0~1)만큼은 남긴다.
        /// </summary>
        private Color GetHuntingAmbient()
        {
            float brightness = Mathf.Clamp01(GameTuning.HuntAmbientBrightness);
            Color ambient = huntingAmbientTint * brightness;
            ambient.a = 1f;
            return ambient;
        }

        /// <summary>
        /// 그사이 상태가 되돌아갔으면 다시 끄지 않도록 기다린 뒤에 한 번 더 확인한다.
        /// </summary>
        private IEnumerator TurnOffAfter(MapLight mapLight, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (isDark && mapLight != null)
                mapLight.SetOn(false);
        }

        /// <summary>
        /// 맵 제작자가 이미 깜빡임을 붙여 둔 조명은 그 설정을 존중해 그대로 두고, 이 컨트롤러가 붙인 것만 기억해 나중에 뗀다.
        /// </summary>
        private IEnumerator StartFlickerAfter(MapLight mapLight, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!isDark || mapLight == null || mapLight.GetComponent<FlickeringLight>() != null)
                yield break;

            FlickeringLight flicker = mapLight.gameObject.AddComponent<FlickeringLight>();
            flicker.SetTargets(mapLight.Lights);
            flicker.IntensityScale = GameTuning.HuntFlickerIntensityScale;
            huntFlickers.Add(flicker);
        }

        /// <summary>
        /// Destroy는 프레임 끝에 처리되어 그 사이 어두운 밝기가 남으므로, 먼저 꺼서 그 자리에서 원래 밝기로 되돌린다.
        /// </summary>
        private void RemoveHuntFlickers()
        {
            foreach (FlickeringLight flicker in huntFlickers)
            {
                if (flicker == null)
                    continue;
                flicker.enabled = false;
                Destroy(flicker);
            }
            huntFlickers.Clear();
        }

        /// <summary>
        /// 아직 기다리는 끄기 코루틴이 다시 켠 조명을 끄지 않도록 먼저 멈춘 뒤, 모든 조명과 환경 설정을 되돌린다.
        /// </summary>
        private void RestoreLights()
        {
            isDark = false;
            StopAllCoroutines();
            RemoveHuntFlickers();

            foreach (MapLight mapLight in mapLights ?? System.Array.Empty<MapLight>())
            {
                if (mapLight != null)
                    mapLight.SetOn(true);
            }

            RestoreEnvironment();
        }

        /// <summary>
        /// 스카이박스를 비우면 카메라 기본 배경색(푸른색)이 보일 수 있으므로, 노출을 0으로 낮춘 기본 절차적 스카이박스로 검은 하늘을 만든다.
        /// </summary>
        private static Material CreateDarkSkybox()
        {
            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
                return null;

            var material = new Material(shader);
            material.SetFloat("_Exposure", 0f);
            return material;
        }

        /// <summary>
        /// 맵마다 환경 설정이 달라서, 어두워지기 직전에 저장해 둔 값으로 되돌린다.
        /// </summary>
        private void RestoreEnvironment()
        {
            RenderSettings.ambientMode = originalAmbientMode;
            RenderSettings.ambientLight = originalAmbientLight;
            RenderSettings.reflectionIntensity = originalReflectionIntensity;
            RenderSettings.skybox = originalSkybox;
        }
    }
}
