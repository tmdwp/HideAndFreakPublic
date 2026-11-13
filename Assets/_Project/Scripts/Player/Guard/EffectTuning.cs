using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>원본 프리팹은 그대로 두고 생성한 이펙트 사본의 밝기와 크기만 조정한다.</summary>
    public static class EffectTuning
    {
        /// <summary>
        /// 에셋 스토어 이펙트를 그대로 쓰면 너무 밝거나 커서, 사본의 모든 파티클 시작 색·크기에 배율을 곱한다.
        /// </summary>
        /// <param name="brightness">1 = 원본, 0.5 = 절반.</param>
        /// <param name="scale">1 = 원본.</param>
        public static void Apply(GameObject effect, float brightness, float scale)
        {
            if (effect == null)
                return;

            foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particles.main;
                main.startColor = Scale(main.startColor, brightness);
                main.startSizeMultiplier *= scale;
            }
        }

        /// <summary>
        /// 시작 색 모드가 이펙트마다 달라서 모드별로 색을 꺼내 배율을 곱한다.
        /// </summary>
        private static ParticleSystem.MinMaxGradient Scale(ParticleSystem.MinMaxGradient source, float factor)
        {
            switch (source.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(Scale(source.color, factor));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(Scale(source.colorMin, factor), Scale(source.colorMax, factor));
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(Scale(source.gradient, factor));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(Scale(source.gradientMin, factor), Scale(source.gradientMax, factor));
                default:
                    return source;
            }
        }

        /// <summary>
        /// 가산 합성 이펙트는 알파만 줄여서는 연해지지 않으므로 RGB까지 함께 곱한다.
        /// </summary>
        private static Color Scale(Color color, float factor)
        {
            return new Color(color.r * factor, color.g * factor, color.b * factor, color.a * factor);
        }

        /// <summary>
        /// 원본 그라디언트를 고치면 프리팹 에셋이 바뀌므로 키를 복사해 새 그라디언트를 만든다.
        /// </summary>
        private static Gradient Scale(Gradient source, float factor)
        {
            if (source == null)
                return null;

            GradientColorKey[] colorKeys = source.colorKeys;
            for (int i = 0; i < colorKeys.Length; i++)
                colorKeys[i].color = new Color(colorKeys[i].color.r * factor, colorKeys[i].color.g * factor, colorKeys[i].color.b * factor);

            GradientAlphaKey[] alphaKeys = source.alphaKeys;
            for (int i = 0; i < alphaKeys.Length; i++)
                alphaKeys[i].alpha *= factor;

            var result = new Gradient { mode = source.mode };
            result.SetKeys(colorKeys, alphaKeys);
            return result;
        }
    }
}
