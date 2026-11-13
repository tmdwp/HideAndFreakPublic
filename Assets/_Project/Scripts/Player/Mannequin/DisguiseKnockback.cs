using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using GuardVsMannequin.Core;
using GuardVsMannequin.PropPhysics;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>변신한 마네킹이 총에 맞으면 진짜 사물처럼 날아가도록 서버가 직접 시뮬레이션한다.</summary>
    [RequireComponent(typeof(MannequinTransformer), typeof(PropDisguiseView))]
    public class DisguiseKnockback : NetworkBehaviour, IImpactReceiver, IActionLock
    {
        // 멈춘 뒤 넘어진 모습을 푸는 이동 거리·회전 각도와 보간 대기 시간.
        private const float RestBreakDistance = 0.15f;
        private const float RestBreakAngle = 2f;
        private const float RestBreakGrace = 0.5f;

        // 멈춘 자리 아래 바닥을 찾는 최대 거리(m).
        private const float GroundProbeDistance = 5f;

        private static readonly RaycastHit[] groundHits = new RaycastHit[8];

        private readonly SyncVar<bool> isKnocked = new();
        private readonly PoseSnapshotBuffer poses = new();
        private readonly ServerClockEstimate clock = new();

        private MannequinTransformer transformer;
        private PropDisguiseView disguiseView;
        private ICharacterMotor motor;
        private MovementValidator validator;
        private PlayerHealth health;

        // 서버: 날아가는 대리 물체와 진행 상태
        private KnockbackProxy proxy;
        private float knockStartTime;
        private float slowTime;
        private Pose knockStartPose;
        private float nextSendTime;

        // 멈춘 뒤 넘어진 모습을 유지하는 기준 자리와 시작 시각
        private Pose? restRootPose;
        private float restStartTime;

        /// <summary>진짜 사물과 같은 세기로 날아가도록 같은 질량 규칙을 쓴다.</summary>
        public float ImpactMass => PropPhysicsRules.GetMass(disguiseView.DisguiseBounds);

        /// <summary>날아가는 중에 변신·해제하면 외형과 대리 물체가 어긋나므로 잠근다.</summary>
        public bool IsActionLocked => isKnocked.Value;

        /// <summary>
        /// 매번 조회하지 않도록 구성 요소를 찾아 두고, 날아가기·변신 변화를 듣는다.
        /// </summary>
        private void Awake()
        {
            transformer = GetComponent<MannequinTransformer>();
            disguiseView = GetComponent<PropDisguiseView>();
            motor = GetComponent<ICharacterMotor>();
            validator = GetComponent<MovementValidator>();
            health = GetComponent<PlayerHealth>();
            isKnocked.OnChange += OnKnockedChanged;
            transformer.DisguiseChanged += OnDisguiseChanged;
        }

        /// <summary>
        /// 대리 물체는 별도 오브젝트라 아바타와 함께 지워지지 않으므로 직접 없앤다.
        /// </summary>
        private void OnDestroy()
        {
            isKnocked.OnChange -= OnKnockedChanged;
            transformer.DisguiseChanged -= OnDisguiseChanged;
            DestroyProxy();
        }

        /// <summary>연속으로 맞아도 자연스럽도록 이미 날아가는 중이면 충격만 더한다.</summary>
        public void ReceiveImpact(Vector3 point, Vector3 impulse)
        {
            if (!IsServerStarted)
                return;

            if (proxy != null)
            {
                proxy.Body.AddForceAtPosition(impulse, point, ForceMode.Impulse);
                slowTime = 0f;
                return;
            }

            if (!CanKnock())
                return;

            knockStartPose = disguiseView.WorldPose;
            proxy = KnockbackProxy.Create(knockStartPose, disguiseView.DisguiseBounds, health, this, GetComponentsInChildren<Collider>(true));
            proxy.Body.AddForceAtPosition(impulse, point, ForceMode.Impulse);
            knockStartTime = Time.time;
            slowTime = 0f;
            nextSendTime = 0f;

            // 물리로 날아가는 거리는 이동 속도 규칙을 따르지 않으므로 검증을 잠시 멈춘다.
            if (validator != null)
                validator.SetSuspended(true);
            isKnocked.Value = true;
        }

        /// <summary>물리와 같은 주기로 확인하도록 FixedUpdate에서 하고, 대역폭을 아끼려고 정해진 비율로만 보낸다.</summary>
        private void FixedUpdate()
        {
            if (!IsServerStarted || proxy == null)
                return;

            if (!health.IsAlive)
            {
                EndKnockback(place: false);
                return;
            }
            if (!transformer.IsDisguised || !IsPlayableState())
            {
                EndKnockback(place: true);
                return;
            }

            Rigidbody body = proxy.Body;
            if (body.position.y < knockStartPose.position.y - GameTuning.PropKillDepth)
            {
                // 맵 밖으로 떨어지면 다시 돌아올 수 없으므로 맞은 자리로 되돌린다.
                body.position = knockStartPose.position;
                body.rotation = knockStartPose.rotation;
                EndKnockback(place: true);
                return;
            }

            // 계속 아주 느리게 구르는 경우에도 끝나도록 최대 시간을 함께 둔다.
            slowTime = PropPhysicsRules.IsSlow(body) ? slowTime + Time.fixedDeltaTime : 0f;
            if (slowTime >= GameTuning.PropSettleTime || Time.time - knockStartTime >= GameTuning.DisguiseKnockbackMaxTime)
            {
                EndKnockback(place: true);
                return;
            }

            if (Time.time >= nextSendTime)
            {
                nextSendTime = Time.time + 1f / GameTuning.PropSyncRate;
                PoseObserversRpc(TimeManager.Tick, body.position, body.rotation, Channel.Unreliable);
            }
        }

        /// <summary>날아가는 동안 외형을 물리 자세에 놓고, 멈춘 뒤 움직이면 넘어진 모습을 푼다.</summary>
        private void LateUpdate()
        {
            if (isKnocked.Value)
            {
                if (!TryGetFlightPose(out Pose flightPose))
                    return;

                disguiseView.SetWorldPose(flightPose);
                if (IsOwner && motor != null)
                    motor.Teleport(GetFootPoint(flightPose), GetYaw(flightPose.rotation));
                return;
            }

            if (restRootPose.HasValue && Time.time - restStartTime >= RestBreakGrace && HasLeftRestPose(restRootPose.Value))
            {
                restRootPose = null;
                disguiseView.ResetPose();
            }
        }

        /// <summary>
        /// 호스트는 대리 물체를 직접 갖고 있으므로 그대로 쓰고, 클라이언트는 띄엄띄엄 받은 자세를 보간해 부드럽게 보여 준다.
        /// </summary>
        private bool TryGetFlightPose(out Pose pose)
        {
            if (IsServerStarted && proxy != null)
            {
                pose = new Pose(proxy.transform.position, proxy.transform.rotation);
                return true;
            }
            if (poses.HasData)
            {
                pose = poses.Sample(clock.GetRenderTime(Time.unscaledTimeAsDouble));
                return true;
            }
            pose = default;
            return false;
        }

        /// <summary>
        /// <summary>이동 검증을 다시 켜고 멈춘 자리 바닥으로 아바타를 옮긴 뒤 넘어진 모습을 알린다.</summary>
        /// 아바타는 세로로 서 있어야 해서, 기울어진 모습은 외형에만 남기고 아바타 회전은 좌우 방향만 쓴다. (서버)
        /// </summary>
        private void EndKnockback(bool place)
        {
            Pose finalPose = new Pose(proxy.Body.position, proxy.Body.rotation);
            DestroyProxy();

            if (validator != null)
                validator.SetSuspended(false);

            if (place)
            {
                Vector3 rootPosition = FindGround(finalPose);
                Quaternion rootRotation = GetYaw(finalPose.rotation);
                Quaternion inverseRoot = Quaternion.Inverse(rootRotation);
                var localPose = new Pose(inverseRoot * (finalPose.position - rootPosition), inverseRoot * finalPose.rotation);

                if (validator != null)
                    validator.ServerTeleport(rootPosition, rootRotation);
                else
                    transform.SetPositionAndRotation(rootPosition, rootRotation);
                RestObserversRpc(rootPosition, rootRotation, localPose.position, localPose.rotation, transformer.IsDisguised);
            }
            isKnocked.Value = false;
        }

        /// <summary>날아가는 자세는 하나쯤 빠져도 다음 값이 오므로 비신뢰로 받는다.</summary>
        [ObserversRpc]
        private void PoseObserversRpc(uint tick, Vector3 position, Quaternion rotation, Channel channel = Channel.Unreliable)
        {
            if (IsServerStarted)
                return;
            double serverTime = TimeManager.TicksToTime(tick);
            clock.Observe(serverTime, Time.unscaledTimeAsDouble);
            poses.Add(serverTime, new Pose(position, rotation));
        }

        /// <summary>모든 화면에서 같은 넘어진 모습이 보이도록 멈춘 자리와 기울기를 받는다.</summary>
        [ObserversRpc]
        private void RestObserversRpc(Vector3 rootPosition, Quaternion rootRotation, Vector3 localPosition, Quaternion localRotation, bool keepTilt)
        {
            poses.Clear();
            if (!keepTilt)
            {
                restRootPose = null;
                disguiseView.ResetPose();
                return;
            }

            restRootPose = new Pose(rootPosition, rootRotation);
            restStartTime = Time.time;
            disguiseView.SetLocalPose(new Pose(localPosition, localRotation));
        }

        /// <summary>입력·중력이 물리 자세와 어긋나지 않도록 날아가는 동안 소유자 이동을 멈춘다.</summary>
        private void OnKnockedChanged(bool previousValue, bool nextValue, bool asServer)
        {
            if (asServer && IsClientInitialized)
                return;

            if (nextValue)
            {
                poses.Clear();
                restRootPose = null;
            }
            if (IsOwner && motor != null)
                motor.SetFrozen(nextValue);
        }

        /// <summary>다른 외형의 넘어진 모습이 남지 않도록 기준을 지운다.</summary>
        private void OnDisguiseChanged(int propIndex)
        {
            restRootPose = null;
            if (IsServerStarted && proxy != null && propIndex == TransformableProp.None)
                EndKnockback(place: true);
        }

        /// <summary>
        /// 외형이 보이지 않는 상태(사람 모습·구울)에서 날아가면 아무것도 보이지 않으므로, 변신해 외형이 보일 때만 허용한다.
        /// </summary>
        private bool CanKnock()
        {
            return health.IsAlive && transformer.IsDisguised && disguiseView.IsShowing && IsPlayableState();
        }

        /// <summary>
        /// 준비·결과 단계에서 날아가면 판 밖에서 위치가 바뀌므로 탐색·사냥 시간에만 허용한다.
        /// </summary>
        private static bool IsPlayableState()
        {
            GameState state = GameServices.CurrentStateOrWaiting;
            return state == GameState.Playing || state == GameState.Hunting;
        }

        /// <summary>
        /// 위치 보간으로 생기는 작은 흔들림에는 넘어진 모습이 풀리지 않도록, 수평 거리와 좌우 방향이 기준을 넘었을 때만 참이다.
        /// </summary>
        private bool HasLeftRestPose(Pose restPose)
        {
            Vector3 offset = transform.position - restPose.position;
            offset.y = 0f;
            return offset.magnitude > RestBreakDistance || Quaternion.Angle(transform.rotation, restPose.rotation) > RestBreakAngle;
        }

        /// <summary>
        /// 날아가는 동안 카메라가 사물 중심 근처를 따라가도록, 경계 가운데에서 경계 높이의 절반 아래를 발 위치로 본다.
        /// </summary>
        private Vector3 GetFootPoint(Pose pose)
        {
            Bounds bounds = disguiseView.DisguiseBounds;
            Vector3 center = pose.position + pose.rotation * bounds.center;
            return center - Vector3.up * bounds.extents.y;
        }

        /// <summary>아바타가 공중에 뜨지 않도록 멈춘 외형 아래 바닥을 찾는다.</summary>
        private Vector3 FindGround(Pose pose)
        {
            Vector3 center = pose.position + pose.rotation * disguiseView.DisguiseBounds.center;
            int count = Physics.RaycastNonAlloc(center, Vector3.down, groundHits, GroundProbeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            float nearestDistance = float.MaxValue;
            Vector3 groundPoint = GetFootPoint(pose);
            for (int i = 0; i < count; i++)
            {
                if (groundHits[i].transform.IsChildOf(transform) || groundHits[i].distance >= nearestDistance)
                    continue;
                nearestDistance = groundHits[i].distance;
                groundPoint = groundHits[i].point;
            }
            return groundPoint;
        }

        /// <summary>아바타는 세로로 서 있어야 하므로 좌우 방향만 남긴다.</summary>
        private Quaternion GetYaw(Quaternion rotation)
        {
            Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.ProjectOnPlane(rotation * Vector3.up, Vector3.up);
            return forward.sqrMagnitude < 0.01f ? transform.rotation : Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        /// <summary>
        /// 여러 곳에서 끝낼 수 있어서 null 검사와 정리를 한곳에 모은다.
        /// </summary>
        private void DestroyProxy()
        {
            if (proxy != null)
                Destroy(proxy.gameObject);
            proxy = null;
        }
    }
}
