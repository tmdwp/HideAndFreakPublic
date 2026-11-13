using FishNet.Connection;
using FishNet.Object;
using GuardVsMannequin.Core;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>소유자 권한 이동의 스피드핵·벽 통과를 막도록 서버에서 위치를 검증한다.</summary>
    public class MovementValidator : NetworkBehaviour
    {
        // 되돌림이 매 프레임 쏟아져 클라이언트가 덜덜 떨지 않게 하는 최소 간격(초)
        private const float CorrectionInterval = 0.25f;

        // 서버가 멈췄던 것으로 보는 프레임 간격(초).
        private const float StallThreshold = 0.5f;

        // 스폰 직후 정착·미끄러짐을 허용하는 이동 금지 중 수평 흔들림(m).
        private const float FrozenDriftAllowance = 0.5f;

        // 제자리 흔들림까지 매 프레임 검사하지 않도록 벽 통과 검사를 건너뛰는 이동 거리(m).
        private const float MinWallCheckDistance = 0.05f;

        private static readonly RaycastHit[] wallHits = new RaycastHit[16];

        [Tooltip("최대 속도에 곱하는 여유 배율.")]
        [SerializeField] private float speedTolerance = 1.3f;

        [Tooltip("몰아서 쓸 수 있는 이동 예산(초). 지연·프레임 멈춤 후 위치가 한꺼번에 도착하는 경우를 허용한다.")]
        [SerializeField] private float maxBurstSeconds = 3f;

        [Tooltip("예산에 더하는 고정 여유 거리(m).")]
        [SerializeField] private float extraSlack = 1f;

        [Tooltip("점프·계단 등으로 올라갈 수 있는 추가 높이(m).")]
        [SerializeField] private float riseAllowance = 2f;

        private ICharacterMotor motor;
        private Vector3 lastValidPosition;
        private float horizontalBudget;
        private float sprintBudget;
        private float riseBudget;
        private float nextCorrectionTime;
        private float lastBudgetTime;
        private bool isTracking;
        private bool isSuspended;

        /// <summary>
        /// 매 프레임 검사에서 GetComponent를 부르지 않도록 이동 구현을 미리 찾아 둔다.
        /// </summary>
        private void Awake()
        {
            motor = GetComponent<ICharacterMotor>();
        }

        /// <summary>
        /// 스폰 직후 첫 이동이 예산 부족으로 되돌려지지 않도록 예산을 가득 채운 채 시작한다.
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();
            lastValidPosition = transform.position;
            FillBudgets(GetAllowedSpeed());
            lastBudgetTime = Time.realtimeSinceStartup;
            isTracking = true;
        }

        /// <summary>위치는 소유자가 정하므로 서버가 매 프레임 비교해 부정 이동을 막는다.</summary>
        private void Update()
        {
            if (!isTracking || isSuspended || !IsServerInitialized || Owner == null || Owner.IsLocalClient)
                return;

            // Time.deltaTime은 멈춘 프레임에서 잘려 정상 이동을 부정으로 오판하므로 실제로 흐른 시간만큼 예산을 쌓는다.
            float now = Time.realtimeSinceStartup;
            float elapsed = now - lastBudgetTime;
            lastBudgetTime = now;

            float allowedSpeed = GetAllowedSpeed();
            float maxBudget = GetMaxBudget(allowedSpeed);

            // 서버 멈춤은 클라이언트가 일으킬 수 없으므로 그동안의 이동은 확인하지 않고 받아들인다.
            if (elapsed > StallThreshold)
            {
                lastValidPosition = transform.position;
                FillBudgets(allowedSpeed);
                return;
            }
            horizontalBudget = Mathf.Min(horizontalBudget + allowedSpeed * elapsed, maxBudget);
            sprintBudget = Mathf.Min(sprintBudget + SprintRules.GetExtraDistanceRecoverRate(allowedSpeed) * elapsed, SprintRules.GetMaxExtraDistance(allowedSpeed));
            riseBudget = Mathf.Min(riseBudget + allowedSpeed * elapsed, maxBudget + riseAllowance);

            Vector3 currentPosition = transform.position;
            Vector3 delta = currentPosition - lastValidPosition;
            float horizontalDistance = new Vector2(delta.x, delta.z).magnitude;
            float riseDistance = Mathf.Max(0f, delta.y);

            if (!MovementRules.CanMove(GameServices.CurrentStateOrWaiting))
            {
                // 스폰 직후 물리 정착으로 생기는 작은 흔들림은 허용하고, 떨어지는 것도 막지 않는다.
                if (horizontalDistance > FrozenDriftAllowance || riseDistance > FrozenDriftAllowance)
                    Correct(horizontalDistance, riseDistance);
                return;
            }

            if (horizontalDistance > horizontalBudget + sprintBudget || riseDistance > riseBudget)
            {
                Correct(horizontalDistance, riseDistance);
                return;
            }
            if (IsPathBlocked(lastValidPosition, currentPosition, out Collider wall))
            {
                CorrectThroughWall(wall);
                return;
            }

            ConsumeHorizontal(horizontalDistance);
            riseBudget -= riseDistance;
            lastValidPosition = currentPosition;
        }

        /// <summary>속도 검사만으로는 짧은 벽 뚫기를 막을 수 없어 몸 중심끼리 선을 그어 맵 충돌체를 지나는지 본다.</summary>
        private bool IsPathBlocked(Vector3 from, Vector3 to, out Collider wall)
        {
            wall = null;
            Vector3 centerOffset = Vector3.up * (motor.CapsuleHeight * 0.5f);
            Vector3 start = from + centerOffset;
            Vector3 path = to + centerOffset - start;
            float distance = path.magnitude;
            if (distance < MinWallCheckDistance)
                return false;

            int count = Physics.RaycastNonAlloc(start, path / distance, wallHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hitCollider = wallHits[i].collider;
                // 정상 이동이 되돌려지지 않도록 자기 몸과 움직이는 것은 빼고, 멈춘 변신 사물은 지형처럼 벽으로 본다.
                if (hitCollider.transform.IsChildOf(transform) || (hitCollider.attachedRigidbody != null && !PropPhysicsRules.IsRestingProp(hitCollider.attachedRigidbody)))
                    continue;

                wall = hitCollider;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 규칙에 따른 순간이동(가드 출발 등)이 부정 이동으로 되돌려지지 않도록 검증 기준 위치도 함께 옮긴다.
        /// </summary>
        [Server]
        public void ServerTeleport(Vector3 position, Quaternion rotation)
        {
            lastValidPosition = position;
            transform.SetPositionAndRotation(position, rotation);
            nextCorrectionTime = Time.time + CorrectionInterval;
            TeleportTargetRpc(Owner, position, rotation);
        }

        /// <summary>날아가는 동안에는 이동 속도 규칙을 따르지 않으므로 검증을 멈춘다.</summary>
        [Server]
        public void SetSuspended(bool suspended)
        {
            isSuspended = suspended;
            if (suspended)
                return;

            lastValidPosition = transform.position;
            FillBudgets(GetAllowedSpeed());
            lastBudgetTime = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// 위치는 소유자 권한이라 서버가 옮겨도 소유자가 다시 덮어쓰므로, 소유자에게도 옮기라고 알린다.
        /// </summary>
        [TargetRpc]
        private void TeleportTargetRpc(NetworkConnection target, Vector3 position, Quaternion rotation)
        {
            motor.Teleport(position, rotation);
        }

        /// <summary>
        /// 검증을 새로 시작하는 순간에 정상 이동이 막히지 않도록 모든 예산을 가득 채운다.
        /// </summary>
        private void FillBudgets(float allowedSpeed)
        {
            horizontalBudget = GetMaxBudget(allowedSpeed);
            sprintBudget = SprintRules.GetMaxExtraDistance(allowedSpeed);
            riseBudget = horizontalBudget + riseAllowance;
        }

        /// <summary>
        /// 걷기만으로 갈 수 있는 거리에 달리기 예산을 쓰면 스태미나 계산과 어긋나므로, 기본 예산부터 쓴다.
        /// </summary>
        private void ConsumeHorizontal(float horizontalDistance)
        {
            float fromBaseBudget = Mathf.Min(horizontalDistance, horizontalBudget);
            horizontalBudget -= fromBaseBudget;
            sprintBudget = Mathf.Max(0f, sprintBudget - (horizontalDistance - fromBaseBudget));
        }

        /// <summary>벽타기 중에는 비행 속도를 쓰므로 둘 중 큰 값에 여유 배율을 곱한다.</summary>
        private float GetAllowedSpeed()
        {
            return Mathf.Max(motor.MaxWalkSpeed, motor.MaxFlySpeed) * speedTolerance;
        }

        /// <summary>지연으로 몰려 오는 위치를 받아 주되 끝없이 쌓이지 않도록 둔 상한(m).</summary>
        private float GetMaxBudget(float allowedSpeed)
        {
            return allowedSpeed * maxBurstSeconds + extraSlack;
        }

        /// <summary>
        /// 원인을 로그로 남겨 오판을 조사할 수 있도록 이동 거리와 예산을 함께 적는다.
        /// </summary>
        private void Correct(float horizontalDistance, float riseDistance)
        {
            SendCorrection($"수평 {horizontalDistance:F1}m / 예산 {horizontalBudget:F1}m + 달리기 {sprintBudget:F1}m, 상승 {riseDistance:F1}m");
        }

        /// <summary>
        /// 어느 충돌체가 문제였는지 알아야 맵 오류와 부정 이동을 구분할 수 있어서 이름을 남긴다.
        /// </summary>
        private void CorrectThroughWall(Collider wall)
        {
            SendCorrection($"벽 통과 ('{wall.name}')");
        }

        /// <summary>
        /// 서버 위치는 매번 되돌리지만, 전송과 로그는 간격을 둬서 클라이언트가 덜덜 떨거나 로그가 넘치지 않게 한다.
        /// </summary>
        private void SendCorrection(string reason)
        {
            transform.position = lastValidPosition;
            if (Time.time < nextCorrectionTime)
                return;

            nextCorrectionTime = Time.time + CorrectionInterval;
            Debug.LogWarning($"[MovementValidator] 비정상 이동 되돌림: 연결 {Owner.ClientId}, {reason}, 상태 {GameServices.CurrentStateOrWaiting}");
            CorrectPositionTargetRpc(Owner, lastValidPosition);
        }

        /// <summary>
        /// 보간하면 벽 너머에서 미끄러져 돌아오는 것처럼 보여서, 보간 없이 바로 옮기고 남은 속도도 없앤다.
        /// </summary>
        [TargetRpc]
        private void CorrectPositionTargetRpc(NetworkConnection target, Vector3 position)
        {
            motor.Teleport(position, null);
        }
    }
}
