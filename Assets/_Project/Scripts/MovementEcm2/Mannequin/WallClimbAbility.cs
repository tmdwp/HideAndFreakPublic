using ECM2;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>
    /// 변신 중인 마네킹이 벽과 천장을 타고 이동하게 한다.
    /// 캐릭터를 회전시키지 않고 그대로 둔 채, ECM2 Flying 모드(중력 없음)로 면을 따라 움직인다.
    /// - 바닥 → 벽: 걷다가 이동 방향 앞에 벽이 있으면 붙는다.
    /// - 벽 → 천장: 벽에서 위로 이동하다 머리 위에 천장이 닿으면 천장으로 옮긴다.
    /// - 천장 → 벽: 천장에서 이동 방향 앞에 벽이 있으면 벽으로 옮긴다.
    /// - 벽 → 바닥: 벽에서 아래로 이동하다 발밑에 바닥이 닿으면 걷기로 돌아간다.
    /// - 붙어 있던 면이 사라지거나(가장자리) 변신을 해제하면 떨어진다.
    /// ECM2 시뮬레이션이 도는 소유자에서만 동작하며, 위치는 NetworkTransform으로 동기화된다.
    /// </summary>
    [RequireComponent(typeof(Character), typeof(MannequinTransformer))]
    public class WallClimbAbility : MonoBehaviour, IMovementPlaneProvider
    {
        /// <summary>붙어 있는 면의 종류.</summary>
        private enum Surface
        {
            None,
            Wall,
            Ceiling
        }

        [Tooltip("이동 방향으로 벽을 찾는 추가 거리.")]
        [SerializeField] private float surfaceCheckDistance = 0.3f;

        [Tooltip("붙은 면과 이 거리 이상 떨어지면 붙어 있지 않은 것으로 본다.")]
        [SerializeField] private float contactTolerance = 0.35f;

        [Tooltip("면 쪽으로 살짝 밀어붙이는 입력 크기. 면에서 조금씩 떨어지는 것을 막는다.")]
        [SerializeField] private float stickStrength = 0.3f;

        [Tooltip("법선의 Y 성분 절댓값이 이 값보다 작으면 벽, 이보다 크면 바닥/천장으로 본다.")]
        [SerializeField] private float wallNormalMaxY = 0.7f;

        private Character character;
        private MannequinTransformer transformer;
        private Surface surface = Surface.None;
        private Vector3 surfaceNormal = Vector3.up;

        /// <summary>벽이나 천장에 붙어 있는지 여부.</summary>
        public bool IsClimbing => surface != Surface.None;

        /// <summary>붙어 있으면 그 면의 법선, 아니면 월드 위쪽.</summary>
        public Vector3 MovementPlaneNormal => IsClimbing ? surfaceNormal : -character.GetGravityDirection();

        /// <summary>
        /// 같은 오브젝트의 구성 요소를 캐시한다.
        /// </summary>
        private void Awake()
        {
            character = GetComponent<Character>();
            transformer = GetComponent<MannequinTransformer>();
        }

        /// <summary>
        /// 캐릭터 시뮬레이션 직전 이벤트와 변신 이벤트를 구독한다.
        /// </summary>
        private void OnEnable()
        {
            character.BeforeSimulationUpdated += OnBeforeSimulationUpdated;
            transformer.DisguiseChanged += OnDisguiseChanged;
        }

        /// <summary>
        /// 이벤트 구독을 해제한다.
        /// </summary>
        private void OnDisable()
        {
            character.BeforeSimulationUpdated -= OnBeforeSimulationUpdated;
            transformer.DisguiseChanged -= OnDisguiseChanged;
        }

        /// <summary>
        /// 변신이 해제되면 즉시 떨어진다.
        /// </summary>
        private void OnDisguiseChanged(int propId)
        {
            if (propId == TransformableProp.None && IsClimbing)
                StopClimbing();
        }

        /// <summary>
        /// 매 시뮬레이션 전에 면 전환을 판단하고, 붙어 있으면 이동 방향을 면에 맞춘다.
        /// </summary>
        private void OnBeforeSimulationUpdated(float deltaTime)
        {
            if (!transformer.IsDisguised)
            {
                if (IsClimbing)
                    StopClimbing();
                return;
            }

            Vector3 move = character.GetMovementDirection();
            switch (surface)
            {
                case Surface.None:
                    if (character.IsWalking() && TryFindWallAhead(move, out Vector3 wallNormal))
                        StartClimbing(Surface.Wall, wallNormal);
                    break;
                case Surface.Wall:
                    UpdateOnWall(move);
                    break;
                case Surface.Ceiling:
                    UpdateOnCeiling(move);
                    break;
            }

            if (IsClimbing)
                ApplySurfaceMovement(move);
        }

        /// <summary>
        /// 벽에 붙은 상태에서 면 유지, 천장/바닥/다른 벽으로의 전환을 처리한다.
        /// </summary>
        private void UpdateOnWall(Vector3 move)
        {
            if (!ProbeFromCenter(-surfaceNormal, character.radius, out RaycastHit wallHit) || !IsWall(wallHit.normal))
            {
                StopClimbing();
                return;
            }
            surfaceNormal = wallHit.normal;

            float vertical = Vector3.Dot(move, Vector3.up);
            if (vertical > 0.1f && ProbeFromCenter(Vector3.up, character.height * 0.5f, out RaycastHit ceilingHit) && IsCeiling(ceilingHit.normal))
            {
                surface = Surface.Ceiling;
                surfaceNormal = ceilingHit.normal;
            }
            else if (vertical < -0.1f && ProbeFromCenter(Vector3.down, character.height * 0.5f, out RaycastHit floorHit) && IsFloor(floorHit.normal))
            {
                StopClimbing();
            }
            else if (TryFindWallAhead(move, out Vector3 cornerNormal) && Vector3.Angle(cornerNormal, surfaceNormal) > 30f)
            {
                // 안쪽 모서리: 옆 벽으로 옮겨 탄다.
                surfaceNormal = cornerNormal;
            }
        }

        /// <summary>
        /// 천장에 붙은 상태에서 면 유지와 벽으로의 전환을 처리한다.
        /// </summary>
        private void UpdateOnCeiling(Vector3 move)
        {
            if (!ProbeFromCenter(Vector3.up, character.height * 0.5f, out RaycastHit ceilingHit) || !IsCeiling(ceilingHit.normal))
            {
                StopClimbing();
                return;
            }
            surfaceNormal = ceilingHit.normal;

            if (TryFindWallAhead(move, out Vector3 wallNormal))
            {
                surface = Surface.Wall;
                surfaceNormal = wallNormal;
            }
        }

        /// <summary>
        /// 이동 입력을 면 위로 투영하고, 면 쪽으로 살짝 밀어붙인다.
        /// 입력 원본을 투영한 뒤 더하므로 여러 번 호출되어도 값이 누적되지 않는다.
        /// </summary>
        private void ApplySurfaceMovement(Vector3 move)
        {
            Vector3 onSurface = Vector3.ProjectOnPlane(move, surfaceNormal);
            character.SetMovementDirection(onSurface - surfaceNormal * stickStrength);
        }

        /// <summary>
        /// 면에 붙는다. 변신 중에는 DisguiseRotationAbility가 회전을 막으므로 캐릭터 방향은 그대로 유지된다.
        /// </summary>
        private void StartClimbing(Surface newSurface, Vector3 normal)
        {
            surface = newSurface;
            surfaceNormal = normal;
            character.SetMovementMode(Character.MovementMode.Flying);
        }

        /// <summary>
        /// 면에서 떨어진다. 중력이 다시 적용되고 바닥에 닿으면 걷기로 돌아간다.
        /// </summary>
        private void StopClimbing()
        {
            surface = Surface.None;
            surfaceNormal = Vector3.up;
            character.SetMovementMode(Character.MovementMode.Falling);
        }

        /// <summary>
        /// 수평 이동 방향 앞에 벽이 있는지 찾는다.
        /// </summary>
        private bool TryFindWallAhead(Vector3 move, out Vector3 normal)
        {
            normal = Vector3.zero;
            Vector3 direction = Vector3.ProjectOnPlane(move, Vector3.up);
            if (direction.sqrMagnitude < 0.01f)
                return false;
            direction.Normalize();

            float radius = character.radius;
            Vector3 center = GetCenter();
            if (!Physics.SphereCast(center, radius * 0.9f, direction, out RaycastHit hit, radius * 0.1f + surfaceCheckDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;

            if (!IsClimbable(hit) || !IsWall(hit.normal))
                return false;

            normal = hit.normal;
            return true;
        }

        /// <summary>
        /// 캐릭터 중심에서 한 방향으로 면이 가까이 있는지 검사한다.
        /// </summary>
        /// <param name="direction">검사 방향.</param>
        /// <param name="extent">중심에서 캐릭터 표면까지의 거리. (옆: 반지름, 위/아래: 높이의 절반)</param>
        /// <param name="hit">찾은 면.</param>
        private bool ProbeFromCenter(Vector3 direction, float extent, out RaycastHit hit)
        {
            bool found = Physics.Raycast(GetCenter(), direction, out hit, extent + contactTolerance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            return found && IsClimbable(hit);
        }

        /// <summary>캐릭터 캡슐의 중심 위치.</summary>
        private Vector3 GetCenter() => character.GetPosition() + Vector3.up * (character.height * 0.5f);

        /// <summary>다른 캐릭터나 움직이는 물체, 자기 자신에는 붙지 않는다.</summary>
        private bool IsClimbable(RaycastHit hit) =>
            (hit.collider.attachedRigidbody == null || PropPhysicsRules.IsRestingProp(hit.collider.attachedRigidbody)) && !hit.transform.IsChildOf(transform);

        private bool IsWall(Vector3 normal) => Mathf.Abs(normal.y) < wallNormalMaxY;
        private bool IsCeiling(Vector3 normal) => normal.y <= -wallNormalMaxY;
        private bool IsFloor(Vector3 normal) => normal.y >= wallNormalMaxY;
    }
}
