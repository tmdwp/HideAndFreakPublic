using GuardVsMannequin.Core;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.PropPhysics
{
    /// <summary>
    /// 튕겨나갈 수 있는 변신 사물의 물리 몸체. 맵을 불러올 때 모든 피어가 같은 사물에 붙인다(<see cref="MapPropSetup"/>).
    /// 평소에는 키네마틱이라 물리 비용이 거의 없다. 서버만 맞은 사물을 다이나믹으로 바꿔 시뮬레이션하고,
    /// 클라이언트는 계속 키네마틱으로 두고 서버가 보낸 위치를 따라간다(<see cref="PropPoseInterpolator"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public class PropBody : MonoBehaviour, IImpactReceiver
    {
        private TransformableProp prop;
        private Rigidbody body;
        private Collider[] solidColliders;
        private Pose startPose;

        /// <summary>사물 번호. (동기화 키)</summary>
        public int Index => prop.Index;

        /// <summary>물리 몸체.</summary>
        public Rigidbody Body => body;

        /// <summary>맵을 불러온 직후(섞기 뒤)의 자리. 맵 밖으로 떨어지면 여기로 되돌린다.</summary>
        public Pose StartPose => startPose;

        /// <inheritdoc />
        public float ImpactMass => body.mass;

        /// <summary>지금 다이나믹(서버 시뮬레이션 중)인지 여부.</summary>
        public bool IsDynamic => !body.isKinematic;

        /// <summary>
        /// 사물에 몸체를 붙인다. 에셋에 Rigidbody가 이미 있으면 그것을 쓴다.
        /// </summary>
        public static PropBody Attach(TransformableProp prop)
        {
            PropBody propBody = prop.gameObject.AddComponent<PropBody>();
            propBody.Initialize(prop);
            return propBody;
        }

        /// <summary>
        /// Rigidbody를 키네마틱으로 준비하고 질량·충돌체·시작 자리를 기록한다.
        /// </summary>
        private void Initialize(TransformableProp target)
        {
            prop = target;
            if (!TryGetComponent(out body))
                body = gameObject.AddComponent<Rigidbody>();

            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.mass = PropPhysicsRules.GetMass(prop.Appearance.Bounds);

            var colliders = new System.Collections.Generic.List<Collider>();
            foreach (Collider part in GetComponentsInChildren<Collider>(true))
            {
                if (!part.isTrigger)
                    colliders.Add(part);
            }
            solidColliders = colliders.ToArray();
            startPose = new Pose(transform.position, transform.rotation);
        }

        /// <summary>
        /// 다이나믹/키네마틱을 바꾼다. (서버) 멈출 때는 속도를 없애고, 움직이는 동안만 화면 보간을 켠다(호스트 화면).
        /// </summary>
        public void SetDynamic(bool dynamic)
        {
            if (!dynamic && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = !dynamic;
            body.interpolation = dynamic ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
        }

        /// <summary>
        /// 월드 위치에 충격을 준다. (서버, 다이나믹일 때만 효과가 있다)
        /// </summary>
        public void ApplyImpulse(Vector3 point, Vector3 impulse)
        {
            body.AddForceAtPosition(impulse, point, ForceMode.Impulse);
        }

        /// <summary>
        /// 자리를 바로 옮긴다. (클라이언트가 받은 위치 적용, 맵 밖 추락 복귀)
        /// </summary>
        public void SetPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
        }

        /// <summary>
        /// 충돌체를 켜고 끈다. 클라이언트는 사물이 날아가는 동안 꺼서 늦게 도착하는 위치에 플레이어가 밀리거나 끼지 않게 한다.
        /// </summary>
        public void SetCollidersEnabled(bool enabled)
        {
            foreach (Collider part in solidColliders)
            {
                if (part != null)
                    part.enabled = enabled;
            }
        }

        /// <summary>
        /// 총알 등의 충격을 물리 서비스에 넘긴다. (서버에서만 서비스가 있다)
        /// </summary>
        public void ReceiveImpact(Vector3 point, Vector3 impulse)
        {
            GameServices.PropPhysics?.ApplyImpact(prop.Index, point, impulse);
        }

        /// <summary>
        /// 날아가는 동안 부딪힌 멈춘 사물·변신한 마네킹을 깨운다. (다이나믹 몸체는 서버에만 있다)
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (IsDynamic)
                ImpactRelay.Relay(collision, this);
        }
    }
}
