using UnityEngine;

namespace GuardVsMannequin.UI
{
    /// <summary>각도로 나누면 속도가 들쭉날쭉하므로 호 길이 표로 일정한 속도를 내는 타원 순환 경로.</summary>
    public class LobbyChasePath : MonoBehaviour
    {
        private const int SampleCount = 128;

        [Tooltip("좌우(X) 반지름(m).")]
        [SerializeField] private float radiusX = 7f;

        [Tooltip("앞뒤(Z) 반지름(m).")]
        [SerializeField] private float radiusZ = 3f;

        [Tooltip("켜면 위에서 볼 때 시계 방향으로 돈다.")]
        [SerializeField] private bool clockwise;

        // cumulativeLengths[i] = 샘플 0부터 i까지의 길이 (마지막 = 전체 길이)
        private float[] cumulativeLengths;

        /// <summary>표가 아직 없을 수 있어 읽을 때 만드는 둘레 길이(m).</summary>
        public float Length
        {
            get
            {
                EnsureTable();
                return cumulativeLengths[SampleCount];
            }
        }

        /// <summary>속도가 일정하도록 거리로 위치를 구하고, 캐릭터가 기울지 않게 진행 방향은 수평으로 눕힌다.</summary>
        public Pose Evaluate(float distance)
        {
            EnsureTable();
            float wrappedDistance = Mathf.Repeat(distance, Length);

            int sampleIndex = System.Array.BinarySearch(cumulativeLengths, wrappedDistance);
            if (sampleIndex < 0)
                sampleIndex = ~sampleIndex - 1;
            sampleIndex = Mathf.Clamp(sampleIndex, 0, SampleCount - 1);

            float segmentLength = cumulativeLengths[sampleIndex + 1] - cumulativeLengths[sampleIndex];
            float segmentProgress = segmentLength > 0f ? (wrappedDistance - cumulativeLengths[sampleIndex]) / segmentLength : 0f;
            float angle = (sampleIndex + segmentProgress) / SampleCount * Mathf.PI * 2f;

            Vector3 position = transform.TransformPoint(LocalPoint(angle));
            Vector3 forward = Vector3.ProjectOnPlane(transform.TransformDirection(LocalTangent(angle)), Vector3.up);
            Quaternion rotation = forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward, Vector3.up) : transform.rotation;
            return new Pose(position, rotation);
        }

        /// <summary>
        /// 반지름을 바꾸면 기존 길이 표가 맞지 않으므로 버려서 다음에 다시 만들게 한다.
        /// </summary>
        private void OnValidate()
        {
            cumulativeLengths = null;
        }

        /// <summary>
        /// 시계 방향 옵션을 Z축 부호로 처리해 같은 식으로 양방향을 다룬다.
        /// </summary>
        private Vector3 LocalPoint(float angle)
        {
            float direction = clockwise ? -1f : 1f;
            return new Vector3(Mathf.Cos(angle) * radiusX, 0f, Mathf.Sin(angle) * radiusZ * direction);
        }

        /// <summary>
        /// 캐릭터가 경로 방향을 바라보도록 LocalPoint의 미분으로 진행 방향을 구한다.
        /// </summary>
        private Vector3 LocalTangent(float angle)
        {
            float direction = clockwise ? -1f : 1f;
            return new Vector3(-Mathf.Sin(angle) * radiusX, 0f, Mathf.Cos(angle) * radiusZ * direction);
        }

        /// <summary>타원 둘레는 닫힌 식이 없어 잘게 나눈 선분 길이를 누적한 표를 한 번만 만든다.</summary>
        private void EnsureTable()
        {
            if (cumulativeLengths != null)
                return;

            cumulativeLengths = new float[SampleCount + 1];
            Vector3 previousPoint = LocalPoint(0f);
            for (int i = 1; i <= SampleCount; i++)
            {
                Vector3 currentPoint = LocalPoint((float)i / SampleCount * Mathf.PI * 2f);
                cumulativeLengths[i] = cumulativeLengths[i - 1] + Vector3.Distance(previousPoint, currentPoint);
                previousPoint = currentPoint;
            }
        }

        /// <summary>
        /// 배경 연출을 배치할 때 경로가 보여야 소품과 겹치는지 확인할 수 있으므로 씬 뷰에 그린다.
        /// </summary>
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
            Vector3 previousPoint = transform.TransformPoint(LocalPoint(0f));
            for (int i = 1; i <= SampleCount; i++)
            {
                Vector3 currentPoint = transform.TransformPoint(LocalPoint((float)i / SampleCount * Mathf.PI * 2f));
                Gizmos.DrawLine(previousPoint, currentPoint);
                previousPoint = currentPoint;
            }
        }
    }
}
