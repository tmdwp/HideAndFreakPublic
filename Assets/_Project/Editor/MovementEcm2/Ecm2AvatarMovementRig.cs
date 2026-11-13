using ECM2;
using GuardVsMannequin.Player;
using GuardVsMannequin.Player.Guard;
using GuardVsMannequin.Player.Mannequin;
using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>프리팹 생성 도구가 ECM2를 직접 참조하지 않도록 ECM2 이동 컴포넌트를 붙이는 구현을 등록한다.</summary>
    public sealed class Ecm2AvatarMovementRig : IAvatarMovementRig
    {
        // 캐릭터 충돌 캡슐 기본 크기(m)
        private const float CapsuleRadius = 0.4f;
        private const float CapsuleHeight = 1.8f;

        /// <summary>
        /// 프리팹 생성 도구가 이 어셈블리를 참조하지 않으므로, 에디터를 켤 때 스스로 등록해야 도구가 찾을 수 있다.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void RegisterOnLoad()
        {
            AvatarMovementRigs.Register(new Ecm2AvatarMovementRig());
        }

        /// <summary>캡슐 크기는 맵의 문·통로 기준과 맞아야 해서 고정값으로 맞춘다.</summary>
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

        /// <summary>
        /// 사다리·밧줄 오르기는 이동 입력을 함께 써야 해서 이동 입력 컴포넌트와 같이 붙인다.
        /// </summary>
        public Behaviour AddMovementDriver(GameObject root)
        {
            root.AddComponent<ClimbAbility>();
            return root.AddComponent<PlayerMovementDriver>();
        }

        /// <summary>
        /// 조준은 ECM2 카메라 정면을 쓰므로 이 구현이 붙인다.
        /// </summary>
        public void AddAimProvider(GameObject root)
        {
            root.AddComponent<CameraAimProvider>();
        }

        /// <summary>
        /// 벽타기와 변신 중 회전은 ECM2 이동 모드에 의존하므로 이 구현이 붙인다.
        /// </summary>
        public void AddMannequinAbilities(GameObject root)
        {
            root.AddComponent<WallClimbAbility>();
            root.AddComponent<DisguiseRotationAbility>();
        }

        /// <summary>앉기는 이 게임에 없어 켜 두면 캡슐 크기가 바뀌므로 끈다.</summary>
        private static void ConfigureCharacter(Character character)
        {
            var serializedCharacter = new SerializedObject(character);
            serializedCharacter.FindProperty("_canEverJump").boolValue = true;
            serializedCharacter.FindProperty("_canEverCrouch").boolValue = false;
            serializedCharacter.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
