using UnityEngine;

namespace GuardVsMannequin.Map
{
    /// <summary>타고 오르는 물체의 종류.</summary>
    public enum ClimbableKind
    {
        /// <summary>사다리: 앞면(+Z)에서만 붙는다.</summary>
        Ladder,
        /// <summary>밧줄: 어느 쪽에서든 붙는다.</summary>
        Rope
    }

    /// <summary>사다리·밧줄을 오를 수 있도록 오르는 선과 위 끝 내려설 방향을 알려 준다.</summary>
    public class Climbable : MonoBehaviour
    {
        [Tooltip("사다리는 앞면(+Z)에서만, 밧줄은 어느 쪽에서든 붙는다.")]
        [SerializeField] private ClimbableKind kind = ClimbableKind.Ladder;

        [Tooltip("오르는 길이(m). 아래 끝(이 오브젝트 위치)에서 위쪽으로.")]
        [SerializeField] private float height = 5f;

        [Tooltip("위 끝에서 내려설 방향(로컬). 사다리는 보통 뒤쪽(−Z, 사다리가 기댄 가구 쪽).")]
        [SerializeField] private Vector3 topExit = Vector3.back;

        [Tooltip("켜면 밧줄도 앞면(+Z) 쪽에 매달린다. (책장 앞처럼 뒤에 막힌 면이 있는 밧줄) 사다리는 항상 앞면.")]
        [SerializeField] private bool holdOnFront;

        /// <summary>사다리는 앞면에서만 붙는다.</summary>
        public bool IsLadder => kind == ClimbableKind.Ladder;

        /// <summary>단위는 m.</summary>
        public float Height => height;

        /// <summary>사다리는 항상, 밧줄은 설정에 따라 앞면 쪽에만 매달린다.</summary>
        public bool HoldsOnFront => IsLadder || holdOnFront;

        public Vector3 Bottom => transform.position;

        public Vector3 Top => transform.position + transform.up * height;

        /// <summary>기울어진 사다리도 지원하려고 transform.up을 쓴 오르는 방향.</summary>
        public Vector3 Axis => transform.up;

        /// <summary>캐릭터가 사다리를 마주 보도록 수평으로 눕힌 앞면 방향.</summary>
        public Vector3 Front => Flatten(transform.forward);

        /// <summary>내려선 캐릭터가 기울지 않도록 수평으로 눕힌 단위 벡터.</summary>
        public Vector3 TopExitDirection => Flatten(transform.TransformDirection(topExit));

        /// <summary>위·아래 끝에 닿았는지 판단하려고 아래 끝에서의 거리(m)를 구한다.</summary>
        public float AlongOf(Vector3 point) => Vector3.Dot(point - Bottom, Axis);

        /// <summary>
        /// 캐릭터가 선 밖으로 밀려나지 않도록 거리를 선 길이로 잘라 선 위의 점을 돌려준다.
        /// </summary>
        public Vector3 PointAt(float along) => Bottom + Axis * Mathf.Clamp(along, 0f, height);

        /// <summary>맵 도구가 직렬화 필드를 직접 건드리지 않도록 설정 창구를 둔다.</summary>
        public void Configure(ClimbableKind newKind, float newHeight, Vector3 newTopExit, bool newHoldOnFront = false)
        {
            kind = newKind;
            holdOnFront = newHoldOnFront;
            height = Mathf.Max(0.5f, newHeight);
            topExit = newTopExit;
        }

        /// <summary>
        /// 방향이 거의 수직이면 정규화 결과가 불안정하므로 그때는 앞쪽으로 대신한다.
        /// </summary>
        private static Vector3 Flatten(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.forward;
        }

        /// <summary>
        /// 맵을 만들 때 오르는 선이 실제 사다리와 맞는지, 내려서는 곳이 막히지 않았는지 확인할 수 있게 그린다.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(Bottom, Top);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(Top, Top + TopExitDirection);
        }
    }
}
