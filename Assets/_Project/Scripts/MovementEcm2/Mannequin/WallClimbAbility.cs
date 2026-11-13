using ECM2;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>변신한 마네킹이 숨을 곳을 넓히도록 캐릭터를 돌리지 않고 Flying 모드로 벽과 천장을 따라 움직이게 한다.</summary>
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

        public bool IsClimbing => surface != Surface.None;

        /// <summary>이동 입력과 카메라 제한이 지금 붙은 면을 기준으로 계산되도록, 붙어 있으면 그 면의 법선을 준다.</summary>
        public Vector3 MovementPlaneNormal => IsClimbing ? surfaceNormal : -character.GetGravityDirection();

        /// <summary>
        /// 매 시뮬레이션마다 GetComponent를 부르지 않도록 미리 찾아 둔다.
        /// </summary>
        private void Awake()
        {
            character = GetComponent<Character>();
            transformer = GetComponent<MannequinTransformer>();
        }

        /// <summary>같은 단계에 반영되도록 ECM2 이동 계산 직전에 방향을 면에 맞춘다.</summary>
        private void OnEnable()
        {
            character.BeforeSimulationUpdated += OnBeforeSimulationUpdated;
            transformer.DisguiseChanged += OnDisguiseChanged;
        }

        /// <summary>
        /// 꺼진 뒤 이벤트가 이 객체를 부르지 않도록 끊는다.
        /// </summary>
        private void OnDisable()
        {
            character.BeforeSimulationUpdated -= OnBeforeSimulationUpdated;
            transformer.DisguiseChanged -= OnDisguiseChanged;
        }

        /// <summary>
        /// 사람 모습으로 벽에 붙어 있으면 안 되므로 변신이 풀리는 즉시 떨어뜨린다.
        /// </summary>
        private void OnDisguiseChanged(int propIndex)
        {
            if (propIndex == TransformableProp.None && IsClimbing)
                StopClimbing();
        }

        /// <summary>
        /// 면이 바뀌는 판단은 지금 이동 방향이 필요해서 매 시뮬레이션 전에 하고, 붙어 있으면 이동 방향을 면에 맞춘다.
        /// </summary>
        private void OnBeforeSimulationUpdated(float deltaTime)
        {
            if (!transformer.IsDisguised)
            {
                if (IsClimbing)
                    StopClimbing();
                return;
            }

            Vector3 moveDirection = character.GetMovementDirection();
            switch (surface)
            {
                case Surface.None:
                    if (character.IsWalking() && TryFindWallAhead(moveDirection, out Vector3 wallNormal))
                        StartClimbing(Surface.Wall, wallNormal);
                    break;
                case Surface.Wall:
                    UpdateOnWall(moveDirection);
                    break;
                case Surface.Ceiling:
                    UpdateOnCeiling(moveDirection);
                    break;
            }

            if (IsClimbing)
                ApplySurfaceMovement(moveDirection);
        }

        /// <summary>
        /// 벽 가장자리를 넘어가면 허공에 붙어 있게 되므로 매번 벽이 아직 있는지 확인하고, 이동 방향에 따라 천장·바닥·옆 벽으로 옮긴다.
        /// </summary>
        private void UpdateOnWall(Vector3 moveDirection)
        {
            if (!ProbeFromCenter(-surfaceNormal, character.radius, out RaycastHit wallHit) || !IsWall(wallHit.normal))
            {
                StopClimbing();
                return;
            }
            surfaceNormal = wallHit.normal;

            float verticalInput = Vector3.Dot(moveDirection, Vector3.up);
            if (verticalInput > 0.1f && ProbeFromCenter(Vector3.up, character.height * 0.5f, out RaycastHit ceilingHit) && IsCeiling(ceilingHit.normal))
            {
                surface = Surface.Ceiling;
                surfaceNormal = ceilingHit.normal;
            }
            else if (verticalInput < -0.1f && ProbeFromCenter(Vector3.down, character.height * 0.5f, out RaycastHit floorHit) && IsFloor(floorHit.normal))
            {
                StopClimbing();
            }
            else if (TryFindWallAhead(moveDirection, out Vector3 cornerNormal) && Vector3.Angle(cornerNormal, surfaceNormal) > 30f)
            {
                // 안쪽 모서리: 옆 벽으로 옮겨 탄다.
                surfaceNormal = cornerNormal;
            }
        }

        /// <summary>
        /// 천장 가장자리를 넘어가면 허공에 붙어 있게 되므로 매번 천장이 아직 있는지 확인하고, 앞에 벽이 있으면 벽으로 옮긴다.
        /// </summary>
        private void UpdateOnCeiling(Vector3 moveDirection)
        {
            if (!ProbeFromCenter(Vector3.up, character.height * 0.5f, out RaycastHit ceilingHit) || !IsCeiling(ceilingHit.normal))
            {
                StopClimbing();
                return;
            }
            surfaceNormal = ceilingHit.normal;

            if (TryFindWallAhead(moveDirection, out Vector3 wallNormal))
            {
                surface = Surface.Wall;
                surfaceNormal = wallNormal;
            }
        }

        /// <summary>면에서 떨어져 나가지 않도록 입력을 면 위로 투영한 뒤 면 쪽으로 살짝 밀어붙인다.</summary>
        private void ApplySurfaceMovement(Vector3 moveDirection)
        {
            Vector3 onSurface = Vector3.ProjectOnPlane(moveDirection, surfaceNormal);
            character.SetMovementDirection(onSurface - surfaceNormal * stickStrength);
        }

        /// <summary>붙어 있는 동안 떨어지지 않도록 중력 없는 Flying 모드로 바꾼다.</summary>
        private void StartClimbing(Surface newSurface, Vector3 normal)
        {
            surface = newSurface;
            surfaceNormal = normal;
            character.SetMovementMode(Character.MovementMode.Flying);
        }

        /// <summary>중력이 다시 걸리도록 떨어지기로 돌아간다.</summary>
        private void StopClimbing()
        {
            surface = Surface.None;
            surfaceNormal = Vector3.up;
            character.SetMovementMode(Character.MovementMode.Falling);
        }

        /// <summary>옆 벽에 스치는 것까지 걸리지 않도록 이동 방향 앞만 몸보다 조금 작은 구로 쓸어 본다.</summary>
        private bool TryFindWallAhead(Vector3 moveDirection, out Vector3 normal)
        {
            normal = Vector3.zero;
            Vector3 flatDirection = Vector3.ProjectOnPlane(moveDirection, Vector3.up);
            if (flatDirection.sqrMagnitude < 0.01f)
                return false;
            flatDirection.Normalize();

            float radius = character.radius;
            Vector3 center = GetCenter();
            if (!Physics.SphereCast(center, radius * 0.9f, flatDirection, out RaycastHit hit, radius * 0.1f + surfaceCheckDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;

            if (!IsClimbable(hit) || !IsWall(hit.normal))
                return false;

            normal = hit.normal;
            return true;
        }

        /// <summary>
        /// 아직 면에 닿아 있는지 확인하려고 몸 표면 바로 바깥까지 광선을 쏜다.
        /// </summary>
        /// <param name="extent">중심에서 캐릭터 표면까지의 거리.</param>
        private bool ProbeFromCenter(Vector3 direction, float extent, out RaycastHit hit)
        {
            bool isFound = Physics.Raycast(GetCenter(), direction, out hit, extent + contactTolerance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            return isFound && IsClimbable(hit);
        }

        /// <summary>발 위치에서 쏘면 바닥에 먼저 걸리므로 몸 중심에서 검사한다.</summary>
        private Vector3 GetCenter() => character.GetPosition() + Vector3.up * (character.height * 0.5f);

        /// <summary>다른 캐릭터나 움직이는 물체, 자기 자신에는 붙지 않는다.</summary>
        private bool IsClimbable(RaycastHit hit) =>
            (hit.collider.attachedRigidbody == null || PropPhysicsRules.IsRestingProp(hit.collider.attachedRigidbody)) && !hit.transform.IsChildOf(transform);

        /// <summary>경사진 면도 벽으로 다룰 수 있도록 법선이 수평에 가까운 범위를 벽으로 본다.</summary>
        private bool IsWall(Vector3 normal) => Mathf.Abs(normal.y) < wallNormalMaxY;

        /// <summary>법선이 충분히 아래를 향하면 천장으로 본다.</summary>
        private bool IsCeiling(Vector3 normal) => normal.y <= -wallNormalMaxY;

        /// <summary>법선이 충분히 위를 향하면 바닥으로 본다.</summary>
        private bool IsFloor(Vector3 normal) => normal.y >= wallNormalMaxY;
    }
}
