using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>걷기 출렁임처럼 빠른 움직임만 남기도록 뼈 위치에서 천천히 따라오는 기준점을 뺀다.</summary>
    public sealed class BoneSwayTracker
    {
        private Vector3 baseline;
        private bool hasBaseline;

        /// <summary>
        /// 외형이 바뀌면 뼈 위치가 달라져 첫 프레임에 큰 흔들림이 튀므로, 기준점을 다시 잡게 한다.
        /// </summary>
        public void Reset()
        {
            hasBaseline = false;
        }

        /// <summary>
        /// 기준점을 지수 감쇠로 따라가게 해서 프레임레이트가 달라도 같은 흔들림이 나오게 한다.
        /// </summary>
        /// <param name="boneLocalPosition">모델 기준 뼈 위치.</param>
        /// <param name="smoothTime">기준점이 따라오는 시간(초).</param>
        /// <param name="axisWeights">축별 배율.</param>
        public Vector3 Sample(Vector3 boneLocalPosition, float deltaTime, float smoothTime, Vector3 axisWeights)
        {
            if (!hasBaseline)
            {
                baseline = boneLocalPosition;
                hasBaseline = true;
            }

            float followRatio = smoothTime > 0f ? 1f - Mathf.Exp(-deltaTime / smoothTime) : 1f;
            baseline = Vector3.Lerp(baseline, boneLocalPosition, followRatio);

            return Vector3.Scale(boneLocalPosition - baseline, axisWeights);
        }
    }
}
