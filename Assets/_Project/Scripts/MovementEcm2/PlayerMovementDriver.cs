using ECM2;
using GuardVsMannequin.Core;
using GuardVsMannequin.Settings;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>로컬 소유자 아바타에서만 입력을 캐릭터의 이동과 시점에 적용한다.</summary>
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

        /// <summary>매 프레임 찾지 않도록 미리 찾아 두고, 프리팹마다 맞추지 않도록 점프 세기는 조정 파일에서 넣는다.</summary>
        private void Awake()
        {
            character = GetComponent<Character>();
            lookControllable = GetComponent<ILookControllable>();
            playerInput = GetComponent<IPlayerInput>();
            movementPlaneProvider = GetComponent<IMovementPlaneProvider>();

            character.jumpImpulse = GameTuning.JumpImpulse;

            // Character가 여러 개면 입력이 카메라가 없는 캐릭터로 전달되어 이동 방향이 고정된다.
            if (GetComponents<Character>().Length > 1)
                Debug.LogError($"[PlayerMovementDriver] {name}에 Character 컴포넌트가 여러 개 있습니다. 프리팹을 다시 생성하세요.", this);
        }

        /// <summary>
        /// 꺼진 뒤에도 마지막 입력이 남아 캐릭터가 계속 움직이거나 뛰지 않도록 비운다.
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
        /// 입력은 프레임마다 바뀌므로 매 프레임 이동·점프·시점을 캐릭터에 넘긴다.
        /// </summary>
        private void Update()
        {
            ApplyMovement();
            ApplyJump();
            ApplyLook();
        }

        /// <summary>누르고 있는 동안 계속 뛰지 않도록 누르는 순간과 떼는 순간에만 요청한다.</summary>
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

        /// <summary>카운트다운 중 되돌림이 생기지 않도록 입력을 0으로 만들고, 벽타기 중에도 같은 키로 움직이게 한다.</summary>
        private void ApplyMovement()
        {
            Vector2 moveInput = MovementRules.CanMove(GameServices.CurrentStateOrWaiting) ? playerInput.Move : Vector2.zero;
            Transform cameraTransform = character.cameraTransform;

            // 벽타기 중에는 벽/천장 면을, 평소에는 중력의 반대 방향을 이동 평면으로 사용한다.
            Vector3 planeNormal = movementPlaneProvider?.MovementPlaneNormal ?? -character.GetGravityDirection();

            Vector3 direction = cameraTransform != null
                ? CalculateCameraRelativeDirection(moveInput, cameraTransform, planeNormal)
                : new Vector3(moveInput.x, 0f, moveInput.y);

            character.SetMovementDirection(Vector3.ClampMagnitude(direction, 1f));
        }

        /// <summary>벽을 보거나 발밑을 볼 때도 움직이도록 이동 입력을 카메라 기준으로 이동 평면 위 방향으로 바꾼다.</summary>
        private static Vector3 CalculateCameraRelativeDirection(Vector2 moveInput, Transform cameraTransform, Vector3 surfaceUp)
        {
            Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, surfaceUp);
            if (forward.sqrMagnitude < 0.25f)
                forward = Vector3.ProjectOnPlane(cameraTransform.up, surfaceUp);
            forward.Normalize();

            Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, surfaceUp);
            right = right.sqrMagnitude > 0.01f ? right.normalized : Vector3.Cross(surfaceUp, forward);

            return forward * moveInput.y + right * moveInput.x;
        }

        /// <summary>
        /// 설정 화면에서 바꾼 감도가 바로 반영되도록 매 프레임 설정의 감도 배율을 곱해 넘긴다.
        /// </summary>
        private void ApplyLook()
        {
            Vector2 lookInput = playerInput.Look;
            float sensitivityScale = OptionsStore.Current.mouseSensitivity;
            lookControllable?.AddLookInput(Vector2.Scale(lookInput, lookSensitivity) * sensitivityScale);
        }
    }
}
