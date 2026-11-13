using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Editor
{
    /// <summary>모드 번들 셰이더가 게임과 똑같이 보이도록 SDK 프로젝트에 게임 그래픽 설정을 적용한다.</summary>
    public static class ModSdkSettings
    {
        /// <summary>SDK 전용 파일 폴더(게임 그래픽 설정, 예제 템플릿).</summary>
        public const string Root = "Assets/_Project/ModSdk";

        public const string SettingsFolder = Root + "/Settings";

        /// <summary>게임과 같은 URP 설정.</summary>
        public const string PipelineAssetPath = SettingsFolder + "/HF_RPAsset.asset";

        /// <summary>게임 전용 렌더러 기능은 SDK에 없으므로 뺀 렌더러 설정.</summary>
        public const string RendererPath = SettingsFolder + "/HF_Renderer.asset";

        public const string VolumeProfilePath = SettingsFolder + "/HF_VolumeProfile.asset";

        public const string TemplateFolder = Root + "/Template";

        /// <summary>모드 내보내기 전에 제작 Unity 버전을 확인하는 게임 Unity 버전 파일.</summary>
        public const string GameUnityVersionPath = SettingsFolder + "/game_unity_version.txt";

        /// <summary>모드 맵 씬 이름이 겹치는지 확인하는 게임 씬 이름 목록 파일.</summary>
        public const string GameSceneNamesPath = SettingsFolder + "/game_scene_names.txt";

        /// <summary>SDK를 만든 게임의 셰이더 변형 제거 설정을 적은 파일("이름=값" 한 줄에 하나).</summary>
        public const string ShaderStrippingPath = SettingsFolder + "/game_shader_stripping.txt";

        /// <summary>SDK를 만든 게임이 Windows에서 쓰는 그래픽 API를 적은 파일(한 줄에 하나).</summary>
        public const string GraphicsApisPath = SettingsFolder + "/game_graphics_apis.txt";

        // 번들 셰이더는 제작 프로젝트의 그래픽 API로만 컴파일되므로 게임이 쓰는 API가 빠지면 분홍색으로 보인다.
        private const BuildTarget WindowsTarget = BuildTarget.StandaloneWindows64;

        // 번들 셰이더에 들어갈 변형을 정하는 Graphics 설정 항목.
        private static readonly string[] ShaderStrippingProperties =
        {
            "m_LightmapStripping", "m_LightmapKeepPlain", "m_LightmapKeepDirCombined", "m_LightmapKeepDynamicPlain",
            "m_LightmapKeepDynamicDirCombined", "m_LightmapKeepShadowMask", "m_LightmapKeepSubtractive",
            "m_FogStripping", "m_FogKeepLinear", "m_FogKeepExp", "m_FogKeepExp2", "m_InstancingStripping", "m_BrgStripping"
        };

        // 게임 프로젝트에서 모드를 시험 삼아 내보낼 때 쓰는 자신의 URP 설정.
        private const string GameProjectPipelinePath = "Assets/Settings/PC_RPAsset.asset";

        /// <summary>SDK 프로젝트에는 게임 빌드 설정이 없어 목록 파일을 읽는다.</summary>
        public static string[] GameSceneNames
        {
            get
            {
                string fullPath = System.IO.Path.GetFullPath(GameSceneNamesPath);
                if (System.IO.File.Exists(fullPath))
                    return System.IO.File.ReadAllLines(fullPath).Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
                return EditorBuildSettings.scenes.Where(scene => scene.enabled)
                    .Select(scene => System.IO.Path.GetFileNameWithoutExtension(scene.path)).ToArray();
            }
        }

        /// <summary>Windows 파일 시스템은 대소문자를 구별하지 않으므로 대소문자를 무시하고 비교한다.</summary>
        public static bool IsGameSceneName(string sceneName)
        {
            return GameSceneNames.Any(name => string.Equals(name, sceneName, System.StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>SDK 프로젝트는 게임과 다른 Unity로 열릴 수 있어 파일에 적힌 게임 버전을 읽는다.</summary>
        public static string GameUnityVersion
        {
            get
            {
                string fullPath = System.IO.Path.GetFullPath(GameUnityVersionPath);
                return System.IO.File.Exists(fullPath) ? System.IO.File.ReadAllText(fullPath).Trim() : Application.unityVersion;
            }
        }

        /// <summary>제작자가 하나씩 맞추지 않도록 모든 품질 단계의 URP·색 공간·셰이더 변형·그래픽 API를 한 번에 맞춘다.</summary>
        [MenuItem(EditorMenus.Mod + "게임 그래픽 설정 적용")]
        public static void ApplyGameGraphics()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PipelineAssetPath);
            if (pipeline == null)
            {
                EditorUtility.DisplayDialog("게임 그래픽 설정", $"설정 파일이 없습니다: {PipelineAssetPath}\nSDK 패키지를 다시 가져오세요.", "확인");
                return;
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int currentQualityLevel = QualitySettings.GetQualityLevel();
            for (int level = 0; level < QualitySettings.names.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(currentQualityLevel, false);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            ApplyShaderStripping();
            ApplyGraphicsApis();

            AssetDatabase.SaveAssets();
            Debug.Log("[ModSdkSettings] 게임 그래픽 설정(URP·품질 단계·선형 색 공간·셰이더 변형·그래픽 API)을 적용했습니다.");
            EditorUtility.DisplayDialog("게임 그래픽 설정", "게임과 같은 그래픽 설정을 적용했습니다.\n모드를 내보내기 전에 한 번만 하면 됩니다.\n에디터 화면의 그래픽 API는 Unity를 다시 열면 바뀌지만, 내보내기는 지금 바로 새 설정을 씁니다.", "확인");
        }

        /// <summary>내보내기 전에 매번 묻지 않도록 이미 맞춰져 있는지 확인한다.</summary>
        public static bool IsGameGraphicsApplied()
        {
            RenderPipelineAsset currentPipeline = GraphicsSettings.defaultRenderPipeline;
            string currentPath = currentPipeline != null ? AssetDatabase.GetAssetPath(currentPipeline) : string.Empty;
            return currentPath == GameProjectPipelinePath || (currentPath == PipelineAssetPath && IsShaderStrippingApplied() && IsGraphicsApisApplied());
        }

        /// <summary>
        /// SDK를 만들 때 게임의 API 목록을 파일로 적어 두기 위해 순서를 유지한 이름 목록을 공개한다.
        /// </summary>
        public static string[] ReadGraphicsApis()
        {
            return PlayerSettings.GetGraphicsAPIs(WindowsTarget).Select(api => api.ToString()).ToArray();
        }

        /// <summary>예전 SDK처럼 API 목록 파일이 없으면 비교할 기준이 없어 맞춰진 것으로 본다.</summary>
        private static bool IsGraphicsApisApplied()
        {
            string[] expectedApis = ReadLines(GraphicsApisPath);
            return expectedApis.Length == 0 || expectedApis.SequenceEqual(ReadGraphicsApis());
        }

        /// <summary>게임이 쓰는 API가 빠지지 않도록 이름을 하나라도 못 읽으면 아무것도 바꾸지 않는다.</summary>
        private static void ApplyGraphicsApis()
        {
            string[] apiNames = ReadLines(GraphicsApisPath);
            var apis = new List<GraphicsDeviceType>();
            foreach (string apiName in apiNames)
            {
                if (!System.Enum.TryParse(apiName, out GraphicsDeviceType api))
                {
                    Debug.LogWarning($"[ModSdkSettings] 알 수 없는 그래픽 API '{apiName}'가 있어 그래픽 API를 바꾸지 않았습니다. SDK 패키지를 다시 가져오세요.");
                    return;
                }
                apis.Add(api);
            }
            if (apis.Count == 0)
                return;

            PlayerSettings.SetUseDefaultGraphicsAPIs(WindowsTarget, false);
            PlayerSettings.SetGraphicsAPIs(WindowsTarget, apis.ToArray());
        }

        /// <summary>사람이 열어 볼 수 있는 파일이라 빈 줄과 앞뒤 공백을 무시한다.</summary>
        private static string[] ReadLines(string assetPath)
        {
            string fullPath = System.IO.Path.GetFullPath(assetPath);
            if (!System.IO.File.Exists(fullPath))
                return System.Array.Empty<string>();
            return System.IO.File.ReadAllLines(fullPath).Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
        }

        /// <summary>공개 API가 없는 설정이라 SerializedObject로 읽어 "이름=값" 목록으로 돌려준다.</summary>
        public static List<string> ReadShaderStripping()
        {
            var graphicsSettings = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            var lines = new List<string>();
            foreach (string propertyName in ShaderStrippingProperties)
            {
                SerializedProperty property = graphicsSettings.FindProperty(propertyName);
                if (property != null)
                    lines.Add($"{propertyName}={ReadValue(property)}");
            }
            return lines;
        }

        /// <summary>비교와 적용이 같은 기준을 쓰도록 게임이 적어 둔 설정 읽기를 한곳에 둔다.</summary>
        private static string[] ReadGameShaderStripping()
        {
            return ReadLines(ShaderStrippingPath);
        }

        /// <summary>
        /// Unity 버전에 따라 항목이 더 있을 수 있어 완전 일치 대신 게임이 적어 둔 항목이 모두 들어 있는지만 본다.
        /// </summary>
        private static bool IsShaderStrippingApplied()
        {
            List<string> currentSettings = ReadShaderStripping();
            return ReadGameShaderStripping().All(currentSettings.Contains);
        }

        /// <summary>번들 셰이더에 게임이 쓰는 변형이 빠지지 않도록 게임 설정대로 쓴다.</summary>
        private static void ApplyShaderStripping()
        {
            var graphicsSettings = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            foreach (string line in ReadGameShaderStripping())
            {
                string[] nameAndValue = line.Split('=');
                SerializedProperty property = nameAndValue.Length == 2 ? graphicsSettings.FindProperty(nameAndValue[0]) : null;
                if (property != null && int.TryParse(nameAndValue[1], out int value))
                    WriteValue(property, value);
            }
            graphicsSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 항목마다 형식(켜기/끄기, 열거형, 정수)이 달라서 파일에 같은 형식으로 적을 수 있게 숫자로 맞춘다.
        /// </summary>
        private static int ReadValue(SerializedProperty property)
        {
            if (property.propertyType == SerializedPropertyType.Boolean)
                return property.boolValue ? 1 : 0;
            return property.propertyType == SerializedPropertyType.Enum ? property.enumValueFlag : property.intValue;
        }

        /// <summary>파일의 숫자를 항목 형식에 맞게 되돌려 쓴다.</summary>
        private static void WriteValue(SerializedProperty property, int value)
        {
            if (property.propertyType == SerializedPropertyType.Boolean)
                property.boolValue = value != 0;
            else if (property.propertyType == SerializedPropertyType.Enum)
                property.enumValueFlag = value;
            else
                property.intValue = value;
        }
    }
}
