using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>풀에서 꺼낼 때마다 처음부터 재생하고 시간이 지나면 풀로 돌아가는 이펙트.</summary>
    public class PooledEffect : MonoBehaviour
    {
        private ParticleSystem[] particleSystems;
        private TrailRenderer[] trails;
        private float releaseTime = float.PositiveInfinity;

        /// <summary>풀이 프리팹별로 나눠 보관하므로 어느 프리팹에서 왔는지 기억한다.</summary>
        public GameObject SourcePrefab { get; private set; }

        /// <summary>조정은 곱해지는 방식이라 다시 적용할 수 없어서, 다른 값이 요청되면 새로 만들도록 기억해 둔다.</summary>
        public float Brightness { get; private set; }

        /// <summary>조정은 곱해지는 방식이라 다시 적용할 수 없어서, 다른 값이 요청되면 새로 만들도록 기억해 둔다.</summary>
        public float Scale { get; private set; }

        /// <summary>
        /// 다시 꺼낼 때마다 GetComponentsInChildren을 부르지 않도록 처음 만들 때 한 번만 찾아 둔다.
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
        /// 이전 사용 때 남은 입자·꼬리가 새 위치까지 이어져 보이지 않도록 지우고 처음부터 재생한다.
        /// </summary>
        /// <param name="lifetime">이 시간(초)이 지나면 풀로 돌아간다.</param>
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
        /// 바로 끄면 이미 나온 입자가 뚝 끊겨 보이므로 방출만 멈추고 자연스럽게 사라질 시간을 준다.
        /// </summary>
        /// <param name="delay">이 시간(초) 뒤 풀로 돌아간다.</param>
        public void StopEmitting(float delay)
        {
            foreach (ParticleSystem particles in particleSystems)
                particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            releaseTime = Time.time + delay;
        }

        /// <summary>
        /// 코루틴을 쓰면 꺼낼 때마다 할당이 생기므로 시각 비교로 반환 시점을 잰다.
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
