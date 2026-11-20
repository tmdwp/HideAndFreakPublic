using System.Collections.Generic;
using ECM2;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>게임 코드가 ECM2를 몰라도 되도록 ECM2 캐릭터를 ICharacterMotor로 노출하는 공통 기반.</summary>
    public abstract class PlayerCharacter : Character, ICharacterMotor
    {
        /// <inheritdoc />
        float ICharacterMotor.MaxWalkSpeed
        {
            get => maxWalkSpeed;
            set => maxWalkSpeed = value;
        }

        /// <inheritdoc />
        float ICharacterMotor.MaxFlySpeed
        {
            get => maxFlySpeed;
            set => maxFlySpeed = value;
        }

        /// <inheritdoc />
        float ICharacterMotor.CapsuleRadius => characterMovement.radius;

        /// <inheritdoc />
        float ICharacterMotor.CapsuleHeight => characterMovement.height;

        /// <inheritdoc />
        void ICharacterMotor.SetCapsule(float radius, float height)
        {
            characterMovement.SetDimensions(radius, height);
        }

        private ShapeDepenetration shapeDepenetration;

        /// <inheritdoc />
        void ICharacterMotor.SetShapeBoxes(IReadOnlyList<BoxCollider> shapeBoxes)
        {
            // 변신하지 않는 캐릭터는 검사가 필요 없으므로 처음 모양을 받을 때 연결한다.
            if (shapeDepenetration == null)
            {
                shapeDepenetration = new ShapeDepenetration(characterMovement);
                CharacterMovementUpdated += OnShapeMovementUpdated;
            }
            shapeDepenetration.SetShapes(shapeBoxes);
        }

        /// <summary>ECM2가 캡슐 이동을 끝낸 직후에 밀어내야 같은 틱에 반영되고 화면에서 박힌 모습이 보이지 않는다.</summary>
        private void OnShapeMovementUpdated(float deltaTime)
        {
            // 날아가는 동안에는 물리가 위치를 정하므로 밀지 않는다.
            if (movementMode != MovementMode.None)
                shapeDepenetration.Resolve();
        }

        /// <inheritdoc />
        Transform ICharacterMotor.ViewTransform => cameraTransform;

        /// <inheritdoc />
        void ICharacterMotor.SetupAsOwner(Camera playerCamera)
        {
            camera = playerCamera;
            // ECM2는 물리 주기로만 움직이므로 보간이 없으면 화면이 끊겨 보인다.
            characterMovement.interpolation = RigidbodyInterpolation.Interpolate;
        }

        /// <inheritdoc />
        void ICharacterMotor.SetupAsRemote()
        {
            // 원격 위치는 NetworkTransform이 정하므로, 시뮬레이션과 보간이 끼어들어 위치를 흔들지 않도록 둘 다 끈다.
            enabled = false;
            characterMovement.interpolation = RigidbodyInterpolation.None;
        }

        /// <inheritdoc />
        void ICharacterMotor.Teleport(Vector3 position, Quaternion? rotation)
        {
            TeleportPosition(position);
            if (rotation.HasValue)
                TeleportRotation(rotation.Value);
            SetVelocity(Vector3.zero);
        }

        /// <inheritdoc />
        public event System.Action<Collider, Vector3, Vector3> SideCollided;

        /// <summary>게임 코드가 ECM2 충돌 구조체를 몰라도 되도록 옆면 충돌만 골라 넘긴다.</summary>
        protected override void OnCollided(ref CollisionResult collisionResult)
        {
            base.OnCollided(ref collisionResult);
            if (collisionResult.hitLocation == HitLocation.Sides && collisionResult.collider != null)
                SideCollided?.Invoke(collisionResult.collider, collisionResult.point, collisionResult.velocity);
        }

        /// <inheritdoc />
        void ICharacterMotor.SetFrozen(bool frozen)
        {
            // 다시 움직일 때는 떨어지는 상태에서 시작해 바닥에 닿으면 걷기로 바뀐다.
            SetVelocity(Vector3.zero);
            SetMovementMode(frozen ? MovementMode.None : MovementMode.Falling);
        }

        private Vector3 dashVelocity;
        private float dashRemaining;
        private float dashElapsed;
        private float dashLaunchSpeed;
        private float dashArcGravity;
        private bool isDashStopPending;
        private bool isDashLaunchPending;

        /// <inheritdoc />
        void ICharacterMotor.Dash(Vector3 horizontalVelocity, float duration, float launchSpeed, float arcGravity)
        {
            dashVelocity = Vector3.ProjectOnPlane(horizontalVelocity, Vector3.up);
            dashRemaining = Mathf.Max(0f, duration);
            dashElapsed = 0f;
            dashLaunchSpeed = Mathf.Max(0f, launchSpeed);
            dashArcGravity = Mathf.Max(0f, arcGravity);
            isDashStopPending = dashRemaining > 0f;
            isDashLaunchPending = isDashStopPending && dashLaunchSpeed > 0f;
        }

        /// <summary>
        /// 이동 모드가 속도를 계산한 뒤, 실제 이동 직전에 속도를 덮어써야 최대 속도·감속·중력 제한을 받지 않고 충돌은 그대로 처리된다.
        /// 벽타기(비행)·날아가기(None) 중에는 방향이 맞지 않으므로 적용하지 않는다.
        /// </summary>
        protected override void OnAfterSimulationUpdate(float deltaTime)
        {
            base.OnAfterSimulationUpdate(deltaTime);
            if (!isDashStopPending || deltaTime <= 0f)
                return;

            bool canDash = IsWalking() || IsFalling();
            Vector3 currentVelocity = characterMovement.velocity;
            if (dashRemaining <= 0f)
            {
                // 도약 속도가 남으면 감속하는 동안 정한 거리보다 더 미끄러지므로 끝난 다음 틱에 수평 속도를 없앤다.
                isDashStopPending = false;
                if (canDash)
                    characterMovement.velocity = new Vector3(0f, currentVelocity.y, 0f);
                return;
            }

            // 마지막 틱은 남은 시간만큼만 밀어야 정한 거리를 넘지 않는다.
            float activeTime = Mathf.Min(dashRemaining, deltaTime);
            float activeRatio = activeTime / deltaTime;
            float verticalVelocity = GetDashVerticalVelocity(currentVelocity.y, activeTime);
            dashElapsed += deltaTime;
            dashRemaining -= deltaTime;
            if (canDash)
                characterMovement.velocity = new Vector3(dashVelocity.x * activeRatio, verticalVelocity, dashVelocity.z * activeRatio);
        }

        /// <summary>
        /// 뛰어올랐다 떨어지는 포물선을 그리도록 도약 중 세로 속도를 정한다.
        /// 첫 틱에는 바닥에 붙여 두는 처리가 위로 뜨는 것을 막으므로 잠시 끄고, 이번 틱 구간의 평균 속도를 써서 틱 간격과 상관없이 같은 높이가 되게 한다.
        /// 도중에 높은 곳에 내려앉아 걷기 상태가 되면 바닥 처리를 그대로 따른다.
        /// </summary>
        private float GetDashVerticalVelocity(float currentVertical, float activeTime)
        {
            if (dashLaunchSpeed <= 0f)
                return currentVertical;

            if (isDashLaunchPending)
            {
                isDashLaunchPending = false;
                characterMovement.PauseGroundConstraint();
            }
            else if (!IsFalling())
            {
                return currentVertical;
            }

            float midTime = dashElapsed + activeTime * 0.5f;
            return dashLaunchSpeed - dashArcGravity * midTime;
        }
    }
}
