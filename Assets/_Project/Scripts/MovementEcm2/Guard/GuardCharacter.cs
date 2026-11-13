using ECM2;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>조준이 중요한 가드를 위해 좌우는 몸, 상하는 카메라 피벗만 돌리는 1인칭 캐릭터.</summary>
    public class GuardCharacter : PlayerCharacter, ILookControllable, IPerspectiveCamera
    {
        [Space(15f)]
        [Tooltip("상하 회전을 적용할 카메라 피벗(눈 높이).")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;

        [Header("3인칭 (테스트 방)")]
        [Tooltip("3인칭 카메라 위치. 카메라 피벗 기준 (오른쪽, 위, 앞).")]
        [SerializeField] private Vector3 thirdPersonOffset = new Vector3(0.5f, 0.3f, -3f);

        [Tooltip("3인칭 카메라가 벽을 뚫지 않도록 검사할 레이어.")]
        [SerializeField] private LayerMask cameraCollisionMask = ~0;
        [SerializeField] private float cameraCollisionRadius = 0.2f;

        private float cameraPitch;
        private readonly PendingLookYaw pendingYaw = new();
        private bool isThirdPerson;
        private Vector3 firstPersonCameraPosition;
        private Quaternion firstPersonCameraRotation;

        /// <summary>기본은 1인칭이다.</summary>
        public bool IsFirstPerson => !isThirdPerson;

        /// <summary>1인칭은 시점 입력으로만 몸을 돌려야 하므로 ECM2의 자동 회전을 끈다.</summary>
        protected override void Awake()
        {
            base.Awake();
            SetRotationMode(RotationMode.None);
        }

        /// <summary>
        /// 위아래를 볼 때 몸이 기울지 않도록 좌우는 몸체 Yaw, 상하는 카메라 Pitch에만 반영한다.
        /// </summary>
        public void AddLookInput(Vector2 lookDelta)
        {
            if (lookDelta.x != 0f)
            {
                AddYawInput(lookDelta.x);
                pendingYaw.Add(lookDelta.x);
            }

            if (lookDelta.y != 0f)
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

        /// <summary>몸은 물리 주기로만 돌아 화면이 끊겨 보이므로 소유자는 카메라 좌우를 매 프레임 최신 입력으로 돌린다.</summary>
        private void LateUpdate()
        {
            if (cameraPivot == null)
                return;

            if (pendingYaw.HasInput)
                cameraPivot.rotation = Quaternion.Euler(cameraPitch, pendingYaw.ViewYaw(this), 0f);
            else
                cameraPivot.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);

            if (isThirdPerson && camera != null)
                PlaceThirdPersonCamera();
        }

        /// <summary>
        /// 3인칭에서 옮긴 카메라가 1인칭으로 돌아올 때 원래 눈 위치로 오도록, 바꾸기 전 자리를 기억했다가 되돌린다.
        /// </summary>
        public void SetFirstPerson(bool firstPerson)
        {
            if (firstPerson == IsFirstPerson || camera == null)
                return;

            if (!firstPerson)
            {
                firstPersonCameraPosition = cameraTransform.localPosition;
                firstPersonCameraRotation = cameraTransform.localRotation;
            }
            else
            {
                cameraTransform.SetLocalPositionAndRotation(firstPersonCameraPosition, firstPersonCameraRotation);
            }
            isThirdPerson = !firstPerson;
        }

        /// <summary>
        /// 카메라가 벽 너머로 들어가 맵 밖이 보이지 않도록, 피벗 뒤쪽에 두되 벽에 가려지면 앞으로 당긴다.
        /// </summary>
        private void PlaceThirdPersonCamera()
        {
            Vector3 pivotPosition = cameraPivot.position;
            Quaternion viewRotation = cameraPivot.rotation;
            Vector3 offset = viewRotation * thirdPersonOffset;
            float distance = offset.magnitude;
            if (distance < 0.01f)
                return;

            Vector3 direction = offset / distance;
            if (Physics.SphereCast(pivotPosition, cameraCollisionRadius, direction, out RaycastHit hit, distance,
                    cameraCollisionMask, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform))
            {
                distance = hit.distance;
            }
            cameraTransform.SetPositionAndRotation(pivotPosition + direction * distance, viewRotation);
        }

        /// <summary>
        /// 인스펙터에서 컴포넌트를 처음 붙이거나 초기화할 때도 1인칭에 맞는 회전 모드가 되도록 맞춘다.
        /// </summary>
        protected override void Reset()
        {
            base.Reset();
            SetRotationMode(RotationMode.None);
        }
    }
}
