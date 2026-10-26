using FishNet.Connection;
using FishNet.Object;
using GuardVsMannequin.Core;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 소유자 권한으로 움직이는 아바타의 위치를 서버에서 검증한다.
    /// - 이동 거리: 현재 최대 속도로 갈 수 있는 거리를 "예산"으로 쌓아 두고, 그보다 멀리 움직이면 되돌린다.
    ///   (네트워크 지연으로 위치가 몰려 도착해도 쌓인 예산 안이면 통과하므로 정상 플레이를 막지 않는다)
    /// - 달리기: 스태미나로 더 갈 수 있는 거리를 "달리기 예산"으로 따로 두고, 기본 예산을 넘는 이동에 쓴다.
    ///   스태미나와 같은 비율(<see cref="SprintRules"/>)로 회복되므로 스태미나 이상으로 계속 달리면 되돌려진다.
    /// - 판 시작 준비(카운트다운): 아무도 수평으로 움직일 수 없다(<see cref="MovementRules"/>). 작은 흔들림만 허용한다.
    /// - 벽 통과: 마지막으로 인정한 몸 중심에서 지금 몸 중심까지 선을 그어 맵 충돌체(벽·바닥·천장·사물)를 뚫으면 되돌린다.
    ///   리지드바디가 붙은 충돌체(다른 플레이어 등 움직이는 것, 멈춘 변신 사물은 제외)와 트리거, 자기 몸은 보지 않는다.
    ///   (캐릭터는 벽타기 중에도 회전하지 않으므로 몸 중심은 위치 + 위쪽 × 키의 절반이다)
    /// 위반하면 마지막으로 정상이던 위치로 소유자에게 되돌림을 보낸다.
    /// 떨어지는 움직임(아래 방향)은 검사하지 않는다. 호스트 자신의 아바타는 검사하지 않는다.
    /// </summary>
    public class MovementValidator : NetworkBehaviour
    {
        private const float CorrectionInterval = 0.25f;

        // 서버 프레임 사이 간격이 이보다 길면 서버가 멈췄던 것으로 본다. (초)
        private const float StallThreshold = 0.5f;

        // 이동 금지 중 허용하는 수평 흔들림(m). (스폰 직후 물리 정착, 경사면 미끄러짐)
        private const float FrozenDriftAllowance = 0.5f;

        // 이보다 짧은 이동은 벽 통과 검사를 하지 않는다(m). (제자리 흔들림까지 매 프레임 검사하지 않도록)
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
        /// 구성 요소를 캐시한다.
        /// </summary>
        private void Awake()
        {
            motor = GetComponent<ICharacterMotor>();
        }

        /// <summary>
        /// 서버에서 스폰 위치를 기준으로 검증을 시작한다.
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();
            lastValidPosition = transform.position;
            FillBudgets(GetAllowedSpeed());
            lastBudgetTime = Time.realtimeSinceStartup;
            isTracking = true;
        }

        /// <summary>
        /// 서버에서 매 프레임 위치 변화를 검사한다.
        /// </summary>
        private void Update()
        {
            if (!isTracking || isSuspended || !IsServerInitialized || Owner == null || Owner.IsLocalClient)
                return;

            // 실제로 흐른 시간만큼 예산을 쌓는다. Time.deltaTime은 프레임이 멈추면 최대 0.33초로 잘려,
            // 멈춘 동안 클라이언트가 정상적으로 움직인 거리를 부정 이동으로 오판하게 된다.
            float now = Time.realtimeSinceStartup;
            float elapsed = now - lastBudgetTime;
            lastBudgetTime = now;

            float speed = GetAllowedSpeed();
            float maxBudget = GetMaxBudget(speed);

            // 서버가 오래 멈췄다가(에디터 일시정지, 로딩 등) 돌아오면 그동안의 이동은 확인할 수 없으므로 받아들인다.
            // 서버 멈춤은 클라이언트가 일으킬 수 없으므로 부정 이동에 악용되지 않는다.
            if (elapsed > StallThreshold)
            {
                lastValidPosition = transform.position;
                FillBudgets(speed);
                return;
            }
            horizontalBudget = Mathf.Min(horizontalBudget + speed * elapsed, maxBudget);
            sprintBudget = Mathf.Min(sprintBudget + SprintRules.GetExtraDistanceRecoverRate(speed) * elapsed, SprintRules.GetMaxExtraDistance(speed));
            riseBudget = Mathf.Min(riseBudget + speed * elapsed, maxBudget + riseAllowance);

            Vector3 current = transform.position;
            Vector3 delta = current - lastValidPosition;
            float horizontal = new Vector2(delta.x, delta.z).magnitude;
            float rise = Mathf.Max(0f, delta.y);

            if (!MovementRules.CanMove(GameServices.CurrentStateOrWaiting))
            {
                // 이동 금지 중: 기준 위치(스폰 위치)에서 거의 벗어나지 않아야 한다. 떨어지는 것은 허용한다.
                if (horizontal > FrozenDriftAllowance || rise > FrozenDriftAllowance)
                    Correct(horizontal, rise);
                return;
            }

            if (horizontal > horizontalBudget + sprintBudget || rise > riseBudget)
            {
                Correct(horizontal, rise);
                return;
            }
            if (IsPathBlocked(lastValidPosition, current, out Collider wall))
            {
                CorrectThroughWall(wall);
                return;
            }

            ConsumeHorizontal(horizontal);
            riseBudget -= rise;
            lastValidPosition = current;
        }

        /// <summary>
        /// 두 위치의 몸 중심을 잇는 선이 맵 충돌체를 뚫는지 확인한다. 뚫으면 막은 충돌체를 돌려준다.
        /// </summary>
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
                Collider hit = wallHits[i].collider;
                // 자기 몸(변신 사물 포함)과 움직이는 것(다른 플레이어, 날아가는 사물 등)은 벽으로 보지 않는다.
                // 멈춰 있는 변신 사물은 물리 몸체가 있어도 맵 지형으로 본다.
                if (hit.transform.IsChildOf(transform) || (hit.attachedRigidbody != null && !PropPhysicsRules.IsRestingProp(hit.attachedRigidbody)))
                    continue;

                wall = hit;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 서버가 아바타를 정해진 위치로 옮긴다. (가드 출발 등 규칙에 따른 순간이동)
        /// 검증 기준 위치도 함께 옮기므로 되돌림으로 처리되지 않는다.
        /// </summary>
        [Server]
        public void ServerTeleport(Vector3 position, Quaternion rotation)
        {
            lastValidPosition = position;
            transform.SetPositionAndRotation(position, rotation);
            nextCorrectionTime = Time.time + CorrectionInterval;
            TeleportTargetRpc(Owner, position, rotation);
        }

        /// <summary>
        /// 검증을 잠시 멈추거나 다시 시작한다. (변신한 마네킹이 서버 물리로 날아가는 동안 — 이동 속도 규칙을 따르지 않음)
        /// 다시 시작할 때는 지금 위치를 기준으로 예산을 가득 채운다.
        /// </summary>
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
        /// 소유자 클라이언트에서 캐릭터를 순간이동시킨다. (보간을 잠시 꺼 미끄러지듯 보이지 않게 한다)
        /// </summary>
        [TargetRpc]
        private void TeleportTargetRpc(NetworkConnection target, Vector3 position, Quaternion rotation)
        {
            motor.Teleport(position, rotation);
        }

        /// <summary>
        /// 모든 예산을 가득 채운다. (검증 시작, 서버 멈춤 후)
        /// </summary>
        private void FillBudgets(float speed)
        {
            horizontalBudget = GetMaxBudget(speed);
            sprintBudget = SprintRules.GetMaxExtraDistance(speed);
            riseBudget = horizontalBudget + riseAllowance;
        }

        /// <summary>
        /// 수평 이동 거리를 기본 예산에서 먼저 쓰고, 모자라면 달리기 예산에서 쓴다.
        /// </summary>
        private void ConsumeHorizontal(float horizontal)
        {
            float fromBase = Mathf.Min(horizontal, horizontalBudget);
            horizontalBudget -= fromBase;
            sprintBudget = Mathf.Max(0f, sprintBudget - (horizontal - fromBase));
        }

        /// <summary>
        /// 현재 설정의 걷기/비행(벽타기) 속도 중 큰 값에 여유 배율을 곱한다. (MoveSpeedController가 서버에도 적용, 달리기 배율은 제외)
        /// </summary>
        private float GetAllowedSpeed()
        {
            return Mathf.Max(motor.MaxWalkSpeed, motor.MaxFlySpeed) * speedTolerance;
        }

        /// <summary>
        /// 쌓을 수 있는 최대 이동 예산(m).
        /// </summary>
        private float GetMaxBudget(float speed)
        {
            return speed * maxBurstSeconds + extraSlack;
        }

        /// <summary>
        /// 이동 거리 위반: 서버 위치를 되돌리고, 소유자에게 되돌림을 보낸다.
        /// </summary>
        private void Correct(float horizontal, float rise)
        {
            SendCorrection($"수평 {horizontal:F1}m / 예산 {horizontalBudget:F1}m + 달리기 {sprintBudget:F1}m, 상승 {rise:F1}m");
        }

        /// <summary>
        /// 벽 통과 위반: 서버 위치를 되돌리고, 소유자에게 되돌림을 보낸다.
        /// </summary>
        private void CorrectThroughWall(Collider wall)
        {
            SendCorrection($"벽 통과 ('{wall.name}')");
        }

        /// <summary>
        /// 서버 위치를 마지막 정상 위치로 되돌리고 소유자에게 알린다. (너무 잦은 전송·로그는 막는다)
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
        /// 소유자 클라이언트에서 캐릭터를 서버가 인정한 위치로 옮기고 속도를 없앤다. (보간 없이 바로 옮긴다)
        /// </summary>
        [TargetRpc]
        private void CorrectPositionTargetRpc(NetworkConnection target, Vector3 position)
        {
            motor.Teleport(position, null);
        }
    }
}
