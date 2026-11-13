using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using GuardVsMannequin.Core;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.PropPhysics
{
    /// <summary>변신 사물 물리를 서버에서 진행하고 움직이는 사물만 클라이언트에 보내는 네트워크 창구.</summary>
    public class PropPhysicsSync : NetworkBehaviour, IPropPhysics, IPropPushRequester
    {
        // 위치 요청을 받는 최소 간격(초)
        private const float SnapshotRequestInterval = 5f;
        // 여러 명이 동시에 기대거나 조작된 요청이 몰려도 한 사물이 계속 가속되지 않도록 두는 사물별 밀기 간격(초)
        private const float PushInterval = 0.25f;

        // 서버: 사물별 다음 밀기 허용 시각
        private readonly Dictionary<int, float> nextPushTimes = new();

        private readonly PropPhysicsSimulation simulation = new();
        private readonly PropPoseInterpolator interpolator = new();
        private readonly List<PropBody> moving = new();
        private readonly List<PropBody> settled = new();
        // 재접속한 사람에게 지금 위치를 보내려고 기억하는 이번 판에 움직인 사물.
        private readonly HashSet<PropBody> displaced = new();
        // 서버: 연결별 다음 위치 요청 허용 시각 (요청 남발 방지)
        private readonly Dictionary<int, float> nextSnapshotTimes = new();
        // 클라이언트: 맵 사물 준비가 끝나면 적용할 위치
        private PropPoseData[] pendingSnapshot;
        private uint pendingSnapshotTick;
        private float nextSendTime;

        /// <summary>
        /// 물리는 서버만 시뮬레이션하므로 서버에서만 사물 충격을 받을 수 있게 등록한다.
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();
            GameServices.RegisterPropPhysics(this);
        }

        /// <summary>
        /// 다음 판에 이전 판의 움직임·요청 기록이 남지 않도록 해제하면서 움직이던 사물을 멈춘다.
        /// </summary>
        public override void OnStopServer()
        {
            base.OnStopServer();
            GameServices.UnregisterPropPhysics(this);
            simulation.Clear();
            displaced.Clear();
            nextSnapshotTimes.Clear();
            nextPushTimes.Clear();
        }

        /// <summary>
        /// 재접속한 게스트는 움직인 사물 위치를 모르므로 게임 씬에 들어오면 지금 위치를 요청하고, 아바타가 사물을 밀 수 있게 창구를 등록한다.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            GameServices.RegisterPropPush(this);
            if (!IsServerStarted)
                RequestSnapshotServerRpc();
        }

        /// <summary>
        /// 접속이 끊긴 뒤 충돌체가 꺼진 채 남지 않도록 보간 기록을 정리한다.
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();
            GameServices.UnregisterPropPush(this);
            interpolator.Clear();
        }

        /// <inheritdoc />
        public void RequestPush(int propIndex, Vector3 point, Vector3 velocity)
        {
            PushServerRpc(propIndex, point, velocity);
        }

        /// <summary>
        /// 클라이언트는 조작할 수 있으므로 요청한 사람의 아바타가 정말 그 사물 옆에 있는지 확인하고, 세기는 서버가 정한다.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void PushServerRpc(int propIndex, Vector3 point, Vector3 velocity, NetworkConnection caller = null)
        {
            if (caller == null || !PropRegistry.TryGet(propIndex, out TransformableProp prop) || !prop.TryGetComponent(out PropBody body))
                return;
            if (nextPushTimes.TryGetValue(propIndex, out float allowedTime) && Time.time < allowedTime)
                return;
            if (!TryGetAvatarPosition(caller, out Vector3 avatarPosition))
                return;

            Bounds propBounds = body.SolidBounds;
            if (!PropPhysicsRules.IsPushPlausible(propBounds, avatarPosition, point))
                return;

            Vector3 impulse = PropPhysicsRules.GetPushImpulse(velocity, body.Body.mass);
            if (impulse == Vector3.zero)
                return;

            nextPushTimes[propIndex] = Time.time + PushInterval;
            ApplyImpact(propIndex, PropPhysicsRules.GetPushPoint(propBounds, point), impulse);
        }

        /// <summary>요청에는 아바타가 들어 있지 않아서, 연결이 가진 오브젝트 중 플레이어 아바타를 찾는다.</summary>
        private static bool TryGetAvatarPosition(NetworkConnection connection, out Vector3 position)
        {
            foreach (NetworkObject owned in connection.Objects)
            {
                if (owned != null && owned.TryGetComponent(out Player.PlayerIdentity _))
                {
                    position = owned.transform.position;
                    return true;
                }
            }
            position = default;
            return false;
        }

        /// <summary>재접속한 사람에게 보내려고 움직인 사물을 기억해 두고 깨운다.</summary>
        public void ApplyImpact(int propIndex, Vector3 point, Vector3 impulse)
        {
            if (!IsServerStarted || !PropRegistry.TryGet(propIndex, out TransformableProp prop) || !prop.TryGetComponent(out PropBody body))
                return;

            displaced.Add(body);
            settled.Clear();
            simulation.Activate(body, point, impulse, Time.time, settled);
            SendSettled();
        }

        /// <summary>물리와 같은 주기로 판단하도록 FixedUpdate에서 진행하고, 대역폭을 아끼려고 정해진 간격으로만 보낸다.</summary>
        private void FixedUpdate()
        {
            if (!IsServerStarted || simulation.ActiveCount == 0)
                return;

            moving.Clear();
            settled.Clear();
            simulation.Step(Time.time, Time.fixedDeltaTime, moving, settled);
            SendSettled();

            if (moving.Count > 0 && Time.time >= nextSendTime)
            {
                nextSendTime = Time.time + 1f / GameTuning.PropSyncRate;
                MovingObserversRpc(TimeManager.Tick, ToPoses(moving), Channel.Unreliable);
            }
        }

        /// <summary>화면은 프레임마다 그려지므로 보간은 Update에서 하고, 호스트는 서버 물리를 그대로 본다.</summary>
        private void Update()
        {
            if (!IsClientStarted || IsServerStarted)
                return;

            ApplyPendingSnapshot();
            interpolator.Apply(Time.unscaledTimeAsDouble);
        }

        /// <summary>
        /// 맵 사물 섞기가 나중에 돌면 받은 위치를 덮어쓰므로 맵 준비가 끝날 때까지 기다렸다가 적용한다.
        /// </summary>
        private void ApplyPendingSnapshot()
        {
            if (pendingSnapshot == null || Map.MapPropSetup.HasPending)
                return;

            interpolator.AddSettled(TimeManager.TicksToTime(pendingSnapshotTick), pendingSnapshot);
            pendingSnapshot = null;
        }

        /// <summary>요청 남발로 서버가 바빠지지 않도록 연결마다 간격을 둔다.</summary>
        [ServerRpc(RequireOwnership = false)]
        private void RequestSnapshotServerRpc(NetworkConnection caller = null)
        {
            if (caller == null)
                return;
            if (nextSnapshotTimes.TryGetValue(caller.ClientId, out float allowedTime) && Time.unscaledTime < allowedTime)
                return;

            nextSnapshotTimes[caller.ClientId] = Time.unscaledTime + SnapshotRequestInterval;
            displaced.RemoveWhere(body => body == null);
            if (displaced.Count > 0)
                SnapshotTargetRpc(caller, TimeManager.Tick, ToPoses(new List<PropBody>(displaced)));
        }

        /// <summary>맵 준비가 끝나지 않았을 수 있어 바로 적용하지 않고 받아만 둔다.</summary>
        [TargetRpc]
        private void SnapshotTargetRpc(NetworkConnection target, uint tick, PropPoseData[] poses)
        {
            pendingSnapshotTick = tick;
            pendingSnapshot = poses;
        }

        /// <summary>멈춘 위치를 잃으면 PC마다 자리가 달라지므로 신뢰 채널로 보낸다.</summary>
        private void SendSettled()
        {
            if (settled.Count > 0)
                SettledObserversRpc(TimeManager.Tick, ToPoses(settled));
        }

        /// <summary>움직이는 위치는 하나쯤 빠져도 다음 값이 오므로 비신뢰로 받는다.</summary>
        [ObserversRpc]
        private void MovingObserversRpc(uint tick, PropPoseData[] poses, Channel channel = Channel.Unreliable)
        {
            if (IsServerStarted)
                return;
            interpolator.AddMoving(TimeManager.TicksToTime(tick), Time.unscaledTimeAsDouble, poses);
        }

        /// <summary>모든 PC에서 같은 자리에 남도록 멈춘 사물의 최종 위치를 받는다.</summary>
        [ObserversRpc]
        private void SettledObserversRpc(uint tick, PropPoseData[] poses)
        {
            if (IsServerStarted)
                return;
            interpolator.AddSettled(TimeManager.TicksToTime(tick), poses);
        }

        /// <summary>
        /// 화면 보간된 트랜스폼은 실제 물리 위치보다 늦으므로, 물리 상태 기준으로 전송용 자세 배열을 만든다.
        /// </summary>
        private static PropPoseData[] ToPoses(List<PropBody> bodies)
        {
            var poses = new PropPoseData[bodies.Count];
            for (int i = 0; i < bodies.Count; i++)
            {
                Rigidbody body = bodies[i].Body;
                poses[i] = new PropPoseData { index = bodies[i].Index, position = body.position, rotation = body.rotation };
            }
            return poses;
        }
    }
}
