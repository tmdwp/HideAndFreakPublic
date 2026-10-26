using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using GuardVsMannequin.Core;
using GuardVsMannequin.PropPhysics;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>
    /// 변신한 마네킹이 총에 맞으면 진짜 사물처럼 날아가게 한다("사물 모드"). 진짜 사물과 같은 물리·시점·동기화를 쓰도록 서버가 직접 시뮬레이션한다.
    /// 1. 서버가 변신 외형과 같은 대리 물체(<see cref="KnockbackProxy"/>)를 놓고 충격을 준다. 이동 검증은 잠시 멈춘다.
    /// 2. 날아가는 동안 소유자는 이동·변신이 잠기고(<see cref="IActionLock"/>), 모든 피어가 대리 물체 자세로 외형을 보여준다.
    /// 3. 멈추면 서버가 그 자리로 아바타를 옮기고, 넘어진 모습은 움직이거나 돌기 전까지 유지한다.
    /// 사람 모습·구울은 날아가지 않는다.
    /// </summary>
    [RequireComponent(typeof(MannequinTransformer), typeof(PropDisguiseView))]
    public class DisguiseKnockback : NetworkBehaviour, IImpactReceiver, IActionLock
    {
        // 멈춘 뒤 넘어진 모습을 풀 이동 거리(m, 수평)와 회전 각도(도). 원격 화면의 위치 보간이 따라올 시간도 준다(초).
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

        /// <summary>변신한 사물과 같은 질량 규칙. (진짜 사물과 같은 세기로 날아가도록)</summary>
        public float ImpactMass => PropPhysicsRules.GetMass(disguiseView.DisguiseBounds);

        /// <summary>날아가는 중에는 변신·해제를 할 수 없다.</summary>
        public bool IsActionLocked => isKnocked.Value;

        /// <summary>
        /// 구성 요소를 캐시하고 상태 변경을 구독한다.
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
        /// 구독을 해제하고 남은 대리 물체를 없앤다.
        /// </summary>
        private void OnDestroy()
        {
            isKnocked.OnChange -= OnKnockedChanged;
            transformer.DisguiseChanged -= OnDisguiseChanged;
            DestroyProxy();
        }

        /// <summary>
        /// 총알·날아온 사물의 충격을 받는다. 날아가는 중이면 충격을 더하고, 변신 중이면 사물 모드를 시작한다. (서버)
        /// </summary>
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

            if (validator != null)
                validator.SetSuspended(true);
            isKnocked.Value = true;
        }

        /// <summary>
        /// 서버: 날아가는 대리 물체의 멈춤·추락·중단 조건을 확인하고 자세를 보낸다.
        /// </summary>
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
                // 맵 밖으로 떨어지면 맞은 자리로 되돌린다.
                body.position = knockStartPose.position;
                body.rotation = knockStartPose.rotation;
                EndKnockback(place: true);
                return;
            }

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

        /// <summary>
        /// 모든 피어: 날아가는 동안 외형을 서버 물리 자세에 놓고(소유자는 카메라가 따라가도록 아바타도 옮김),
        /// 멈춘 뒤에는 움직이거나 돌면 넘어진 모습을 푼다.
        /// </summary>
        private void LateUpdate()
        {
            if (isKnocked.Value)
            {
                if (!TryGetFlightPose(out Pose pose))
                    return;

                disguiseView.SetWorldPose(pose);
                if (IsOwner && motor != null)
                    motor.Teleport(GetFootPoint(pose), GetYaw(pose.rotation));
                return;
            }

            if (restRootPose.HasValue && Time.time - restStartTime >= RestBreakGrace && HasLeftRestPose(restRootPose.Value))
            {
                restRootPose = null;
                disguiseView.ResetPose();
            }
        }

        /// <summary>
        /// 날아가는 동안 보여줄 자세. 서버(호스트)는 대리 물체를 그대로, 클라이언트는 받은 자세를 보간한다.
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
        /// 사물 모드를 끝낸다. <paramref name="place"/>면 멈춘 자리로 아바타를 옮기고 넘어진 모습을 모든 피어에 알린다. (서버)
        /// </summary>
        private void EndKnockback(bool place)
        {
            Pose final = new Pose(proxy.Body.position, proxy.Body.rotation);
            DestroyProxy();

            if (validator != null)
                validator.SetSuspended(false);

            if (place)
            {
                Vector3 rootPosition = FindGround(final);
                Quaternion rootRotation = GetYaw(final.rotation);
                Quaternion inverse = Quaternion.Inverse(rootRotation);
                var local = new Pose(inverse * (final.position - rootPosition), inverse * final.rotation);

                if (validator != null)
                    validator.ServerTeleport(rootPosition, rootRotation);
                else
                    transform.SetPositionAndRotation(rootPosition, rootRotation);
                RestObserversRpc(rootPosition, rootRotation, local.position, local.rotation, transformer.IsDisguised);
            }
            isKnocked.Value = false;
        }

        /// <summary>
        /// 날아가는 자세를 받는다. (클라이언트, 비신뢰 전송)
        /// </summary>
        [ObserversRpc]
        private void PoseObserversRpc(uint tick, Vector3 position, Quaternion rotation, Channel channel = Channel.Unreliable)
        {
            if (IsServerStarted)
                return;
            double serverTime = TimeManager.TicksToTime(tick);
            clock.Observe(serverTime, Time.unscaledTimeAsDouble);
            poses.Add(serverTime, new Pose(position, rotation));
        }

        /// <summary>
        /// 멈춘 자리와 넘어진 모습을 받는다. (모든 피어) 변신이 풀린 채 끝났으면 기본 자세로 둔다.
        /// </summary>
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

        /// <summary>
        /// 날아가기 상태가 바뀌면 소유자의 이동을 멈추거나 다시 시작한다. (피어마다 한 번)
        /// </summary>
        private void OnKnockedChanged(bool prev, bool next, bool asServer)
        {
            if (asServer && IsClientInitialized)
                return;

            if (next)
            {
                poses.Clear();
                restRootPose = null;
            }
            if (IsOwner && motor != null)
                motor.SetFrozen(next);
        }

        /// <summary>
        /// 변신이 바뀌면 넘어진 모습 기준을 지운다. 서버는 날아가는 중 변신이 풀리면(사냥 시간 진입) 그 자리에서 멈춘다.
        /// </summary>
        private void OnDisguiseChanged(int propIndex)
        {
            restRootPose = null;
            if (IsServerStarted && proxy != null && propIndex == TransformableProp.None)
                EndKnockback(place: true);
        }

        /// <summary>
        /// 사물 모드를 시작할 수 있는지. (살아 있고, 변신해 외형이 보이며, 탐색·사냥 시간)
        /// </summary>
        private bool CanKnock()
        {
            return health.IsAlive && transformer.IsDisguised && disguiseView.IsShowing && IsPlayableState();
        }

        /// <summary>
        /// 날아갈 수 있는 단계인지. (탐색, 사냥 시간)
        /// </summary>
        private static bool IsPlayableState()
        {
            GameState state = GameServices.CurrentStateOrWaiting;
            return state == GameState.Playing || state == GameState.Hunting;
        }

        /// <summary>
        /// 아바타 자리가 멈춘 자리에서 벗어났는지. (수평 거리, 좌우 방향)
        /// </summary>
        private bool HasLeftRestPose(Pose rest)
        {
            Vector3 offset = transform.position - rest.position;
            offset.y = 0f;
            return offset.magnitude > RestBreakDistance || Quaternion.Angle(transform.rotation, rest.rotation) > RestBreakAngle;
        }

        /// <summary>
        /// 외형 자세의 대략적인 발 위치. (경계 가운데에서 경계 높이의 절반 아래 — 날아가는 동안 카메라 기준)
        /// </summary>
        private Vector3 GetFootPoint(Pose pose)
        {
            Bounds bounds = disguiseView.DisguiseBounds;
            Vector3 center = pose.position + pose.rotation * bounds.center;
            return center - Vector3.up * bounds.extents.y;
        }

        /// <summary>
        /// 멈춘 외형 가운데 아래의 바닥 위치. 바닥이 없으면 대략적인 발 위치.
        /// </summary>
        private Vector3 FindGround(Pose pose)
        {
            Vector3 center = pose.position + pose.rotation * disguiseView.DisguiseBounds.center;
            int count = Physics.RaycastNonAlloc(center, Vector3.down, groundHits, GroundProbeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            float nearest = float.MaxValue;
            Vector3 ground = GetFootPoint(pose);
            for (int i = 0; i < count; i++)
            {
                if (groundHits[i].transform.IsChildOf(transform) || groundHits[i].distance >= nearest)
                    continue;
                nearest = groundHits[i].distance;
                ground = groundHits[i].point;
            }
            return ground;
        }

        /// <summary>
        /// 회전에서 좌우 방향만 남긴다. (아바타는 세로로 서 있어야 함)
        /// </summary>
        private Quaternion GetYaw(Quaternion rotation)
        {
            Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.ProjectOnPlane(rotation * Vector3.up, Vector3.up);
            return forward.sqrMagnitude < 0.01f ? transform.rotation : Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        /// <summary>
        /// 대리 물체를 없앤다.
        /// </summary>
        private void DestroyProxy()
        {
            if (proxy != null)
                Destroy(proxy.gameObject);
            proxy = null;
        }
    }
}
