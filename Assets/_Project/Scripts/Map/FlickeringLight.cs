using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Map
{
    /// <summary>오래된 전구처럼 밝기가 흔들리고 가끔 꺼졌다 켜지는 조명.</summary>
    public class FlickeringLight : MonoBehaviour
    {
        [Tooltip("원래 밝기에 곱할 배율. 1이면 원래 밝기를 기준으로 깜빡인다.")]
        [SerializeField] private float intensityScale = 1f;

        [Tooltip("평소 흔들림 폭(0~1). 기준 밝기에서 이 비율만큼 어두워질 수 있다.")]
        [Range(0f, 1f)]
        [SerializeField] private float noiseAmount = 0.25f;

        [Tooltip("평소 흔들림 속도.")]
        [SerializeField] private float noiseSpeed = 6f;

        [Tooltip("1초에 깜빡임이 시작될 확률.")]
        [SerializeField] private float blinkChancePerSecond = 0.2f;

        [Tooltip("한 번 깜빡일 때 꺼져 있는 시간 범위(초).")]
        [SerializeField] private Vector2 blinkDuration = new Vector2(0.05f, 0.2f);

        [Tooltip("꺼져 있을 때 남는 밝기 비율.")]
        [Range(0f, 1f)]
        [SerializeField] private float blinkFloor = 0.05f;

        private Light[] targets = System.Array.Empty<Light>();
        private float[] originalIntensities = System.Array.Empty<float>();
        private float noiseSeed;
        private float blinkRemaining;

        /// <summary>전체를 어둡게 하면서도 깜빡임은 유지하도록 밖에서 바꿀 수 있게 연다.</summary>
        public float IntensityScale
        {
            get => intensityScale;
            set => intensityScale = Mathf.Max(0f, value);
        }

        /// <summary>
        /// 여러 조명이 똑같이 깜빡이면 기계적으로 보이므로 조명마다 노이즈 시작점을 다르게 하고, 기본 대상을 잡는다.
        /// </summary>
        private void Awake()
        {
            noiseSeed = Random.value * 100f;
            SetTargets(GetComponentsInChildren<Light>(true));
        }

        /// <summary>
        /// 깜빡임은 원래 밝기에 비율을 곱하는 방식이라, 대상을 바꿀 때 이전 대상을 먼저 되돌리고 새 대상의 지금 밝기를 기준으로 저장한다.
        /// </summary>
        public void SetTargets(IReadOnlyList<Light> lights)
        {
            RestoreIntensities();
            targets = new Light[lights.Count];
            originalIntensities = new float[lights.Count];
            for (int i = 0; i < lights.Count; i++)
            {
                targets[i] = lights[i];
                originalIntensities[i] = lights[i] != null ? lights[i].intensity : 0f;
            }
        }

        /// <summary>
        /// 배율을 바꿔도 깜빡임이 이어지도록 매 프레임 기준 밝기 × 깜빡임 비율로 다시 계산한다.
        /// </summary>
        private void Update()
        {
            ApplyRatio(intensityScale * NextRatio(Time.deltaTime));
        }

        /// <summary>
        /// 무작위 값만 쓰면 지글거리므로 평소에는 펄린 노이즈로 부드럽게 흔들고, 낮은 확률로 짧게 꺼뜨린다.
        /// </summary>
        private float NextRatio(float deltaTime)
        {
            if (blinkRemaining > 0f)
            {
                blinkRemaining -= deltaTime;
                return blinkFloor;
            }

            if (Random.value < blinkChancePerSecond * deltaTime)
            {
                blinkRemaining = Random.Range(blinkDuration.x, blinkDuration.y);
                return blinkFloor;
            }

            float noise = Mathf.PerlinNoise(Time.time * noiseSpeed, noiseSeed);
            return 1f - noiseAmount * noise;
        }

        /// <summary>
        /// 한 기구의 여러 조명이 함께 깜빡이도록 같은 비율을 모두에 적용한다.
        /// </summary>
        private void ApplyRatio(float ratio)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                    targets[i].intensity = originalIntensities[i] * ratio;
            }
        }

        /// <summary>
        /// 깜빡이다 멈춘 순간의 어두운 밝기로 남지 않도록 꺼질 때 원래 밝기로 되돌린다.
        /// </summary>
        private void OnDisable()
        {
            RestoreIntensities();
        }

        /// <summary>
        /// 저장해 둔 원래 밝기로 되돌리는 것을 비율 1 적용으로 처리한다.
        /// </summary>
        private void RestoreIntensities()
        {
            ApplyRatio(1f);
        }
    }
}
