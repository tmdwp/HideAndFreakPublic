using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>모든 조명이 실시간이라 GI가 필요 없고, 켜 두면 큰 맵에서 GI 재계산으로 메모리가 부족해지므로 모든 씬의 GI를 끈다.</summary>
    public static class SceneLightingSetup
    {
        public const string SettingsPath = TestEnvironmentSetupTool.SoFolder + "/Lighting_RealtimeOnly.lighting";

        /// <summary>한 씬만 GI가 켜져 있어도 메모리 부족이 다시 나므로 모든 씬에 같은 설정 에셋을 지정한다.</summary>
        public static void Apply(Scene scene)
        {
            Lightmapping.SetLightingSettingsForScene(scene, GetOrCreateSettings());
            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>사용자가 바꾼 환경광 설정을 지키려고 에셋이 있으면 그대로 쓰고, 없을 때만 만든다.</summary>
        private static LightingSettings GetOrCreateSettings()
        {
            LightingSettings settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
            if (settings != null)
                return settings;

            settings = new LightingSettings
            {
                name = "Lighting_RealtimeOnly",
                bakedGI = false,
                realtimeGI = false
            };
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }
    }
}
