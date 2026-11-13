using GuardVsMannequin.Core;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.PropPhysics
{
    /// <summary>평소 물리 비용이 거의 없도록 키네마틱으로 두고 서버만 맞은 사물을 시뮬레이션하는 변신 사물 몸체.</summary>
    [DisallowMultipleComponent]
    public class PropBody : MonoBehaviour, IImpactReceiver
    {
        private TransformableProp prop;
        private Rigidbody body;
        private Collider[] solidColliders;
        private Pose startPose;

        /// <summary>사물은 네트워크 오브젝트가 아니어서 이 번호로 동기화한다.</summary>
        public int Index => prop.Index;

        public Rigidbody Body => body;

        /// <summary>맵 밖으로 떨어지면 돌아올 곳으로, 섞기 뒤의 자리를 기억해 둔다.</summary>
        public Pose StartPose => startPose;

        /// <inheritdoc />
        public float ImpactMass => body.mass;

        /// <summary>날아가는 동안만 다른 물체를 깨우도록 서버에서 시뮬레이션 중인지 알려 준다.</summary>
        public bool IsDynamic => !body.isKinematic;

        /// <summary>미는 지점·거리 검사가 실제로 부딪히는 모양을 기준으로 하도록 충돌체 경계를 합친다.</summary>
        public Bounds SolidBounds
        {
            get
            {
                var bounds = new Bounds(transform.position, Vector3.zero);
                bool hasAny = false;
                foreach (Collider part in solidColliders)
                {
                    if (part == null || !part.enabled)
                        continue;
                    if (hasAny)
                        bounds.Encapsulate(part.bounds);
                    else
                        bounds = part.bounds;
                    hasAny = true;
                }
                return bounds;
            }
        }

        /// <summary>
        /// 맵 프리팹을 고치지 않아도 되도록 실행 중에 몸체를 붙인다.
        /// </summary>
        public static PropBody Attach(TransformableProp prop)
        {
            PropBody propBody = prop.gameObject.AddComponent<PropBody>();
            propBody.Initialize(prop);
            return propBody;
        }

        /// <summary>물리 비용이 들지 않도록 키네마틱으로 시작하고, Rigidbody가 둘이 되지 않게 기존 것을 쓴다.</summary>
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

        /// <summary>다시 깨울 때 튀지 않도록 속도를 없애고, 움직이는 동안만 화면 보간을 켠다.</summary>
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

        /// <summary>맞은 지점에 따라 회전도 달라지도록 위치를 지정해 충격을 준다.</summary>
        public void ApplyImpulse(Vector3 point, Vector3 impulse)
        {
            body.AddForceAtPosition(impulse, point, ForceMode.Impulse);
        }

        /// <summary>
        /// 키네마틱 몸체는 transform만 바꾸면 물리 위치가 늦게 따라오므로 둘 다 바로 옮긴다.
        /// </summary>
        public void SetPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
        }

        /// <summary>
        /// 클라이언트는 사물이 날아가는 동안 꺼서, 늦게 도착하는 위치에 플레이어가 밀리거나 끼지 않게 한다.
        /// </summary>
        public void SetCollidersEnabled(bool enabled)
        {
            foreach (Collider part in solidColliders)
            {
                if (part != null)
                    part.enabled = enabled;
            }
        }

        /// <summary>물리는 서버만 시뮬레이션하므로 받은 충격을 물리 서비스에 넘긴다.</summary>
        public void ReceiveImpact(Vector3 point, Vector3 impulse)
        {
            GameServices.PropPhysics?.ApplyImpact(prop.Index, point, impulse);
        }

        /// <summary>키네마틱 사물은 물리만으로 밀리지 않으므로 날아가는 동안 부딪힌 대상을 직접 깨운다.</summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (IsDynamic)
                ImpactRelay.Relay(collision, this);
        }
    }
}
