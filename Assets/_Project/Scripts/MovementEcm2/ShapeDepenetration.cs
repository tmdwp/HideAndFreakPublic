using System.Collections.Generic;
using ECM2;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>ECM2는 캡슐로만 움직여서, 캡슐 밖으로 튀어나온 사물 모양이 벽에 박히면 이동이 끝난 뒤 수평으로 밀어낸다.</summary>
    internal sealed class ShapeDepenetration
    {
        // 여러 벽에 동시에 닿은 구석에서도 빠져나오도록 몇 번 반복하되, 한 틱 비용은 묶어 둔다.
        private const int MaxIterations = 3;

        // 아주 얇게 닿은 것까지 밀면 벽을 따라 미끄러질 때 떨리므로 무시하는 깊이(m).
        private const float MinPushDistance = 0.001f;

        private static readonly Collider[] overlapBuffer = new Collider[32];

        private readonly CharacterMovement movement;
        private readonly List<BoxCollider> shapeBoxes = new();

        /// <summary>이동 결과와 같은 값을 고치도록 ECM2 이동 컴포넌트를 받는다.</summary>
        public ShapeDepenetration(CharacterMovement characterMovement)
        {
            movement = characterMovement;
        }

        /// <summary>호출하는 쪽 목록이 나중에 바뀌어도 영향받지 않도록 복사해 둔다.</summary>
        public void SetShapes(IReadOnlyList<BoxCollider> boxes)
        {
            shapeBoxes.Clear();
            if (boxes == null)
                return;
            foreach (BoxCollider box in boxes)
            {
                if (box != null)
                    shapeBoxes.Add(box);
            }
        }

        /// <summary>
        /// 밀려난 뒤에도 같은 방향 속도가 남아 다시 박히지 않도록, 미는 방향으로 들어가던 속도도 없앤다.
        /// </summary>
        public void Resolve()
        {
            if (shapeBoxes.Count == 0)
                return;

            for (int iteration = 0; iteration < MaxIterations; iteration++)
            {
                Vector3 push = FindPush();
                if (push.sqrMagnitude < MinPushDistance * MinPushDistance)
                    return;

                movement.SetPosition(movement.GetPosition() + push);

                Vector3 normal = push.normalized;
                float intoWallSpeed = Vector3.Dot(movement.velocity, normal);
                if (intoWallSpeed < 0f)
                    movement.velocity -= normal * intoWallSpeed;
            }
        }

        /// <summary>
        /// 두 상자가 같은 벽에 닿았을 때 두 번 밀지 않도록, 이미 모은 이동으로 부족한 만큼만 더한다.
        /// </summary>
        private Vector3 FindPush()
        {
            Vector3 total = Vector3.zero;
            foreach (BoxCollider box in shapeBoxes)
            {
                if (!box.enabled || !box.gameObject.activeInHierarchy)
                    continue;

                Transform boxTransform = box.transform;
                Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, Abs(boxTransform.lossyScale));
                int count = Physics.OverlapBoxNonAlloc(boxTransform.TransformPoint(box.center), halfExtents, overlapBuffer,
                    boxTransform.rotation, movement.collisionLayers, QueryTriggerInteraction.Ignore);

                for (int i = 0; i < count; i++)
                {
                    if (!TryGetPush(box, overlapBuffer[i], out Vector3 push))
                        continue;

                    float pushDistance = push.magnitude;
                    Vector3 direction = push / pushDistance;
                    float alreadyPushed = Vector3.Dot(total, direction);
                    if (alreadyPushed < pushDistance)
                        total += direction * (pushDistance - alreadyPushed);
                }
            }
            return total;
        }

        /// <summary>
        /// 계단·턱은 캡슐이 올라가므로, 상자를 오를 수 있는 높이만큼 들어 올려도 박히는 것만 벽으로 보고 수평 성분만 쓴다.
        /// </summary>
        private bool TryGetPush(BoxCollider box, Collider other, out Vector3 push)
        {
            push = Vector3.zero;
            if (IsIgnored(other))
                return false;

            Transform boxTransform = box.transform;
            Transform otherTransform = other.transform;
            if (!Physics.ComputePenetration(box, boxTransform.position, boxTransform.rotation,
                    other, otherTransform.position, otherTransform.rotation, out Vector3 direction, out float distance))
                return false;

            Vector3 raisedPosition = boxTransform.position + Vector3.up * movement.stepOffset;
            if (!Physics.ComputePenetration(box, raisedPosition, boxTransform.rotation,
                    other, otherTransform.position, otherTransform.rotation, out _, out _))
                return false;

            push = Vector3.ProjectOnPlane(direction * distance, Vector3.up);
            return push.sqrMagnitude >= MinPushDistance * MinPushDistance;
        }

        /// <summary>
        /// 자기 몸과는 겨루지 않고, 물리로 움직이는 물체는 상자가 밀어내도록 건너뛴다.
        /// </summary>
        private bool IsIgnored(Collider other)
        {
            Rigidbody otherBody = other.attachedRigidbody;
            if (otherBody != null && (otherBody == movement.rigidbody || !otherBody.isKinematic))
                return true;
            return other == movement.collider;
        }

        /// <summary>뒤집힌 배율이 상자 크기를 음수로 만들지 않도록 한다.</summary>
        private static Vector3 Abs(Vector3 value) => new(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }
}
