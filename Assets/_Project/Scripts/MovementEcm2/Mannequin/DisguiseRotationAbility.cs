using ECM2;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>변신 중 사물 방향을 직접 정할 수 있도록 상황별로 마네킹 몸 방향 규칙을 바꾼다.</summary>
    [RequireComponent(typeof(Character), typeof(MannequinTransformer))]
    public class DisguiseRotationAbility : MonoBehaviour
    {
        [Tooltip("Q/E를 누르고 있을 때의 회전 속도(도/초).")]
        [SerializeField] private float rotateSpeed = 120f;

        private Character character;
        private MannequinCharacter mannequinCharacter;
        private MannequinTransformer transformer;
        private IPlayerInput playerInput;

        /// <summary>
        /// 매 프레임 GetComponent를 부르지 않도록 미리 찾아 둔다.
        /// </summary>
        private void Awake()
        {
            character = GetComponent<Character>();
            mannequinCharacter = character as MannequinCharacter;
            transformer = GetComponent<MannequinTransformer>();
            playerInput = GetComponent<IPlayerInput>();
        }

        /// <summary>
        /// ECM2가 회전을 계산하기 직전에 모드를 바꿔야 같은 단계에 반영되므로 시뮬레이션 직전 이벤트를 듣는다.
        /// </summary>
        private void OnEnable()
        {
            character.BeforeSimulationUpdated += OnBeforeSimulationUpdated;
        }

        /// <summary>
        /// 꺼진 뒤 이벤트가 이 객체를 부르지 않도록 끊는다.
        /// </summary>
        private void OnDisable()
        {
            character.BeforeSimulationUpdated -= OnBeforeSimulationUpdated;
        }

        /// <summary>변신 중이나 1인칭에서는 자동 회전이 방향을 망치므로 끈다.</summary>
        private void OnBeforeSimulationUpdated(float deltaTime)
        {
            bool isFirstPerson = mannequinCharacter != null && mannequinCharacter.IsFirstPerson;
            Character.RotationMode mode = transformer.IsDisguised || isFirstPerson
                ? Character.RotationMode.None
                : Character.RotationMode.OrientRotationToMovement;

            if (character.rotationMode != mode)
                character.SetRotationMode(mode);
        }

        /// <summary>
        /// 변신 중에는 마우스가 카메라를 돌리므로 사물 방향은 Q/E로 따로 돌릴 수 있게 한다.
        /// </summary>
        private void Update()
        {
            if (!transformer.IsDisguised || playerInput == null)
                return;

            float rotateInput = playerInput.Rotate;
            if (rotateInput != 0f)
                character.AddYawInput(rotateInput * rotateSpeed * Time.deltaTime);
        }
    }
}
