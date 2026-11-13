using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>판정은 서버가 이미 끝냈으므로 보이는 총알만 끝점까지 날려 명중 이펙트를 남긴다.</summary>
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
        private bool hasArrived = true;

        /// <summary>
        /// 사격마다 GetComponent를 부르지 않도록 미리 찾아 둔다.
        /// </summary>
        private void Awake()
        {
            pooled = GetComponent<PooledEffect>();
        }

        /// <summary>풀에서 꺼낸 총알은 이전 상태를 가지므로 꺼낼 때마다 목표를 새로 정한다.</summary>
        /// <param name="moveSpeed">단위는 m/s.</param>
        /// <param name="hitEffectPrefab">빗나갔으면 명중 이펙트를 남기지 않도록 null.</param>
        public void Launch(Vector3 targetPoint, float moveSpeed, GameObject hitEffectPrefab, float hitEffectBrightness, float hitEffectScale)
        {
            target = targetPoint;
            speed = Mathf.Max(1f, moveSpeed);
            hitPrefab = hitEffectPrefab;
            hitBrightness = hitEffectBrightness;
            hitScale = hitEffectScale;
            hasArrived = false;
            pooled.Restart(MaxFlightTime);
        }

        /// <summary>
        /// 꼬리가 갑자기 끊기지 않도록 도착하면 바로 끄지 않고 방출만 멈춘 뒤 풀로 돌려보낸다.
        /// </summary>
        private void Update()
        {
            if (hasArrived)
                return;

            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            if ((transform.position - target).sqrMagnitude > 0.0001f)
                return;

            hasArrived = true;
            if (hitPrefab != null)
                EffectPool.Spawn(hitPrefab, target, Quaternion.LookRotation(-transform.forward), null, hitBrightness, hitScale, HitLifetime);

            pooled.StopEmitting(FadeOutTime);
        }
    }
}
