using ECM2;
using GuardVsMannequin.Core;
using GuardVsMannequin.Settings;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// <see cref="IPlayerInput"/>을 읽어 캐릭터의 이동과 시점에 적용한다.
    /// 1인칭/3인칭 구분 없이 동작하며, 로컬 소유자 아바타에서만 활성화된다.
    /// </summary>
    [RequireComponent(typeof(Character))]
    public class PlayerMovementDriver : MonoBehaviour
    {
        [Tooltip("기본 마우스 감도(픽셀당 회전 각도). 설정 화면의 감도 배율이 곱해진다.")]
        [SerializeField] private Vector2 lookSensitivity = new Vector2(0.1f, 0.1f);

        private Character character;
        private ILookControllable lookControllable;
        private IPlayerInput playerInput;
        private IMovementPlaneProvider movementPlaneProvider;
        private bool wasJumpHeld;

        /// <summary>
        /// 같은 오브젝트의 캐릭터와 입력 구현을 찾는다.
        /// </summary>
        private void Awake()
        {
            character = GetComponent<Character>();
            lookControllable = GetComponent<ILookControllable>();
            playerInput = GetComponent<IPlayerInput>();
            movementPlaneProvider = GetComponent<IMovementPlaneProvider>();

            // 점프 세기는 조정 파일(game_tuning.csv)을 따른다.
            character.jumpImpulse = GameTuning.JumpImpulse;

            // Character가 여러 개면 입력이 카메라가 없는 캐릭터로 전달되어 이동 방향이 고정된다.
            if (GetComponents<Character>().Length > 1)
                Debug.LogError($"[PlayerMovementDriver] {name}에 Character 컴포넌트가 여러 개 있습니다. 프리팹을 다시 생성하세요.", this);
        }

        /// <summary>
        /// 비활성화 시 남아 있는 이동 입력을 제거해 캐릭터가 계속 움직이지 않게 한다.
        /// </summary>
        private void OnDisable()
        {
            if (character != null)
            {
                character.SetMovementDirection(Vector3.zero);
                character.StopJumping();
            }
            wasJumpHeld = false;
        }

        /// <summary>
        /// 매 프레임 이동 방향, 점프, 시점 입력을 캐릭터에 전달한다.
        /// </summary>
        private void Update()
        {
            ApplyMovement();
            ApplyJump();
            ApplyLook();
        }

        /// <summary>
        /// 점프 키를 누르는 순간 점프를 요청하고, 떼면 끝낸다. (ECM2가 바닥에 있을 때만 뛰게 처리)
        /// 벽타기(Flying) 중에는 ECM2가 점프를 허용하지 않는다.
        /// </summary>
        private void ApplyJump()
        {
            bool isJumpHeld = playerInput.IsJumpHeld && MovementRules.CanMove(GameServices.CurrentStateOrWaiting);
            if (isJumpHeld == wasJumpHeld)
                return;

            wasJumpHeld = isJumpHeld;
            if (isJumpHeld)
                character.Jump();
            else
                character.StopJumping();
        }

        /// <summary>
        /// 카메라 기준 이동 방향을 계산해 캐릭터에 적용한다.
        /// </summary>
        private void ApplyMovement()
        {
            // 판 시작 준비(카운트다운) 중에는 움직이지 않는다. (서버도 같은 규칙으로 검증)
            Vector2 move = MovementRules.CanMove(GameServices.CurrentStateOrWaiting) ? playerInput.Move : Vector2.zero;
            Transform cameraTransform = character.cameraTransform;

            // 벽타기 중에는 벽/천장 면을, 평소에는 중력의 반대 방향을 이동 평면으로 사용한다.
            Vector3 planeNormal = movementPlaneProvider?.MovementPlaneNormal ?? -character.GetGravityDirection();

            Vector3 direction = cameraTransform != null
                ? CalculateCameraRelativeDirection(move, cameraTransform, planeNormal)
                : new Vector3(move.x, 0f, move.y);

            character.SetMovementDirection(Vector3.ClampMagnitude(direction, 1f));
        }

        /// <summary>
        /// 이동 입력을 카메라 기준으로, 현재 이동 평면(중력에 수직인 면) 위의 방향으로 변환한다.
        /// 카메라가 면을 거의 정면으로 보고 있으면(벽을 바라보거나 발밑을 내려다볼 때)
        /// 카메라 정면 대신 카메라 위쪽을 "앞"으로 사용한다. 벽에서는 W가 위로 오르기가 된다.
        /// </summary>
        private static Vector3 CalculateCameraRelativeDirection(Vector2 move, Transform cameraTransform, Vector3 surfaceUp)
        {
            Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, surfaceUp);
            if (forward.sqrMagnitude < 0.25f)
                forward = Vector3.ProjectOnPlane(cameraTransform.up, surfaceUp);
            forward.Normalize();

            Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, surfaceUp);
            right = right.sqrMagnitude > 0.01f ? right.normalized : Vector3.Cross(surfaceUp, forward);

            return forward * move.y + right * move.x;
        }

        /// <summary>
        /// 감도를 적용한 시점 입력을 캐릭터에 전달한다.
        /// </summary>
        private void ApplyLook()
        {
            Vector2 look = playerInput.Look;
            // 설정 화면의 마우스 감도 배율을 적용한다.
            float sensitivityScale = OptionsStore.Current.mouseSensitivity;
            lookControllable?.AddLookInput(Vector2.Scale(look, lookSensitivity) * sensitivityScale);
        }
    }
}
