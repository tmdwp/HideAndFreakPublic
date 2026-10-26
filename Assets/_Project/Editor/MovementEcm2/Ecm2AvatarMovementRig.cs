using ECM2;
using GuardVsMannequin.Player;
using GuardVsMannequin.Player.Guard;
using GuardVsMannequin.Player.Mannequin;
using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// ECM2로 아바타 이동 컴포넌트를 붙인다. (<see cref="IAvatarMovementRig"/> 구현)
    /// 에디터를 켜면 <see cref="AvatarMovementRigs"/>에 등록되어 프리팹 생성 도구가 쓴다.
    /// GVM_ECM2 기호가 있을 때만 컴파일된다. (ECM2가 없는 public 브랜치에서는 빠짐)
    /// </summary>
    public sealed class Ecm2AvatarMovementRig : IAvatarMovementRig
    {
        // 캐릭터 충돌 캡슐 기본 크기(m)
        private const float CapsuleRadius = 0.4f;
        private const float CapsuleHeight = 1.8f;

        /// <summary>
        /// 에디터를 켤 때 등록한다.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void RegisterOnLoad()
        {
            AvatarMovementRigs.Register(new Ecm2AvatarMovementRig());
        }

        /// <inheritdoc />
        public Component AddCharacter(GameObject root, AvatarKind kind)
        {
            CharacterMovement movement = root.AddComponent<CharacterMovement>();
            movement.SetDimensions(CapsuleRadius, CapsuleHeight);

            Character character = kind == AvatarKind.Guard
                ? root.AddComponent<GuardCharacter>()
                : root.AddComponent<MannequinCharacter>();
            ConfigureCharacter(character);
            return character;
        }

        /// <inheritdoc />
        public Behaviour AddMovementDriver(GameObject root)
        {
            return root.AddComponent<PlayerMovementDriver>();
        }

        /// <inheritdoc />
        public void AddAimProvider(GameObject root)
        {
            root.AddComponent<CameraAimProvider>();
        }

        /// <inheritdoc />
        public void AddMannequinAbilities(GameObject root)
        {
            root.AddComponent<WallClimbAbility>();
            root.AddComponent<DisguiseRotationAbility>();
        }

        /// <summary>
        /// 점프(Space)는 켜고, 이 게임에서 쓰지 않는 앉기를 끈다.
        /// </summary>
        private static void ConfigureCharacter(Character character)
        {
            var serialized = new SerializedObject(character);
            serialized.FindProperty("_canEverJump").boolValue = true;
            serialized.FindProperty("_canEverCrouch").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
