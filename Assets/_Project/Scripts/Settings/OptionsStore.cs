using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GuardVsMannequin.Settings
{
    /// <summary>개인 설정과 키 바인딩을 이 PC에 저장하고, 바뀌면 쓰는 곳이 즉시 반영하도록 알린다.</summary>
    public static class OptionsStore
    {
        private const string OptionsKey = "GuardVsMannequin.Options";
        private const string BindingsKey = "GuardVsMannequin.BindingOverrides";

        private static GameOptions current;
        private static string bindingOverridesJson;
        private static bool isLoaded;

        /// <summary>감도·음량 등을 쓰는 곳이 다시 시작하지 않아도 바로 반영하도록 알린다.</summary>
        public static event Action OptionsChanged;

        /// <summary>이미 만든 입력 에셋 복제본들이 새 키를 다시 적용하도록 알린다.</summary>
        public static event Action BindingsChanged;

        /// <summary>처음 읽는 곳이 어디든 저장값이 먼저 불러와지도록 읽을 때 확인한다.</summary>
        public static GameOptions Current
        {
            get
            {
                EnsureLoaded();
                return current;
            }
        }

        /// <summary>
        /// 범위를 벗어난 값이 저장되면 다음 실행에도 남으므로 저장 전에 보정하고, 바로 화면·소리에 적용한다.
        /// </summary>
        public static void SetOptions(GameOptions options)
        {
            EnsureLoaded();
            options.mouseSensitivity = Mathf.Clamp(options.mouseSensitivity, GameOptions.MinSensitivity, GameOptions.MaxSensitivity);
            options.masterVolume = Mathf.Clamp01(options.masterVolume);
            options.bgmVolume = Mathf.Clamp01(options.bgmVolume);
            options.sfxVolume = Mathf.Clamp01(options.sfxVolume);
            options.screenMode = DisplaySettings.ClampMode(options.screenMode);
            options.frameLimit = FrameRateSettings.ClampMode(options.frameLimit);
            options.cameraSway = CameraSwaySettings.ClampMode(options.cameraSway);
            options.statsDisplay = StatsDisplaySettings.ClampMode(options.statsDisplay);

            bool isDisplayChanged = !current.HasSameDisplay(options);
            current = options;
            PlayerPrefs.SetString(OptionsKey, JsonUtility.ToJson(current));
            PlayerPrefs.Save();

            ApplyMasterVolume();
            FrameRateSettings.Apply(current);
            // 해상도 변경은 화면이 깜빡이므로 화면 설정이 바뀌었을 때만 적용한다.
            if (isDisplayChanged)
                DisplaySettings.Apply(current);
            OptionsChanged?.Invoke();
        }

        /// <summary>원본 에셋이 바뀌지 않도록 입력 에셋 복제본에 저장된 키를 적용한다.</summary>
        public static void ApplyBindings(InputActionAsset actions)
        {
            EnsureLoaded();
            actions.RemoveAllBindingOverrides();
            if (!string.IsNullOrEmpty(bindingOverridesJson))
                actions.LoadBindingOverridesFromJson(bindingOverridesJson);
        }

        /// <summary>
        /// 설정 화면이 편집한 결과를 다른 입력 에셋에도 퍼뜨리려고, 덮어쓰기만 JSON으로 저장한 뒤 알린다.
        /// </summary>
        public static void SaveBindings(InputActionAsset editedActions)
        {
            EnsureLoaded();
            bindingOverridesJson = editedActions.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString(BindingsKey, bindingOverridesJson);
            PlayerPrefs.Save();
            BindingsChanged?.Invoke();
        }

        /// <summary>설정 화면을 열기 전에도 저장한 음량·프레임 제한이 적용되도록 게임 시작 때 불러온다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LoadOnStartup()
        {
            isLoaded = false;
            EnsureLoaded();
        }

        /// <summary>매번 읽지 않도록 한 번만 불러오고, 저장값이 깨지면 기본값을 쓴다.</summary>
        private static void EnsureLoaded()
        {
            if (isLoaded)
                return;
            isLoaded = true;

            current = GameOptions.CreateDefault();
            string json = PlayerPrefs.GetString(OptionsKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    current = JsonUtility.FromJson<GameOptions>(json);
                }
                catch (ArgumentException)
                {
                    Debug.LogWarning("[OptionsStore] 저장된 설정을 읽지 못해 기본값을 사용합니다.");
                }
            }

            bindingOverridesJson = PlayerPrefs.GetString(BindingsKey, string.Empty);
            ApplyMasterVolume();
            // 저장한 적 없는 예전 설정은 frameLimit이 0이라 수직 동기화가 기본으로 켜진다.
            FrameRateSettings.Apply(current);

            // 고른 적 없으면 Unity 기본 화면을 유지한다.
            if (current.screenWidth > 0 && current.screenHeight > 0)
                DisplaySettings.Apply(current);
        }

        /// <summary>
        /// 모든 소리에 한 번에 걸리도록 전체 음량은 AudioListener 음량으로 적용한다.
        /// </summary>
        private static void ApplyMasterVolume()
        {
            AudioListener.volume = current.masterVolume;
        }
    }
}
