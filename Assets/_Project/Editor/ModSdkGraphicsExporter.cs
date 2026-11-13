using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GuardVsMannequin.Editor
{
    /// <summary>모드 번들 셰이더가 게임과 같은 변형으로 만들어지도록 게임 URP 설정을 SDK용으로 복사한다.</summary>
    public static class ModSdkGraphicsExporter
    {
        private const string SourceAsset = "Assets/Settings/PC_RPAsset.asset";
        private const string SourceRenderer = "Assets/Settings/PC_Renderer.asset";

        /// <summary>게임 설정이 바뀌었을 수 있어 매번 새로 복사하고, SDK에서 쓸 수 없는 게임 전용 렌더러 기능은 뺀다.</summary>
        public static bool Export()
        {
            var sourceAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(SourceAsset);
            if (sourceAsset == null || AssetDatabase.LoadAssetAtPath<UniversalRendererData>(SourceRenderer) == null)
            {
                Debug.LogError($"[ModSdkGraphicsExporter] 게임 URP 설정을 찾을 수 없습니다: {SourceAsset}, {SourceRenderer}");
                return false;
            }

            EnsureFolder(ModSdkSettings.SettingsFolder);
            CopyOver(SourceAsset, ModSdkSettings.PipelineAssetPath);
            CopyOver(SourceRenderer, ModSdkSettings.RendererPath);
            // Graphics 설정은 에셋으로 복사할 수 없어 값을 파일로 적어 둔다.
            System.IO.File.WriteAllLines(ModSdkSettings.ShaderStrippingPath, ModSdkSettings.ReadShaderStripping(), new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(ModSdkSettings.ShaderStrippingPath);
            System.IO.File.WriteAllLines(ModSdkSettings.GraphicsApisPath, ModSdkSettings.ReadGraphicsApis(), new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(ModSdkSettings.GraphicsApisPath);

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(ModSdkSettings.RendererPath);
            RemoveGameRendererFeatures(rendererData);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ModSdkSettings.PipelineAssetPath);
            var serializedPipeline = new SerializedObject(pipeline);
            SerializedProperty rendererList = serializedPipeline.FindProperty("m_RendererDataList");
            rendererList.arraySize = 1;
            rendererList.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
            serializedPipeline.FindProperty("m_DefaultRendererIndex").intValue = 0;

            // SDK가 게임 프로젝트의 다른 폴더에 의존하지 않도록 기본 볼륨 프로필도 복사한다.
            SerializedProperty volumeProfile = serializedPipeline.FindProperty("m_VolumeProfile");
            if (volumeProfile != null && volumeProfile.objectReferenceValue != null)
            {
                CopyOver(AssetDatabase.GetAssetPath(volumeProfile.objectReferenceValue), ModSdkSettings.VolumeProfilePath);
                volumeProfile.objectReferenceValue = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ModSdkSettings.VolumeProfilePath);
            }
            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(pipeline);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            return true;
        }

        /// <summary>SDK에는 게임 코드가 없어 게임 렌더러 기능이 "스크립트 없음"으로 깨지므로 지운다.</summary>
        private static void RemoveGameRendererFeatures(UniversalRendererData rendererData)
        {
            var serializedRenderer = new SerializedObject(rendererData);
            SerializedProperty features = serializedRenderer.FindProperty("m_RendererFeatures");
            // 함께 지워야 URP가 "없는 기능" 경고를 내지 않는 내부 ID 목록.
            SerializedProperty featureMap = serializedRenderer.FindProperty("m_RendererFeatureMap");
            for (int i = features.arraySize - 1; i >= 0; i--)
            {
                var feature = features.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererFeature;
                bool isGameFeature = feature == null || feature.GetType().Assembly.GetName().Name.StartsWith("GuardVsMannequin");
                if (!isGameFeature)
                    continue;

                features.DeleteArrayElementAtIndex(i);
                if (featureMap != null && featureMap.isArray && featureMap.arraySize > i)
                    featureMap.DeleteArrayElementAtIndex(i);
                if (feature != null)
                    Object.DestroyImmediate(feature, true);
            }
            serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
            rendererData.SetDirty();
        }

        /// <summary>
        /// CopyAsset은 대상이 있으면 실패하므로 먼저 지운 뒤 복사한다.
        /// </summary>
        private static void CopyOver(string sourcePath, string destinationPath)
        {
            AssetDatabase.DeleteAsset(destinationPath);
            AssetDatabase.CopyAsset(sourcePath, destinationPath);
        }

        /// <summary>
        /// AssetDatabase.CreateFolder는 한 단계씩만 만들 수 있어 "Assets/a/b/c"의 각 단계를 차례로 만든다.
        /// </summary>
        private static void EnsureFolder(string folderPath)
        {
            string[] folderNames = folderPath.Split('/');
            string currentPath = folderNames[0];
            foreach (string folderName in folderNames.Skip(1))
            {
                string nextPath = $"{currentPath}/{folderName}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                    AssetDatabase.CreateFolder(currentPath, folderName);
                currentPath = nextPath;
            }
        }
    }
}
