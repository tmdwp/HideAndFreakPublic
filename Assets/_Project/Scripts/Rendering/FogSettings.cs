using UnityEngine;

namespace GuardVsMannequin.Rendering
{
    /// <summary>안개를 잠시 바꾸는 연출들이 같은 방식으로 원래 값을 저장·복원하도록 씬 안개 설정을 묶는다.</summary>
    public readonly struct FogSettings
    {
        private readonly bool enabled;
        private readonly FogMode mode;
        private readonly Color color;
        private readonly float startDistance;
        private readonly float endDistance;

        /// <summary>시야를 줄이는 연출은 모두 선형 안개라 그 값만 받는다.</summary>
        public FogSettings(bool enabled, FogMode mode, Color color, float startDistance, float endDistance)
        {
            this.enabled = enabled;
            this.mode = mode;
            this.color = color;
            this.startDistance = startDistance;
            this.endDistance = endDistance;
        }

        /// <summary>시작 거리보다 끝 거리가 짧으면 안개가 깨져 보이므로 최소 간격을 둔 선형 안개를 만든다.</summary>
        public static FogSettings Linear(Color color, float startDistance, float endDistance)
        {
            return new FogSettings(true, FogMode.Linear, color, startDistance, Mathf.Max(startDistance + 0.1f, endDistance));
        }

        /// <summary>
        /// 시야가 한 번에 열리면 화면이 튀므로 선형 안개 거리를 점점 늘린다. 목표가 선형 안개가 아니면 비교할 거리가 없어
        /// <paramref name="clearDistance"/>까지 늘린 뒤 호출하는 쪽이 원래 설정으로 바꾼다.
        /// </summary>
        /// <param name="progress">0이면 지금 값, 1이면 목표 값.</param>
        public FogSettings FadeToward(FogSettings target, float clearDistance, float progress)
        {
            bool isTargetLinear = target.enabled && target.mode == FogMode.Linear;
            float targetStart = isTargetLinear ? target.startDistance : clearDistance;
            float targetEnd = isTargetLinear ? target.endDistance : clearDistance;
            Color targetColor = target.enabled ? target.color : color;

            float t = Mathf.SmoothStep(0f, 1f, progress);
            return Linear(Color.Lerp(color, targetColor, t), Mathf.Lerp(startDistance, targetStart, t), Mathf.Lerp(endDistance, targetEnd, t));
        }

        /// <summary>연출이 끝나면 맵 원래 안개로 돌아가야 해서 바꾸기 전에 저장한다.</summary>
        public static FogSettings Capture()
        {
            return new FogSettings(RenderSettings.fog, RenderSettings.fogMode, RenderSettings.fogColor,
                RenderSettings.fogStartDistance, RenderSettings.fogEndDistance);
        }

        /// <summary>안개는 씬 전역 설정이라 RenderSettings에 한꺼번에 넣는다.</summary>
        public void Apply()
        {
            RenderSettings.fog = enabled;
            RenderSettings.fogMode = mode;
            RenderSettings.fogColor = color;
            RenderSettings.fogStartDistance = startDistance;
            RenderSettings.fogEndDistance = endDistance;
        }
    }
}
