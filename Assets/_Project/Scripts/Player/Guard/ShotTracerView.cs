using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>
    /// 사격 연출. 총구에서 총알 이펙트가 날아가 끝점에서 명중 이펙트를 보여준다.
    /// 판정은 서버의 히트스캔이 이미 끝낸 상태이며, 여기서는 보이는 것만 처리한다.
    /// 총알 이펙트가 지정되지 않았으면 예전처럼 선(LineRenderer)과 탄착 표시로 대신한다.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class ShotTracerView : MonoBehaviour
    {
        private const float EffectLifetime = 2f;

        [SerializeField] private float tracerDuration = 0.15f;
        [SerializeField] private float impactDuration = 0.4f;

        [Tooltip("총구를 제공하는 총 외형. 있으면 보는 사람 기준 총구에서 연출을 시작한다.")]
        [SerializeField] private GuardWeaponView weaponView;

        [Tooltip("총 외형이 없을 때의 연출 시작점.")]
        [SerializeField] private Transform muzzle;

        [Tooltip("탄착 지점에 잠깐 표시할 오브젝트. (총알 이펙트가 없을 때) 실행 시 아바타에서 분리된다.")]
        [SerializeField] private Transform impactMarker;

        [Header("이펙트 (비우면 선으로 표시)")]
        [Tooltip("총구에서 끝점까지 날아가는 총알 이펙트.")]
        [SerializeField] private GameObject bulletPrefab;

        [Tooltip("끝점에 부딪혔을 때의 이펙트.")]
        [SerializeField] private GameObject hitPrefab;

        [Tooltip("총구 화염 이펙트.")]
        [SerializeField] private GameObject muzzleFlashPrefab;

        [Tooltip("총알 이펙트의 이동 속도(m/s). 판정은 즉시 끝나므로 보기 좋은 속도로 둔다.")]
        [SerializeField] private float bulletSpeed = 90f;

        [Header("이펙트 밝기·크기 (1 = 원본, Play 중 수정하면 다음 사격부터 반영)")]
        [Range(0f, 1f)] [SerializeField] private float bulletBrightness = 0.35f;
        [Range(0.1f, 2f)] [SerializeField] private float bulletScale = 0.6f;
        [Range(0f, 1f)] [SerializeField] private float hitBrightness = 0.4f;
        [Range(0.1f, 2f)] [SerializeField] private float hitScale = 0.6f;
        [Range(0f, 1f)] [SerializeField] private float muzzleFlashBrightness = 0.35f;
        [Range(0.1f, 2f)] [SerializeField] private float muzzleFlashScale = 0.4f;

        private LineRenderer lineRenderer;
        private float tracerHideTime;
        private float impactHideTime;

        /// <summary>
        /// 라인 렌더러를 월드 좌표 모드로 초기화하고, 탄착 표시를 아바타에서 분리한다.
        /// </summary>
        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 2;
            lineRenderer.enabled = false;

            if (impactMarker != null)
            {
                // 아바타가 움직여도 탄착 표시는 맞은 자리에 남아야 하므로 부모에서 분리한다.
                impactMarker.SetParent(null, true);
                impactMarker.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 아바타와 함께 분리된 탄착 표시도 제거한다.
        /// </summary>
        private void OnDestroy()
        {
            if (impactMarker != null)
                Destroy(impactMarker.gameObject);
        }

        /// <summary>
        /// 표시 시간이 지나면 선과 탄착 표시를 숨긴다.
        /// </summary>
        private void Update()
        {
            if (lineRenderer.enabled && Time.time >= tracerHideTime)
                lineRenderer.enabled = false;

            if (impactMarker != null && impactMarker.gameObject.activeSelf && Time.time >= impactHideTime)
                impactMarker.gameObject.SetActive(false);
        }

        /// <summary>
        /// 총구에서 끝점까지 사격을 연출한다.
        /// </summary>
        /// <param name="origin">서버가 판정에 사용한 원점. 총구가 없을 때 대신 사용한다.</param>
        /// <param name="endPoint">사격 끝점.</param>
        /// <param name="didHit">사거리 안에서 충돌했는지 여부.</param>
        public void Show(Vector3 origin, Vector3 endPoint, bool didHit)
        {
            Transform start = GetMuzzle();
            Vector3 startPosition = start != null ? start.position : origin;

            // 이펙트는 풀(EffectPool)에서 꺼내 다시 쓴다. (쏠 때마다 만들고 지우지 않도록)
            if (muzzleFlashPrefab != null && start != null)
            {
                // 총과 함께 움직이도록 총구에 붙인다.
                EffectPool.Spawn(muzzleFlashPrefab, start.position, start.rotation, start, muzzleFlashBrightness, muzzleFlashScale, EffectLifetime);
            }

            if (bulletPrefab != null)
            {
                Quaternion direction = Quaternion.LookRotation(endPoint - startPosition + Vector3.forward * 0.0001f);
                PooledEffect bullet = EffectPool.Spawn(bulletPrefab, startPosition, direction, null, bulletBrightness, bulletScale, EffectLifetime);
                if (!bullet.TryGetComponent(out BulletEffect effect))
                    effect = bullet.gameObject.AddComponent<BulletEffect>();
                effect.Launch(endPoint, bulletSpeed, didHit ? hitPrefab : null, hitBrightness, hitScale);
                return;
            }

            ShowLine(startPosition, endPoint, didHit);
        }

        /// <summary>
        /// 보는 사람 기준 총구를 고른다.
        /// </summary>
        private Transform GetMuzzle()
        {
            if (weaponView != null && weaponView.Muzzle != null)
                return weaponView.Muzzle;
            return muzzle;
        }

        /// <summary>
        /// 이펙트가 없을 때의 대체 연출: 선과 탄착 표시.
        /// </summary>
        private void ShowLine(Vector3 startPosition, Vector3 endPoint, bool didHit)
        {
            lineRenderer.SetPosition(0, startPosition);
            lineRenderer.SetPosition(1, endPoint);
            lineRenderer.enabled = true;
            tracerHideTime = Time.time + tracerDuration;

            if (didHit && impactMarker != null)
            {
                impactMarker.position = endPoint;
                impactMarker.gameObject.SetActive(true);
                impactHideTime = Time.time + impactDuration;
            }
        }
    }
}
