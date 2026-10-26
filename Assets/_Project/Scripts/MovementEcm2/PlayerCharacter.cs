using ECM2;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// ECM2 캐릭터를 게임 코드의 <see cref="ICharacterMotor"/>로 노출하는 공통 기반. (가드·마네킹 캐릭터가 상속)
    /// 이 폴더(GuardVsMannequin.Movement.ECM2 어셈블리)만 ECM2를 참조한다.
    /// ECM2가 없는 프로젝트에서는 GVM_ECM2 기호가 없어 이 어셈블리가 컴파일되지 않고, 게임 코드는 그대로 컴파일된다.
    /// </summary>
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

        /// <inheritdoc />
        Transform ICharacterMotor.ViewTransform => cameraTransform;

        /// <inheritdoc />
        void ICharacterMotor.SetupAsOwner(Camera playerCamera)
        {
            camera = playerCamera;
            // ECM2는 물리 단계(Fixed Timestep)에서만 움직이므로, 보간이 없으면 화면이 물리 주기로 끊겨 보인다.
            characterMovement.interpolation = RigidbodyInterpolation.Interpolate;
        }

        /// <inheritdoc />
        void ICharacterMotor.SetupAsRemote()
        {
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
        void ICharacterMotor.SetFrozen(bool frozen)
        {
            // 다시 움직일 때는 떨어지는 상태에서 시작해 바닥에 닿으면 걷기로 바뀐다.
            SetVelocity(Vector3.zero);
            SetMovementMode(frozen ? MovementMode.None : MovementMode.Falling);
        }
    }
}
