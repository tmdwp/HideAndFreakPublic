using System.Collections.Generic;
using GuardVsMannequin.Core;
using GuardVsMannequin.Player;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.PropPhysics
{
    /// <summary>
    /// 서버는 아바타 이동을 시뮬레이션하지 않아 충돌을 모르므로, 내 아바타가 옆면으로 변신 사물에 부딪히면 서버에 밀어 달라고 알린다.
    /// </summary>
    public class PropPushReporter : MonoBehaviour
    {
        // 사물에 기대고 서 있으면 물리 틱마다 충돌이 나므로 같은 사물은 이 간격(초)마다만 알린다.
        private const float SameTargetInterval = 0.3f;

        private ICharacterMotor motor;
        private readonly Dictionary<int, float> nextReportTimes = new();

        /// <summary>충돌마다 찾지 않도록 미리 찾아 둔다.</summary>
        private void Awake()
        {
            motor = GetComponent<ICharacterMotor>();
        }

        /// <summary>꺼진 동안 이벤트가 이 객체를 부르지 않도록 켜질 때만 구독한다.</summary>
        private void OnEnable()
        {
            if (motor != null)
                motor.SideCollided += OnSideCollided;
        }

        private void OnDisable()
        {
            if (motor != null)
                motor.SideCollided -= OnSideCollided;
        }

        /// <summary>천천히 스친 것까지 보내면 사물이 계속 흔들리므로, 정한 속도 이상으로 부딪힌 변신 사물만 알린다.</summary>
        private void OnSideCollided(Collider other, Vector3 point, Vector3 velocity)
        {
            IPropPushRequester requester = GameServices.PropPush;
            if (requester == null || GameTuning.PlayerPushStrength <= 0f)
                return;

            Rigidbody otherBody = other.attachedRigidbody;
            if (!PropPhysicsRules.IsRestingProp(otherBody) || !otherBody.TryGetComponent(out PropBody propBody))
                return;
            if (Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude < GameTuning.PlayerPushMinSpeed)
                return;

            int propIndex = propBody.Index;
            if (nextReportTimes.TryGetValue(propIndex, out float allowedTime) && Time.time < allowedTime)
                return;
            nextReportTimes[propIndex] = Time.time + SameTargetInterval;

            requester.RequestPush(propIndex, point, velocity);
        }
    }
}
