using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 뼈 하나의 "흔들림"을 잰다: 모델 기준 위치에서 천천히 따라오는 기준점을 뺀 값.
    /// 걷기·달리기의 위아래·좌우 출렁임처럼 빠른 움직임만 남고, 앉기·자세 바뀜처럼 느린 변화는 기준점이 따라가 지워진다.
    /// (값 계산만 한다 — 어디에 쓸지는 <see cref="CharacterSway"/>가 정한다)
    /// </summary>
    public sealed class BoneSwayTracker
    {
        private Vector3 baseline;
        private bool hasBaseline;

        /// <summary>
        /// 기준점을 지운다. (외형이 바뀌어 뼈가 달라졌을 때)
        /// </summary>
        public void Reset()
        {
            hasBaseline = false;
        }

        /// <summary>
        /// 이번 프레임 흔들림을 모델 기준 좌표로 돌려준다.
        /// </summary>
        /// <param name="boneLocalPosition">모델 기준 뼈 위치.</param>
        /// <param name="smoothTime">기준점이 따라오는 시간(초). 길수록 느린 움직임까지 흔들림으로 남는다.</param>
        /// <param name="axisWeights">축별 배율. (좌우, 위아래, 앞뒤)</param>
        public Vector3 Sample(Vector3 boneLocalPosition, float deltaTime, float smoothTime, Vector3 axisWeights)
        {
            if (!hasBaseline)
            {
                baseline = boneLocalPosition;
                hasBaseline = true;
            }

            float follow = smoothTime > 0f ? 1f - Mathf.Exp(-deltaTime / smoothTime) : 1f;
            baseline = Vector3.Lerp(baseline, boneLocalPosition, follow);

            return Vector3.Scale(boneLocalPosition - baseline, axisWeights);
        }
    }
}
