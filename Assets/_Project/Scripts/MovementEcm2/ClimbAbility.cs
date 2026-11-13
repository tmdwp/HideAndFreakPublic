using ECM2;
using GuardVsMannequin.Core;
using GuardVsMannequin.Map;
using GuardVsMannequin.Player.Mannequin;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>가드와 변신하지 않은 마네킹이 사다리·밧줄을 타고 오르내리게 한다.</summary>
    [RequireComponent(typeof(Character))]
    public class ClimbAbility : MonoBehaviour
    {
        private enum State
        {
            None,
            Climbing,
            Exiting
        }

        [Tooltip("오르내리는 속도 배율. (이동 속도 기준)")]
        [SerializeField] private float climbSpeedScale = 0.7f;

        [Tooltip("오르는 물체를 찾는 반경(m). 몸 중심 기준.")]
        [SerializeField] private float detectRadius = 0.8f;

        [Tooltip("오르는 선에서 몸 표면까지 띄울 거리(m). 사다리 / 밧줄.")]
        [SerializeField] private Vector2 standOff = new Vector2(0.25f, 0.12f);

        [Tooltip("선 옆 제자리로 끌어당기는 세기(1/초).")]
        [SerializeField] private float holdGain = 8f;

        [Tooltip("놓은 뒤 다시 붙을 수 있을 때까지의 시간(초).")]
        [SerializeField] private float reattachDelay = 0.4f;

        [Tooltip("위 끝에서 이만큼 더 올라간 뒤 내려선다(m).")]
        [SerializeField] private float exitLift = 0.35f;

        [Tooltip("점프로 놓을 때 뒤로·위로 튕기는 속도(m/s).")]
        [SerializeField] private Vector2 jumpOffVelocity = new Vector2(2.5f, 2f);

        // 검사마다 배열을 새로 만들지 않도록 재사용한다.
        private static readonly Collider[] nearby = new Collider[16];

        private Character character;
        private IPlayerInput input;
        private MannequinTransformer transformer;
        private bool hasTransformer;
        private State state = State.None;
        private Climbable current;
        private Vector3 holdSide;
        private Vector3 exitStart;
        private float exitTime;
        private float lastReleaseTime = -10f;
        private bool wasJumpHeld;

        /// <summary>다른 기능이 오르는 중에 끼어들지 않도록 내려서는 중까지 포함해 알려 준다.</summary>
        public bool IsClimbing => state != State.None;

        /// <summary>매 시뮬레이션마다 찾지 않도록 미리 찾아 둔다.</summary>
        private void Awake()
        {
            character = GetComponent<Character>();
            input = GetComponent<IPlayerInput>();
            hasTransformer = TryGetComponent(out transformer);
        }

        /// <summary>
        /// ECM2가 이동을 계산하기 직전에 이동 모드·방향을 바꿔야 같은 단계에 반영되므로 시뮬레이션 직전 이벤트를 듣는다.
        /// </summary>
        private void OnEnable()
        {
            character.BeforeSimulationUpdated += OnBeforeSimulationUpdated;
        }

        /// <summary>
        /// 꺼진 채로 Flying 모드가 남으면 공중에 떠 있게 되므로, 붙어 있었으면 놓고 구독을 끊는다.
        /// </summary>
        private void OnDisable()
        {
            character.BeforeSimulationUpdated -= OnBeforeSimulationUpdated;
            if (IsClimbing)
                Release(Vector3.zero);
        }

        /// <summary>점프는 누르는 순간에만 놓기로 처리하고, 카운트다운·변신·넉백 중에는 바로 놓는다.</summary>
        private void OnBeforeSimulationUpdated(float deltaTime)
        {
            if (input == null)
                return;

            bool isJumpHeld = input.IsJumpHeld;
            bool isJumpPressed = isJumpHeld && !wasJumpHeld;
            wasJumpHeld = isJumpHeld;

            bool isBlocked = !MovementRules.CanMove(GameServices.CurrentStateOrWaiting)
                             || (hasTransformer && transformer.IsDisguised)
                             || character.GetMovementMode() == Character.MovementMode.None;
            if (isBlocked)
            {
                if (IsClimbing)
                    Release(Vector3.zero);
                return;
            }

            Vector2 moveInput = input.Move;
            switch (state)
            {
                case State.None:
                    if (moveInput.y > 0.5f && Time.time - lastReleaseTime > reattachDelay && TryFindClimbable(out Climbable found))
                        Attach(found);
                    break;
                case State.Climbing:
                    UpdateClimbing(moveInput.y, isJumpPressed);
                    break;
                case State.Exiting:
                    UpdateExiting(deltaTime);
                    break;
            }
        }

        #region 붙기·놓기

        /// <summary>
        /// 여러 개가 가까이 있으면 엉뚱한 쪽에 붙지 않도록 조건을 만족하는 것 중 가장 가까운 것을 고른다.
        /// </summary>
        private bool TryFindClimbable(out Climbable found)
        {
            found = null;
            Vector3 center = GetCenter();
            int count = Physics.OverlapSphereNonAlloc(center, detectRadius + character.radius, nearby, ~0, QueryTriggerInteraction.Collide);
            float bestSqrDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Climbable candidate = nearby[i].GetComponentInParent<Climbable>();
                if (candidate == null || !CanAttach(candidate))
                    continue;
                float sqrDistance = (candidate.PointAt(candidate.AlongOf(character.GetPosition())) - character.GetPosition()).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    found = candidate;
                }
            }
            return found != null;
        }

        /// <summary>지나가다 의도치 않게 붙거나 사다리를 뚫지 않도록 바라보는 방향·앞면·높이를 확인한다.</summary>
        private bool CanAttach(Climbable candidate)
        {
            Vector3 feetPosition = character.GetPosition();
            float alongDistance = candidate.AlongOf(feetPosition);
            if (alongDistance < -0.6f || alongDistance > candidate.Height - 0.3f)
                return false;

            Vector3 toLine = Flatten(candidate.PointAt(alongDistance) - feetPosition);
            if (toLine.sqrMagnitude > 1e-4f && Vector3.Dot(LookDirection(), toLine.normalized) < 0.2f)
                return false;
            return !candidate.IsLadder || Vector3.Dot(feetPosition - candidate.Bottom, candidate.Front) > -0.1f;
        }

        /// <summary>떨어지지 않도록 Flying 모드로 바꾸고, 한 바퀴 돌아 붙지 않게 매달릴 쪽을 정한다.</summary>
        private void Attach(Climbable target)
        {
            current = target;
            state = State.Climbing;
            if (target.HoldsOnFront)
            {
                holdSide = target.Front;
            }
            else
            {
                Vector3 sideOffset = Flatten(character.GetPosition() - target.PointAt(target.AlongOf(character.GetPosition())));
                holdSide = sideOffset.sqrMagnitude > 1e-4f ? sideOffset.normalized : -LookDirection();
            }
            character.SetMovementMode(Character.MovementMode.Flying);
            character.SetVelocity(Vector3.zero);
        }

        /// <summary>중력이 다시 걸리도록 떨어지기로 돌아가고, 바로 다시 붙지 않게 놓은 시각을 기억한다.</summary>
        private void Release(Vector3 velocity)
        {
            state = State.None;
            current = null;
            lastReleaseTime = Time.time;
            character.SetMovementDirection(Vector3.zero);
            character.SetMovementMode(Character.MovementMode.Falling);
            character.SetVelocity(velocity);
        }

        #endregion

        #region 오르내리기·내려서기

        /// <summary>허공을 오르는 것처럼 보이지 않도록 선을 따라 움직이며 선 옆 제자리로 끌어당긴다.</summary>
        private void UpdateClimbing(float verticalInput, bool isJumpPressed)
        {
            if (current == null)
            {
                Release(Vector3.zero);
                return;
            }
            if (isJumpPressed)
            {
                Release(holdSide * jumpOffVelocity.x + Vector3.up * jumpOffVelocity.y);
                return;
            }

            Vector3 feetPosition = character.GetPosition();
            float alongDistance = current.AlongOf(feetPosition);
            if (verticalInput < -0.1f && alongDistance <= 0.05f)
            {
                Release(Vector3.zero);
                return;
            }
            if (verticalInput > 0.1f && alongDistance >= current.Height - 0.05f)
            {
                state = State.Exiting;
                exitStart = feetPosition;
                exitTime = 0f;
                return;
            }

            float holdOffset = character.radius + (current.IsLadder ? standOff.x : standOff.y);
            Vector3 holdPoint = current.PointAt(alongDistance) + holdSide * holdOffset;
            Vector3 lateral = Vector3.ProjectOnPlane(holdPoint - feetPosition, current.Axis);
            float flySpeed = Mathf.Max(0.1f, character.maxFlySpeed);
            Vector3 direction = current.Axis * (Mathf.Clamp(verticalInput, -1f, 1f) * climbSpeedScale) + lateral * (holdGain / flySpeed);
            character.SetMovementDirection(Vector3.ClampMagnitude(direction, 1f));
        }

        /// <summary>가장자리에 걸리지 않도록 조금 더 올라간 뒤 내려서고, 끼면 영원히 매달리지 않게 놓는다.</summary>
        private void UpdateExiting(float deltaTime)
        {
            exitTime += deltaTime;
            if (current == null || exitTime > 2.5f)
            {
                Release(Vector3.zero);
                return;
            }

            Vector3 feetPosition = character.GetPosition();
            float heightAboveTop = feetPosition.y - current.Top.y;
            Vector3 exitDirection = current.TopExitDirection;
            float travelled = Vector3.Dot(Flatten(feetPosition - exitStart), exitDirection);
            // 몸이 위 끝을 넘고 발밑에 디딜 곳이 생기면 놓는다.
            bool isOverSupport = travelled >= character.radius * 2f && HasGroundBelow();
            if (isOverSupport || travelled >= MaxExitTravel)
            {
                Release(Vector3.zero);
                return;
            }
            Vector3 direction = heightAboveTop < exitLift ? Vector3.up : exitDirection;
            character.SetMovementDirection(direction * climbSpeedScale);
        }

        #endregion

        /// <summary>내려서다 디딜 곳을 못 찾아도 놓는 수평 거리(m).</summary>
        private const float MaxExitTravel = 3f;

        /// <summary>
        /// 디딜 곳이 없는 데서 놓으면 떨어지므로 발밑 가까이에 면이 있는지 확인한다.
        /// </summary>
        private bool HasGroundBelow()
        {
            float reach = character.height * 0.5f + exitLift + 0.2f;
            return Physics.Raycast(GetCenter(), Vector3.down, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        /// <summary>발 위치로 찾으면 바닥에 닿은 물체만 걸리므로 몸 중심에서 찾는다.</summary>
        private Vector3 GetCenter() => character.GetPosition() + Vector3.up * (character.height * 0.5f);

        /// <summary>위아래 시선이 섞이지 않도록 수평 방향만 쓴다.</summary>
        private Vector3 LookDirection()
        {
            Transform viewTransform = character.cameraTransform != null ? character.cameraTransform : transform;
            Vector3 flatForward = Flatten(viewTransform.forward);
            return flatForward.sqrMagnitude > 1e-4f ? flatForward.normalized : Flatten(transform.forward).normalized;
        }

        /// <summary>높이 차이가 방향·거리 판단에 섞이지 않도록 수평 성분만 남긴다.</summary>
        private static Vector3 Flatten(Vector3 vector) => new Vector3(vector.x, 0f, vector.z);
    }
}
