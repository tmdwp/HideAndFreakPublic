using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>
    /// 풀(<see cref="EffectPool"/>)에서 꺼내 쓰는 이펙트 하나. 파티클·꼬리를 처음부터 다시 재생하고, 정한 시간이 지나면 풀로 돌아간다.
    /// 밝기·크기 조정(<see cref="EffectTuning"/>)은 원본에 곱해지는 방식이라 만들 때 한 번만 하고, 그 값을 기억해 다른 값이면 새로 만든다.
    /// </summary>
    public class PooledEffect : MonoBehaviour
    {
        private ParticleSystem[] particleSystems;
        private TrailRenderer[] trails;
        private float releaseTime = float.PositiveInfinity;

        /// <summary>이 이펙트를 만든 원본 프리팹. (풀 구분용)</summary>
        public GameObject SourcePrefab { get; private set; }

        /// <summary>만들 때 적용한 밝기 배율.</summary>
        public float Brightness { get; private set; }

        /// <summary>만들 때 적용한 크기 배율.</summary>
        public float Scale { get; private set; }

        /// <summary>
        /// 처음 만들었을 때 원본·조정값을 기록하고 파티클 목록을 캐시한다.
        /// </summary>
        public void Initialize(GameObject sourcePrefab, float brightness, float scale)
        {
            SourcePrefab = sourcePrefab;
            Brightness = brightness;
            Scale = scale;
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            trails = GetComponentsInChildren<TrailRenderer>(true);
        }

        /// <summary>
        /// 남은 파티클과 꼬리를 지우고 처음부터 재생한다. lifetime(초)이 지나면 풀로 돌아간다.
        /// </summary>
        public void Restart(float lifetime)
        {
            foreach (TrailRenderer trail in trails)
                trail.Clear();
            foreach (ParticleSystem particles in particleSystems)
            {
                particles.Clear(true);
                particles.Play(true);
            }
            releaseTime = Time.time + lifetime;
        }

        /// <summary>
        /// 파티클 방출만 멈춘다. (이미 나온 입자는 자연스럽게 사라진다) delay(초) 뒤 풀로 돌아간다.
        /// </summary>
        public void StopEmitting(float delay)
        {
            foreach (ParticleSystem particles in particleSystems)
                particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            releaseTime = Time.time + delay;
        }

        /// <summary>
        /// 시간이 다 되면 풀로 돌아간다.
        /// </summary>
        private void Update()
        {
            if (Time.time >= releaseTime)
            {
                releaseTime = float.PositiveInfinity;
                EffectPool.Release(this);
            }
        }
    }
}
