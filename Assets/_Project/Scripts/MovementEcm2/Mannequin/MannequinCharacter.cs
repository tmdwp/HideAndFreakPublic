using ECM2;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>평소에는 3인칭, 사냥 시간에는 눈 위치 1인칭으로 바뀌는 마네킹 캐릭터.</summary>
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
        private IEyePointProvider eyePoint;

        /// <summary>몸 회전 방식이 달라지는 사냥 시간 1인칭 여부.</summary>
        public bool IsFirstPerson { get; private set; }

        /// <summary>3인칭에서는 몸이 이동 방향을 보게 하고, 벽타기 중 입력을 떼면 바로 멈추도록 감속을 크게 둔다.</summary>
        protected override void Awake()
        {
            base.Awake();
            SetRotationMode(RotationMode.OrientRotationToMovement);

            brakingDecelerationFlying = 20f;

            cameraYaw = transform.eulerAngles.y;
            movementPlaneProvider = GetComponent<IMovementPlaneProvider>();
            eyePoint = GetComponent<IEyePointProvider>();
        }

        /// <summary>1인칭으로 바뀌는 순간 엉뚱한 방향을 보지 않도록 몸 정면으로 시점을 초기화한다.</summary>
        public void SetFirstPerson(bool firstPerson)
        {
            IsFirstPerson = firstPerson;
            cameraPitch = firstPerson ? 0f : 15f;
            cameraYaw = transform.eulerAngles.y;
        }

        /// <summary>
        /// 3인칭에서는 카메라만 몸 주위를 돌고, 1인칭에서는 시선이 곧 몸 방향이라 좌우 입력으로 몸을 돌린다.
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
        /// 몸에 반영된 입력을 카메라가 한 번 더 더하면 두 배로 돌아가므로 대기 입력도 함께 비운다.
        /// </summary>
        protected override void ConsumeRotationInput()
        {
            base.ConsumeRotationInput();
            pendingYaw.Consume();
        }

        /// <summary>
        /// 원격 아바타에는 카메라가 없으므로 카메라가 할당된 로컬 소유자에서만 갱신한다.
        /// </summary>
        private void LateUpdate()
        {
            if (camera == null || followTarget == null)
                return;

            UpdateCamera();
        }

        /// <summary>
        /// 3인칭 카메라가 벽 너머로 들어가 맵 밖이 보이지 않도록, 궤도 위치에 두되 벽에 가려지면 앞으로 당긴다.
        /// </summary>
        private void UpdateCamera()
        {
            if (IsFirstPerson)
            {
                // 몸은 물리 주기로만 돌아 화면이 끊겨 보이므로, 아직 몸에 반영되지 않은 좌우 입력까지 더해 바라본다.
                Quaternion viewRotation = Quaternion.Euler(cameraPitch, pendingYaw.ViewYaw(this), 0f);
                Vector3 eyePosition = eyePoint != null && eyePoint.TryGetEyePosition(out Vector3 headEye) ? headEye : followTarget.position;
                cameraTransform.SetPositionAndRotation(eyePosition, viewRotation);
                return;
            }

            ClampOrbitToSurface();

            Quaternion orbitRotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
            Vector3 pivotPosition = followTarget.position;
            Vector3 backDirection = orbitRotation * Vector3.back;

            float distance = followDistance;
            if (Physics.SphereCast(pivotPosition, cameraCollisionRadius, backDirection, out RaycastHit hit, followDistance,
                    cameraCollisionMask, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform))
            {
                distance = hit.distance;
            }

            cameraTransform.SetPositionAndRotation(pivotPosition + backDirection * distance, orbitRotation);
        }

        /// <summary>벽·천장에 붙어 있을 때 카메라가 면 안쪽만 보지 않도록 회전 각도를 제한한다.</summary>
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
                float yawOffset = Mathf.Clamp(Mathf.DeltaAngle(wallYaw, cameraYaw), -wallYawLimit, wallYawLimit);
                cameraYaw = wallYaw + yawOffset;
            }
        }
    }
}
