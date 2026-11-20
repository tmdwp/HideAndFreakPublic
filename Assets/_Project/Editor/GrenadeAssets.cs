using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Editor
{
    /// <summary>프리팹이 참조해 셰이더가 빌드에 들어가도록 연막탄 연출 에셋(모델·머티리얼·텍스처)을 준비한다.</summary>
    public static class GrenadeAssets
    {
        public const string SmokeGrenadeModelPath = LowPolyCharacterCheck.ModelFolder + "/SM_SmokeGrenade.fbx";

        private const string LitShaderName = "Universal Render Pipeline/Lit";
        private const string ParticleShaderName = "Universal Render Pipeline/Particles/Unlit";
        private const string SmokeMaterialName = "M_GrenadeSmoke";
        private const string SmokeTextureName = "T_SmokePuff";
        private const int SmokeTextureSize = 128;

        /// <summary>모델이 없을 때 쓰는 임시 구는 조명에 반응해야 맵 분위기와 어울리므로 Lit 셰이더를 쓴다.</summary>
        public static Material GetGrenadeMaterial()
        {
            string materialPath = $"{TestEnvironmentSetupTool.MaterialFolder}/M_Grenade.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null)
                return material;

            Shader shader = FindShader(LitShaderName, "M_Grenade");
            if (shader == null)
                return null;
            material = new Material(shader);
            material.SetColor("_BaseColor", new Color(0.22f, 0.27f, 0.18f));
            AssetDatabase.CreateAsset(material, materialPath);
            return material;
        }

        /// <summary>Blender 생성 스크립트(generate_smoke_grenade.py)가 내보낸 모델을 쓰며, 아직 없으면 임시 구로 남도록 null을 준다.</summary>
        public static GameObject GetSmokeGrenadeModel()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(SmokeGrenadeModelPath);
            if (model == null)
                Debug.LogWarning($"[GrenadeAssets] 연막탄 모델이 없습니다: {SmokeGrenadeModelPath}");
            return model;
        }

        /// <summary>
        /// 연막은 어두운 사냥 시간에도 보여야 하므로 조명과 무관한 반투명 파티클 셰이더를 쓰고,
        /// 사용자가 바꾼 값을 덮어쓰지 않도록 이미 있으면 그대로 쓴다.
        /// </summary>
        public static Material GetSmokeMaterial()
        {
            string materialPath = $"{TestEnvironmentSetupTool.MaterialFolder}/{SmokeMaterialName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null)
                return material;

            Shader shader = FindShader(ParticleShaderName, SmokeMaterialName);
            if (shader == null)
                return null;

            material = new Material(shader);
            MakeAlphaBlended(material);
            material.SetColor("_BaseColor", Color.white);
            Texture2D puff = GetOrCreateSmokeTexture();
            if (puff != null)
            {
                material.SetTexture("_BaseMap", puff);
                material.mainTexture = puff;
            }
            AssetDatabase.CreateAsset(material, materialPath);
            return material;
        }

        /// <summary>URP 파티클 셰이더는 인스펙터 대신 코드로 만들면 투명 설정이 자동으로 안 바뀌어 키워드·블렌드를 직접 맞춘다.</summary>
        private static void MakeAlphaBlended(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        /// <summary>둥근 원 하나면 입자 경계가 또렷해 연기로 안 보여서, 가장자리를 노이즈로 흐트러뜨린 뭉게구름 모양을 굽는다.</summary>
        private static Texture2D GetOrCreateSmokeTexture()
        {
            string texturePath = $"{TestEnvironmentSetupTool.MaterialFolder}/{SmokeTextureName}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (existing != null)
                return existing;

            var texture = new Texture2D(SmokeTextureSize, SmokeTextureSize, TextureFormat.RGBA32, false);
            float half = SmokeTextureSize * 0.5f;
            for (int y = 0; y < SmokeTextureSize; y++)
            {
                for (int x = 0; x < SmokeTextureSize; x++)
                {
                    float distance = new Vector2(x - half, y - half).magnitude / half;
                    float noise = Mathf.PerlinNoise(x * 0.06f, y * 0.06f) * 0.6f + Mathf.PerlinNoise(x * 0.15f + 31f, y * 0.15f + 17f) * 0.4f;
                    float falloff = Mathf.Clamp01(1f - distance);
                    float alpha = Mathf.Clamp01(Mathf.SmoothStep(0f, 1f, falloff) * (0.55f + noise * 0.75f));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);

            if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        }

        /// <summary>파이프라인 설정이 달라 셰이더가 없을 때 어떤 에셋이 빠졌는지 알 수 있게 경고한다.</summary>
        private static Shader FindShader(string shaderName, string assetName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                Debug.LogWarning($"[GrenadeAssets] 셰이더 {shaderName}를 찾지 못해 {assetName}을 만들지 않았습니다. (기본 머티리얼로 표시)");
            return shader;
        }
    }
}
