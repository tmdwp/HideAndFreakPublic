using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Map
{
    /// <summary>사냥 시간에 조명을 끌 때 빛과 발광 머티리얼이 함께 꺼지도록 묶는다.</summary>
    public class MapLight : MonoBehaviour
    {
        [Tooltip("켜고 끌 조명. 비어 있으면 자식에서 찾는다.")]
        [SerializeField] private Light[] lights;

        [Tooltip("전구 외형. 켜짐/꺼짐 머티리얼로 교체한다. (없어도 됨)")]
        [SerializeField] private Renderer bulbRenderer;
        [SerializeField] private Material onMaterial;
        [SerializeField] private Material offMaterial;

        public bool IsOn { get; private set; } = true;

        /// <summary>사냥 시간 깜빡임이 개별 조명을 다뤄야 해서 공개한다.</summary>
        public IReadOnlyList<Light> Lights => lights;

        /// <summary>
        /// 맵 제작자가 목록을 채우지 않아도 동작하도록, 비어 있으면 자식에서 모은다.
        /// </summary>
        private void Awake()
        {
            if (lights == null || lights.Length == 0)
                lights = GetComponentsInChildren<Light>(true);
        }

        /// <summary>빛만 꺼지고 전구가 빛나 보이면 어색하므로 전구 머티리얼도 함께 바꾼다.</summary>
        public void SetOn(bool on)
        {
            IsOn = on;
            foreach (Light mapLight in lights)
                mapLight.enabled = on;

            if (bulbRenderer != null)
            {
                Material material = on ? onMaterial : offMaterial;
                if (material != null)
                    bulbRenderer.sharedMaterial = material;
            }
        }
    }
}
