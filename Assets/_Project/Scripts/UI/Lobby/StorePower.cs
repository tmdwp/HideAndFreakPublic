using System.Collections.Generic;
using GuardVsMannequin.Map;
using UnityEngine;

namespace GuardVsMannequin.UI
{
    /// <summary>
    /// 로비 편의점의 전기를 한꺼번에 줄였다 켜도록, 조명 밝기와 형광등·냉장고 같은 발광면 밝기를 같은 비율로 맞춘다.
    /// 발광은 머티리얼을 바꾸지 않도록 슬롯별 프로퍼티 블록으로 낮춘다.
    /// </summary>
    public class StorePower : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private const string EmissionKeyword = "_EMISSION";

        [Tooltip("평소에도 깜빡이는 가게 조명. 깜빡임을 유지한 채 밝기 배율만 바꾼다.")]
        [SerializeField] private FlickeringLight[] lights;

        [Tooltip("전기가 나가면 함께 꺼질 발광면(형광등 관·냉장고·계산대 화면 등).")]
        [SerializeField] private Renderer[] poweredRenderers;

        private readonly List<EmissiveSlot> emissiveSlots = new();
        private MaterialPropertyBlock propertyBlock;

        /// <summary>발광 슬롯만 골라 원래 발광색을 기억해, 발광이 없는 슬롯까지 빛나지 않게 한다.</summary>
        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            foreach (Renderer powered in poweredRenderers ?? System.Array.Empty<Renderer>())
            {
                if (powered == null)
                    continue;
                Material[] materials = powered.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && materials[i].IsKeywordEnabled(EmissionKeyword))
                        emissiveSlots.Add(new EmissiveSlot(powered, i, materials[i].GetColor(EmissionColorId)));
                }
            }
        }

        /// <summary>0이면 정전, 1이면 평소 밝기.</summary>
        public void SetLevel(float level)
        {
            level = Mathf.Max(0f, level);
            foreach (FlickeringLight storeLight in lights ?? System.Array.Empty<FlickeringLight>())
            {
                if (storeLight != null)
                    storeLight.IntensityScale = level;
            }

            foreach (EmissiveSlot slot in emissiveSlots)
            {
                slot.Renderer.GetPropertyBlock(propertyBlock, slot.MaterialIndex);
                propertyBlock.SetColor(EmissionColorId, slot.BaseEmission * level);
                slot.Renderer.SetPropertyBlock(propertyBlock, slot.MaterialIndex);
            }
        }

        /// <summary>한 렌더러의 발광 슬롯 하나와 그 원래 발광색.</summary>
        private readonly struct EmissiveSlot
        {
            public readonly Renderer Renderer;
            public readonly int MaterialIndex;
            public readonly Color BaseEmission;

            public EmissiveSlot(Renderer renderer, int materialIndex, Color baseEmission)
            {
                Renderer = renderer;
                MaterialIndex = materialIndex;
                BaseEmission = baseEmission;
            }
        }
    }
}
