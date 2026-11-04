using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>
    /// 총알 이펙트를 끝점까지 직선으로 이동시키고, 도착하면 명중 이펙트를 남긴 뒤 풀로 돌아간다.
    /// (보이는 연출 전용. 판정은 서버 히트스캔이 이미 처리했다)
    /// 총알과 명중 이펙트는 <see cref="EffectPool"/>에서 꺼내 다시 쓴다.
    /// </summary>
    [RequireComponent(typeof(PooledEffect))]
    public class BulletEffect : MonoBehaviour
    {
        // 도착 후 꼬리(Trail) 파티클이 자연스럽게 사라질 시간
        private const float FadeOutTime = 0.6f;
        private const float HitLifetime = 2f;

        // 도착하지 못해도(아주 먼 끝점) 이 시간이 지나면 풀로 돌아간다.
        private const float MaxFlightTime = 5f;

        private PooledEffect pooled;
        private Vector3 target;
        private float speed;
        private GameObject hitPrefab;
        private float hitBrightness = 1f;
        private float hitScale = 1f;
        private bool arrived = true;

        /// <summary>
        /// 풀 이펙트 컴포넌트를 캐시한다.
        /// </summary>
        private void Awake()
        {
            pooled = GetComponent<PooledEffect>();
        }

        /// <summary>
        /// 이동을 시작한다. (풀에서 다시 꺼낼 때마다 호출된다)
        /// </summary>
        /// <param name="targetPoint">도착 지점.</param>
        /// <param name="moveSpeed">이동 속도(m/s).</param>
        /// <param name="hitEffectPrefab">도착 시 남길 이펙트. 없으면 null.</param>
        /// <param name="hitEffectBrightness">명중 이펙트 밝기 배율.</param>
        /// <param name="hitEffectScale">명중 이펙트 크기 배율.</param>
        public void Launch(Vector3 targetPoint, float moveSpeed, GameObject hitEffectPrefab, float hitEffectBrightness, float hitEffectScale)
        {
            target = targetPoint;
            speed = Mathf.Max(1f, moveSpeed);
            hitPrefab = hitEffectPrefab;
            hitBrightness = hitEffectBrightness;
            hitScale = hitEffectScale;
            arrived = false;
            pooled.Restart(MaxFlightTime);
        }

        /// <summary>
        /// 끝점을 향해 이동하고, 도착하면 명중 이펙트를 만든 뒤 파티클 방출을 멈춘다.
        /// </summary>
        private void Update()
        {
            if (arrived)
                return;

            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            if ((transform.position - target).sqrMagnitude > 0.0001f)
                return;

            arrived = true;
            if (hitPrefab != null)
                EffectPool.Spawn(hitPrefab, target, Quaternion.LookRotation(-transform.forward), null, hitBrightness, hitScale, HitLifetime);

            pooled.StopEmitting(FadeOutTime);
        }
    }
}
