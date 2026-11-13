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
    }
}
