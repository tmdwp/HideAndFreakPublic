using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>판정은 서버가 끝냈으므로 총구에서 끝점까지 보이는 사격 연출만 처리한다.</summary>
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
        /// 아바타가 움직여도 선과 탄착 표시가 쏜 자리에 남아야 하므로, 선은 월드 좌표로 두고 탄착 표시는 부모에서 떼어 낸다.
        /// </summary>
        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 2;
            lineRenderer.enabled = false;

            if (impactMarker != null)
            {
                impactMarker.SetParent(null, true);
                impactMarker.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 부모에서 떼어 낸 탄착 표시는 아바타와 함께 지워지지 않으므로 직접 지운다.
        /// </summary>
        private void OnDestroy()
        {
            if (impactMarker != null)
                Destroy(impactMarker.gameObject);
        }

        /// <summary>
        /// 코루틴 없이 표시 시간을 재려고 매 프레임 끝난 것만 숨긴다.
        /// </summary>
        private void Update()
        {
            if (lineRenderer.enabled && Time.time >= tracerHideTime)
                lineRenderer.enabled = false;

            if (impactMarker != null && impactMarker.gameObject.activeSelf && Time.time >= impactHideTime)
                impactMarker.gameObject.SetActive(false);
        }

        /// <summary>자연스럽게 보이도록 서버 원점 대신 보는 사람 기준 총구에서 시작하고 이펙트는 풀에서 꺼낸다.</summary>
        /// <param name="origin">총구가 없을 때 대신 쓰는 서버 판정 원점.</param>
        /// <param name="didHit">빗나갔을 때 명중 이펙트를 남기지 않으려고 받는다.</param>
        public void Show(Vector3 origin, Vector3 endPoint, bool didHit)
        {
            Transform muzzleTransform = GetMuzzle();
            Vector3 startPosition = muzzleTransform != null ? muzzleTransform.position : origin;

            if (muzzleFlashPrefab != null && muzzleTransform != null)
            {
                // 총과 함께 움직이도록 총구에 붙인다.
                EffectPool.Spawn(muzzleFlashPrefab, muzzleTransform.position, muzzleTransform.rotation, muzzleTransform, muzzleFlashBrightness, muzzleFlashScale, EffectLifetime);
            }

            if (bulletPrefab != null)
            {
                // 끝점과 시작점이 같으면 LookRotation이 경고를 내므로 아주 작은 값을 더한다.
                Quaternion bulletRotation = Quaternion.LookRotation(endPoint - startPosition + Vector3.forward * 0.0001f);
                PooledEffect bullet = EffectPool.Spawn(bulletPrefab, startPosition, bulletRotation, null, bulletBrightness, bulletScale, EffectLifetime);
                if (!bullet.TryGetComponent(out BulletEffect bulletEffect))
                    bulletEffect = bullet.gameObject.AddComponent<BulletEffect>();
                bulletEffect.Launch(endPoint, bulletSpeed, didHit ? hitPrefab : null, hitBrightness, hitScale);
                return;
            }

            ShowLine(startPosition, endPoint, didHit);
        }

        /// <summary>
        /// 1인칭·3인칭에서 총구 위치가 달라서, 총 외형이 보는 사람 기준 총구를 알려 주면 그것을 쓴다.
        /// </summary>
        private Transform GetMuzzle()
        {
            if (weaponView != null && weaponView.Muzzle != null)
                return weaponView.Muzzle;
            return muzzle;
        }

        /// <summary>
        /// 이펙트 에셋이 아직 없어도 사격이 보이도록 선과 탄착 표시로 대신한다.
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
