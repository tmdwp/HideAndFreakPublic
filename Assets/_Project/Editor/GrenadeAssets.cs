using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>프리팹이 참조해 셰이더가 빌드에 들어가도록 수류탄 연출 머티리얼을 에셋으로 만든다.</summary>
    public static class GrenadeAssets
    {
        private const string LitShaderName = "Universal Render Pipeline/Lit";
        private const string UnlitShaderName = "Universal Render Pipeline/Unlit";

        /// <summary>몸체는 조명에 반응해야 맵 분위기와 어울리므로 Lit 셰이더를 쓴다.</summary>
        public static Material GetGrenadeMaterial()
        {
            return GetOrCreate("M_Grenade", LitShaderName, new Color(0.22f, 0.27f, 0.18f));
        }

        /// <summary>폭발 구는 어두운 사냥 시간에도 잘 보여야 하므로 조명과 무관한 Unlit 셰이더를 쓴다.</summary>
        public static Material GetBlastMaterial()
        {
            return GetOrCreate("M_GrenadeBlast", UnlitShaderName, new Color(1f, 0.55f, 0.2f));
        }

        /// <summary>도구를 다시 실행해도 사용자가 바꾼 색을 덮어쓰지 않도록 이미 있으면 그대로 쓴다.</summary>
        private static Material GetOrCreate(string materialName, string shaderName, Color color)
        {
            string materialPath = $"{TestEnvironmentSetupTool.MaterialFolder}/{materialName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null)
                return material;

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[GrenadeAssets] 셰이더 {shaderName}를 찾지 못해 {materialName}을 만들지 않았습니다. (기본 머티리얼로 표시)");
                return null;
            }

            material = new Material(shader);
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, materialPath);
            return material;
        }
    }
}
