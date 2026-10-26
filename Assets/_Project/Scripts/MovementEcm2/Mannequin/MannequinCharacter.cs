using ECM2;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>
    /// 마네킹 캐릭터.
    /// - 3인칭(기본): 카메라는 추적 대상을 중심으로 궤도 회전하고, 몸체는 이동 방향을 바라본다.
    /// - 1인칭(사냥 시간): 카메라가 눈 위치에 붙고, 좌우 시점이 몸체를 회전시킨다.
    /// </summary>
    public class MannequinCharacter : PlayerCharacter, ILookControllable, IPerspectiveCamera
    {
        [Space(15f)]
        [Tooltip("카메라가 바라볼 추적 대상(어깨 높이).")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private float followDistance = 3.5f;
        [SerializeField] private float minPitch = -60f;
        [SerializeField] private float maxPitch = 75f;

        [Tooltip("벽에 붙었을 때 벽을 정면으로 보는 방향 기준 좌우 회전 제한(도).")]
        [SerializeField] private float wallYawLimit = 75f;

        [Tooltip("카메라가 벽을 뚫지 않도록 검사할 레이어.")]
        [SerializeField] private LayerMask cameraCollisionMask = ~0;
        [SerializeField] private float cameraCollisionRadius = 0.2f;

        [Tooltip("1인칭에서의 상하 시점 제한(도).")]
        [SerializeField] private float firstPersonPitchLimit = 80f;

        private float cameraYaw;
        private float cameraPitch = 15f;
        private readonly PendingLookYaw pendingYaw = new();
        private IMovementPlaneProvider movementPlaneProvider;

        /// <summary>1인칭 시점인지 여부. (사냥 시간)</summary>
        public bool IsFirstPerson { get; private set; }

        /// <summary>
        /// 회전 방식을 적용한다. (이동 속도는 MoveSpeedController가 게임 설정으로 적용)
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            SetRotationMode(RotationMode.OrientRotationToMovement);

            // 벽타기는 Flying 모드를 쓰므로 입력을 떼면 바로 멈추게 한다.
            brakingDecelerationFlying = 20f;

            cameraYaw = transform.eulerAngles.y;
            movementPlaneProvider = GetComponent<IMovementPlaneProvider>();
        }

        /// <summary>
        /// 1인칭/3인칭 시점을 전환한다. 1인칭에서는 몸의 정면을 바라보도록 시점을 초기화한다.
        /// 몸체 회전 방식은 <see cref="DisguiseRotationAbility"/>가 이 값을 보고 결정한다.
        /// </summary>
        public void SetFirstPerson(bool firstPerson)
        {
            IsFirstPerson = firstPerson;
            cameraPitch = firstPerson ? 0f : 15f;
            cameraYaw = transform.eulerAngles.y;
        }

        /// <summary>
        /// 시점 입력을 반영한다. 3인칭은 카메라 궤도 각도, 1인칭은 몸체 Yaw와 카메라 Pitch에 적용한다.
        /// </summary>
        public void AddLookInput(Vector2 lookDelta)
        {
            if (IsFirstPerson)
            {
                if (lookDelta.x != 0f)
                {
                    AddYawInput(lookDelta.x);
                    pendingYaw.Add(lookDelta.x);
                }
                cameraPitch = MathLib.ClampAngle(cameraPitch - lookDelta.y, -firstPersonPitchLimit, firstPersonPitchLimit);
                return;
            }

            cameraYaw = MathLib.ClampAngle(cameraYaw + lookDelta.x, -180f, 180f);
            cameraPitch = MathLib.ClampAngle(cameraPitch - lookDelta.y, minPitch, maxPitch);
        }

        /// <summary>
        /// 모아 둔 좌우 입력이 물리 단계에서 몸에 반영되면 카메라용 대기 입력도 비운다.
        /// </summary>
        protected override void ConsumeRotationInput()
        {
            base.ConsumeRotationInput();
            pendingYaw.Consume();
        }

        /// <summary>
        /// 카메라가 할당된 경우(로컬 소유자)에만 카메라 위치와 회전을 갱신한다.
        /// </summary>
        private void LateUpdate()
        {
            if (camera == null || followTarget == null)
                return;

            UpdateCamera();
        }

        /// <summary>
        /// 추적 대상 뒤쪽 궤도 위치에 카메라를 배치하고, 벽에 가려지면 앞으로 당긴다.
        /// </summary>
        private void UpdateCamera()
        {
            if (IsFirstPerson)
            {
                // 눈 위치(추적 대상)에서 몸의 정면(+아직 몸에 반영되지 않은 좌우 입력) + 상하 시점을 바라본다.
                Quaternion viewRotation = Quaternion.Euler(cameraPitch, pendingYaw.ViewYaw(this), 0f);
                cameraTransform.SetPositionAndRotation(followTarget.position, viewRotation);
                return;
            }

            ClampOrbitToSurface();

            Quaternion orbitRotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
            Vector3 pivot = followTarget.position;
            Vector3 back = orbitRotation * Vector3.back;

            float distance = followDistance;
            if (Physics.SphereCast(pivot, cameraCollisionRadius, back, out RaycastHit hit, followDistance,
                    cameraCollisionMask, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform))
            {
                distance = hit.distance;
            }

            cameraTransform.SetPositionAndRotation(pivot + back * distance, orbitRotation);
        }

        /// <summary>
        /// 벽이나 천장에 붙어 있을 때 카메라가 면 안쪽으로 돌아가지 않도록 회전 각도를 제한한다.
        /// - 벽: 벽을 정면으로 보는 방향 기준 좌우 wallYawLimit도까지
        /// - 천장: 카메라가 캐릭터 아래에서 올려다보도록 Pitch를 음수로 제한
        /// </summary>
        private void ClampOrbitToSurface()
        {
            Vector3 surfaceUp = movementPlaneProvider?.MovementPlaneNormal ?? -GetGravityDirection();

            if (surfaceUp.y < -0.7f)
            {
                cameraPitch = Mathf.Clamp(cameraPitch, minPitch, -10f);
            }
            else if (Mathf.Abs(surfaceUp.y) < 0.7f)
            {
                // 벽 법선의 반대 방향(벽을 바라보는 방향)의 Yaw
                float wallYaw = Mathf.Atan2(-surfaceUp.x, -surfaceUp.z) * Mathf.Rad2Deg;
                float offset = Mathf.Clamp(Mathf.DeltaAngle(wallYaw, cameraYaw), -wallYawLimit, wallYawLimit);
                cameraYaw = wallYaw + offset;
            }
        }
    }
}
