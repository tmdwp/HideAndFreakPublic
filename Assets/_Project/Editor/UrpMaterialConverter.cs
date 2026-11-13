using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Editor
{
    /// <summary>외부 에셋이 URP에서 분홍색으로 보이지 않도록 Built-in용 머티리얼을 URP Lit으로 바꾼다.</summary>
    public static class UrpMaterialConverter
    {
        private const string LitShaderName = "Universal Render Pipeline/Lit";

        // 이미 URP에서 동작하는 셰이더 이름 접두사
        private static readonly string[] CompatiblePrefixes =
        {
            "Universal Render Pipeline/", "Shader Graphs/", "Hidden/Universal", "Sprites/", "UI/", "Skybox/",
            "TextMeshPro/", "Particles/", "Legacy Shaders/Particles/", "Hidden/"
        };

        // 이름에 들어 있으면 알파 컷(나뭇잎, 철망 등)으로 보는 단어
        private static readonly string[] CutoutHints = { "cutout", "leaf", "leaves", "foliage", "grass", "fence", "chain", "net", "plant" };

        // 이름에 들어 있으면 반투명으로 보는 단어
        private static readonly string[] TransparentHints = { "transparent", "glass", "window" };

        /// <summary>여러 렌더러가 머티리얼을 공유하므로 한 번만 변환하고, 깨지는 파티클은 뺀다.</summary>
        public static int ConvertRenderers(IEnumerable<Renderer> renderers)
        {
            var visitedMaterials = new HashSet<Material>();
            int convertedCount = 0;
            foreach (Renderer targetRenderer in renderers)
            {
                if (targetRenderer == null || targetRenderer is ParticleSystemRenderer)
                    continue;

                foreach (Material material in targetRenderer.sharedMaterials)
                {
                    if (material != null && visitedMaterials.Add(material) && ConvertIfNeeded(material))
                        convertedCount++;
                }
            }
            return convertedCount;
        }

        /// <summary>셰이더를 바꾸면 이전 속성을 읽을 수 없으므로 값을 먼저 모아 둔 뒤 Lit으로 바꿔 다시 넣는다.</summary>
        public static bool ConvertIfNeeded(Material material)
        {
            if (material == null || IsCompatible(material.shader))
                return false;

            Shader litShader = Shader.Find(LitShaderName);
            if (litShader == null)
            {
                Debug.LogError("[UrpMaterialConverter] URP Lit 셰이더를 찾을 수 없습니다.");
                return false;
            }

            // 셰이더를 바꾸면 이전 속성을 읽을 수 없으므로 먼저 값을 모아 둔다.
            string oldShaderName = material.shader != null ? material.shader.name : "(없음)";
            Texture baseMap = GetTexture(material, "_MainTex", "_BaseMap", "_Albedo", "_Diffuse", "_BaseColorMap");
            Color baseColor = GetColor(material, Color.white, "_Color", "_BaseColor", "_TintColor", "_MainColor");
            Vector2 tiling = material.HasProperty("_MainTex") ? material.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 offset = material.HasProperty("_MainTex") ? material.GetTextureOffset("_MainTex") : Vector2.zero;
            Texture normalMap = GetTexture(material, "_BumpMap", "_NormalMap", "_Normal");
            float normalScale = material.HasProperty("_BumpScale") ? material.GetFloat("_BumpScale") : 1f;
            Texture metallicMap = GetTexture(material, "_MetallicGlossMap", "_MetallicMap");
            float metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
            float smoothness = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness")
                : material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") : 0.3f;
            Texture occlusionMap = GetTexture(material, "_OcclusionMap", "_AO");
            Texture emissionMap = GetTexture(material, "_EmissionMap", "_Emission");
            Color emissionColor = GetColor(material, Color.black, "_EmissionColor");
            float cutoff = material.HasProperty("_Cutoff") ? material.GetFloat("_Cutoff") : 0.5f;
            int standardMode = GetStandardRenderingMode(material, oldShaderName);
            bool wasCutout = standardMode == 1 || material.IsKeywordEnabled("_ALPHATEST_ON") || NameContains(material, oldShaderName, CutoutHints);
            bool wasTransparent = standardMode >= 2 || material.renderQueue >= (int)RenderQueue.Transparent || NameContains(material, oldShaderName, TransparentHints);

            Undo.RecordObject(material, "Convert to URP");
            material.shader = litShader;

            material.SetTexture("_BaseMap", baseMap);
            material.SetTextureScale("_BaseMap", tiling);
            material.SetTextureOffset("_BaseMap", offset);
            material.SetColor("_BaseColor", baseColor);

            material.SetTexture("_BumpMap", normalMap);
            material.SetFloat("_BumpScale", normalScale);
            SetKeyword(material, "_NORMALMAP", normalMap != null);

            material.SetTexture("_MetallicGlossMap", metallicMap);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
            SetKeyword(material, "_METALLICSPECGLOSSMAP", metallicMap != null);

            material.SetTexture("_OcclusionMap", occlusionMap);
            SetKeyword(material, "_OCCLUSIONMAP", occlusionMap != null);

            bool hasEmission = emissionMap != null || emissionColor.maxColorComponent > 0.01f;
            material.SetTexture("_EmissionMap", emissionMap);
            material.SetColor("_EmissionColor", hasEmission && emissionMap != null && emissionColor.maxColorComponent <= 0.01f ? Color.white : emissionColor);
            SetKeyword(material, "_EMISSION", hasEmission);
            material.globalIlluminationFlags = hasEmission ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            ApplySurface(material, wasCutout, wasTransparent, cutoff);

            EditorUtility.SetDirty(material);
            Debug.Log($"[UrpMaterialConverter] {material.name}: {oldShaderName} → URP Lit", material);
            return true;
        }

        /// <summary>URP·셰이더 그래프·UI 셰이더를 Lit으로 바꾸면 오히려 망가지므로 걸러 낸다.</summary>
        private static bool IsCompatible(Shader shader)
        {
            if (shader == null || shader.name == "Hidden/InternalErrorShader")
                return false;

            foreach (string prefix in CompatiblePrefixes)
            {
                if (shader.name.StartsWith(prefix))
                    return true;
            }
            return false;
        }

        /// <summary>속성 값만 바꾸면 키워드·블렌드·렌더 큐가 따라 바뀌지 않으므로 직접 맞춘다.</summary>
        private static void ApplySurface(Material material, bool isCutout, bool isTransparent, float cutoff)
        {
            material.SetFloat("_Surface", isTransparent ? 1f : 0f);
            material.SetFloat("_AlphaClip", isCutout ? 1f : 0f);
            material.SetFloat("_Cutoff", cutoff);
            SetKeyword(material, "_ALPHATEST_ON", isCutout);
            SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", isTransparent);

            if (isTransparent)
            {
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.renderQueue = (int)RenderQueue.Transparent;
                material.SetOverrideTag("RenderType", "Transparent");
            }
            else
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
                material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                material.SetFloat("_ZWrite", 1f);
                material.renderQueue = isCutout ? (int)RenderQueue.AlphaTest : -1;
                material.SetOverrideTag("RenderType", isCutout ? "TransparentCutout" : "Opaque");
            }

            // 나뭇잎 등 얇은 면은 양면으로 그린다.
            material.SetFloat("_Cull", isCutout ? (float)CullMode.Off : (float)CullMode.Back);
        }

        /// <summary>렌더 큐만으로 투명 여부를 알 수 없는 에셋이 있어 Standard의 Rendering Mode를 함께 본다.</summary>
        private static int GetStandardRenderingMode(Material material, string shaderName)
        {
            if (!shaderName.StartsWith("Standard") || !material.HasProperty("_Mode"))
                return -1;
            return Mathf.RoundToInt(material.GetFloat("_Mode"));
        }

        /// <summary>
        /// 에셋 팩마다 텍스처 속성 이름이 달라서 후보를 차례로 보고, 속성이 있어도 비어 있으면 다음 후보로 넘어간다.
        /// </summary>
        private static Texture GetTexture(Material material, params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                if (material.HasProperty(propertyName) && material.GetTexture(propertyName) != null)
                    return material.GetTexture(propertyName);
            }
            return null;
        }

        /// <summary>에셋 팩마다 색 속성 이름이 달라 후보를 차례로 본다.</summary>
        private static Color GetColor(Material material, Color fallback, params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                if (material.HasProperty(propertyName))
                    return material.GetColor(propertyName);
            }
            return fallback;
        }

        /// <summary>에셋 전용 셰이더는 투명 설정을 알 수 없어 이름에 든 단어로 추정한다.</summary>
        private static bool NameContains(Material material, string shaderName, string[] hints)
        {
            string searchText = (material.name + " " + shaderName).ToLowerInvariant();
            foreach (string hint in hints)
            {
                if (searchText.Contains(hint))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 조건에 따라 키워드를 켜고 끄는 코드가 여러 번 나와 한 줄로 쓰려고 둔다.
        /// </summary>
        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }
    }
}
