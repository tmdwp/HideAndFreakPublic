using System;
using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>던진 가드가 사라져도 연출이 끝까지 재생되도록 별도 오브젝트로 두는 날아가는 수류탄.</summary>
    public class GrenadeFlight : MonoBehaviour
    {
        // 날아가는 동안 굴러가는 것처럼 보이게 하는 회전 속도(도/초)
        private const float SpinSpeed = 720f;

        private GrenadeSegment[] segments;
        private float fuseTime;
        private float elapsed;
        private Action<Vector3> exploded;

        /// <summary>
        /// 서버가 정한 경로와 신관 시간을 그대로 받아야 모든 화면에서 같은 곳, 같은 순간에 터진다.
        /// </summary>
        public void Launch(GrenadeSegment[] path, float duration, Action<Vector3> onExploded)
        {
            segments = path;
            fuseTime = Mathf.Max(0f, duration);
            exploded = onExploded;
            transform.position = segments[0].start;
        }

        /// <summary>서버와 같은 구간 식을 쓰므로 위치를 받지 않고 시간만으로 따라간다.</summary>
        private void Update()
        {
            elapsed += Time.deltaTime;
            float time = Mathf.Min(elapsed, fuseTime);
            transform.position = GrenadeTrajectory.GetPosition(segments, time);
            if (!GrenadeTrajectory.IsResting(segments, time))
                transform.Rotate(Vector3.right, SpinSpeed * Time.deltaTime, Space.Self);

            if (elapsed < fuseTime)
                return;

            exploded?.Invoke(transform.position);
            Destroy(gameObject);
        }
    }
}
