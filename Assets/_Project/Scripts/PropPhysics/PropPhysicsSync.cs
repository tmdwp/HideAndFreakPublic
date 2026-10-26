using System.Collections.Generic;
using FishNet.Object;
using FishNet.Transporting;
using GuardVsMannequin.Core;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.PropPhysics
{
    /// <summary>
    /// 변신 사물 물리의 네트워크 창구. (게임 시스템 씬의 서버 오브젝트에 붙는다)
    /// 서버: 충격을 받아 <see cref="PropPhysicsSimulation"/>을 진행하고, 움직이는 사물만 번호·위치·회전으로 묶어 보낸다(비신뢰, 초당 <see cref="GameTuning.PropSyncRate"/>회).
    /// 멈춘 위치는 신뢰 채널로 한 번 보낸다. 사물마다 NetworkObject를 쓰지 않는다.
    /// 클라이언트: 받은 위치를 <see cref="PropPoseInterpolator"/>로 보간해 적용한다. (호스트는 서버 물리를 그대로 보므로 적용하지 않음)
    /// </summary>
    public class PropPhysicsSync : NetworkBehaviour, IPropPhysics
    {
        private readonly PropPhysicsSimulation simulation = new();
        private readonly PropPoseInterpolator interpolator = new();
        private readonly List<PropBody> moving = new();
        private readonly List<PropBody> settled = new();
        private float nextSendTime;

        /// <summary>
        /// 서버에서 물리 서비스로 등록한다.
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();
            GameServices.RegisterPropPhysics(this);
        }

        /// <summary>
        /// 등록을 해제하고 움직이던 사물을 멈춘다.
        /// </summary>
        public override void OnStopServer()
        {
            base.OnStopServer();
            GameServices.UnregisterPropPhysics(this);
            simulation.Clear();
        }

        /// <summary>
        /// 클라이언트 보간 기록을 지운다.
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();
            interpolator.Clear();
        }

        /// <summary>
        /// 사물을 깨워 충격을 준다. 상한 때문에 멈춘 사물이 있으면 그 위치를 보낸다. (서버)
        /// </summary>
        public void ApplyImpact(int propIndex, Vector3 point, Vector3 impulse)
        {
            if (!IsServerStarted || !PropRegistry.TryGet(propIndex, out TransformableProp prop) || !prop.TryGetComponent(out PropBody body))
                return;

            settled.Clear();
            simulation.Activate(body, point, impulse, Time.time, settled);
            SendSettled();
        }

        /// <summary>
        /// 서버: 물리 단계마다 멈춤·추락을 정리하고 정해진 간격으로 움직이는 사물 위치를 보낸다.
        /// </summary>
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

        /// <summary>
        /// 클라이언트: 받은 위치를 보간해 적용한다.
        /// </summary>
        private void Update()
        {
            if (IsClientStarted && !IsServerStarted)
                interpolator.Apply(Time.unscaledTimeAsDouble);
        }

        /// <summary>
        /// 이번에 멈춘 사물의 최종 위치를 신뢰 채널로 보낸다.
        /// </summary>
        private void SendSettled()
        {
            if (settled.Count > 0)
                SettledObserversRpc(TimeManager.Tick, ToPoses(settled));
        }

        /// <summary>
        /// 움직이는 사물 위치를 받는다. (클라이언트)
        /// </summary>
        [ObserversRpc]
        private void MovingObserversRpc(uint tick, PropPoseData[] poses, Channel channel = Channel.Unreliable)
        {
            if (IsServerStarted)
                return;
            interpolator.AddMoving(TimeManager.TicksToTime(tick), Time.unscaledTimeAsDouble, poses);
        }

        /// <summary>
        /// 멈춘 사물의 최종 위치를 받는다. (클라이언트)
        /// </summary>
        [ObserversRpc]
        private void SettledObserversRpc(uint tick, PropPoseData[] poses)
        {
            if (IsServerStarted)
                return;
            interpolator.AddSettled(TimeManager.TicksToTime(tick), poses);
        }

        /// <summary>
        /// 몸체 목록을 전송용 자세 배열로 바꾼다. (화면 보간된 트랜스폼이 아닌 물리 상태 기준)
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
