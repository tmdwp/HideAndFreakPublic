using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GuardVsMannequin.Editor
{
    /// <summary>빌드 시간과 실행 성능을 위해 이 게임에 필요 없는 렌더링 기능을 끈다.</summary>
    public static class BuildOptimizationSettings
    {
        private const string PcPipelinePath = "Assets/Settings/PC_RPAsset.asset";
        private const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";

        /// <summary>항목을 하나씩 바꾸다 빠뜨리지 않도록 메뉴 하나로 모두 적용하고 저장한다.</summary>
        [MenuItem(EditorMenus.Root + "빌드 최적화 설정 적용")]
        public static void Apply()
        {
            ApplyPipelineSettings();
            DisableSsao();
            UseDirect3D11Only();
            AssetDatabase.SaveAssets();
            Debug.Log("[BuildOptimizationSettings] 적용 완료. 다음 빌드부터 셰이더 변형 수가 줄어듭니다. (첫 빌드는 새 변형을 다시 컴파일)");
        }

        /// <summary>켜진 기능마다 셰이더 변형이 곱으로 늘어 빌드가 느려지므로 쓰지 않는 것을 끈다.</summary>
        private static void ApplyPipelineSettings()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PcPipelinePath);
            if (pipeline == null)
            {
                Debug.LogError($"[BuildOptimizationSettings] URP 에셋을 찾을 수 없습니다: {PcPipelinePath}");
                return;
            }

            var serializedPipeline = new SerializedObject(pipeline);
            SetBool(serializedPipeline, "m_SupportsLightCookies", false);
            SetBool(serializedPipeline, "m_SupportsLightLayers", false);
            SetBool(serializedPipeline, "m_EnableLODCrossFade", false);
            SetBool(serializedPipeline, "m_ReflectionProbeBlending", false);
            SetBool(serializedPipeline, "m_ReflectionProbeBoxProjection", false);
            SetBool(serializedPipeline, "m_SoftShadowsSupported", false);
            SetInt(serializedPipeline, "m_ShadowCascadeCount", 2);
            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
        }

        /// <summary>어두운 맵에서는 SSAO가 거의 보이지 않는데 GPU 비용은 커서 끈다.</summary>
        private static void DisableSsao()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(PcRendererPath);
            if (rendererData == null)
            {
                Debug.LogWarning($"[BuildOptimizationSettings] 렌더러를 찾을 수 없습니다: {PcRendererPath}");
                return;
            }

            foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures.Where(rendererFeature => rendererFeature != null))
            {
                if (feature.GetType().Name.Contains("AmbientOcclusion"))
                {
                    feature.SetActive(false);
                    EditorUtility.SetDirty(feature);
                }
            }
            EditorUtility.SetDirty(rendererData);
        }

        /// <summary>API마다 셰이더를 따로 컴파일하므로 Steam 오버레이와 맞는 DX11 하나로 줄인다.</summary>
        private static void UseDirect3D11Only()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows, new[] { GraphicsDeviceType.Direct3D11 });
        }

        /// <summary>
        /// URP 버전에 따라 필드 이름이 바뀔 수 있어, 없으면 조용히 넘어가지 않고 무엇이 적용되지 않았는지 경고한다.
        /// </summary>
        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.boolValue = value;
            else
                Debug.LogWarning($"[BuildOptimizationSettings] URP 설정 항목을 찾을 수 없습니다: {propertyName}");
        }

        /// <summary>
        /// URP 버전에 따라 필드 이름이 바뀔 수 있어, 없으면 조용히 넘어가지 않고 무엇이 적용되지 않았는지 경고한다.
        /// </summary>
        private static void SetInt(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.intValue = value;
            else
                Debug.LogWarning($"[BuildOptimizationSettings] URP 설정 항목을 찾을 수 없습니다: {propertyName}");
        }
    }
}
