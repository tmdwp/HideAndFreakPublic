using ECM2;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>
    /// 마네킹의 몸 방향 규칙을 관리한다.
    /// - 평소: 이동 방향을 바라본다.
    /// - 변신 중: 이동해도 방향이 바뀌지 않고, Q/E로만 좌우 회전한다. (사물 배치 방향을 직접 정하기 위함)
    /// - 1인칭(사냥 시간): 마우스 좌우 시점으로 몸을 회전한다.
    /// 회전 모드는 이 컴포넌트만 변경한다. (벽타기 등 다른 기능은 회전 모드를 건드리지 않음)
    /// ECM2 시뮬레이션이 도는 소유자에서만 동작하며, 회전은 NetworkTransform으로 동기화된다.
    /// </summary>
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
        /// 같은 오브젝트의 구성 요소를 캐시한다.
        /// </summary>
        private void Awake()
        {
            character = GetComponent<Character>();
            mannequinCharacter = character as MannequinCharacter;
            transformer = GetComponent<MannequinTransformer>();
            playerInput = GetComponent<IPlayerInput>();
        }

        /// <summary>
        /// 캐릭터 시뮬레이션 직전 이벤트를 구독한다.
        /// </summary>
        private void OnEnable()
        {
            character.BeforeSimulationUpdated += OnBeforeSimulationUpdated;
        }

        /// <summary>
        /// 이벤트 구독을 해제한다.
        /// </summary>
        private void OnDisable()
        {
            character.BeforeSimulationUpdated -= OnBeforeSimulationUpdated;
        }

        /// <summary>
        /// 변신 여부에 맞는 회전 모드를 적용한다.
        /// </summary>
        private void OnBeforeSimulationUpdated(float deltaTime)
        {
            // 1인칭(사냥 시간)에서는 시점 입력이 몸을 직접 회전시키므로 자동 회전을 끈다.
            bool isFirstPerson = mannequinCharacter != null && mannequinCharacter.IsFirstPerson;
            Character.RotationMode mode = transformer.IsDisguised || isFirstPerson
                ? Character.RotationMode.None
                : Character.RotationMode.OrientRotationToMovement;

            if (character.rotationMode != mode)
                character.SetRotationMode(mode);
        }

        /// <summary>
        /// 변신 중이면 Q/E 입력만큼 좌우로 회전시킨다.
        /// </summary>
        private void Update()
        {
            if (!transformer.IsDisguised || playerInput == null)
                return;

            float rotate = playerInput.Rotate;
            if (rotate != 0f)
                character.AddYawInput(rotate * rotateSpeed * Time.deltaTime);
        }
    }
}
