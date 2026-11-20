using UnityEngine;

namespace GuardVsMannequin.UI
{
    /// <summary>사운드 에셋 없이도 명중·탈락을 귀로 구별하도록 짧은 화면용 합성음을 만든다.</summary>
    public static class UiFeedbackClips
    {
        private const int SampleRate = 44100;

        private static AudioClip hit;
        private static AudioClip eliminate;

        /// <summary>합성이 가볍지 않아 처음 쓸 때 한 번만 만들고, 파괴됐으면 다시 만든다.</summary>
        public static AudioClip Hit => hit != null ? hit : hit = Create("Hit", 0.06f, HitTick);

        /// <summary>합성이 가볍지 않아 처음 쓸 때 한 번만 만들고, 파괴됐으면 다시 만든다.</summary>
        public static AudioClip Eliminate => eliminate != null ? eliminate : eliminate = Create("Eliminate", 0.28f, EliminateChime);

        /// <summary>찢어지는 소리가 나지 않게 -1~1로 자른다.</summary>
        private static AudioClip Create(string name, float length, System.Func<float, float, float> sampler)
        {
            int count = Mathf.CeilToInt(length * SampleRate);
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
                samples[i] = Mathf.Clamp(sampler((float)i / SampleRate, length), -1f, 1f);

            AudioClip clip = AudioClip.Create($"UiFeedback_{name}", count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>총소리에 묻히지 않도록 높고 짧은 "틱".</summary>
        private static float HitTick(float t, float length)
        {
            return Mathf.Sin(2f * Mathf.PI * 1800f * t) * Mathf.Exp(-t * 70f) * 0.55f;
        }

        /// <summary>일반 명중과 구별되도록 두 음이 이어지는 짧은 울림.</summary>
        private static float EliminateChime(float t, float length)
        {
            float first = Mathf.Sin(2f * Mathf.PI * 880f * t) * Mathf.Exp(-t * 18f);
            float secondTime = t - 0.07f;
            float second = secondTime > 0f ? Mathf.Sin(2f * Mathf.PI * 1320f * secondTime) * Mathf.Exp(-secondTime * 14f) : 0f;
            return (first * 0.45f + second * 0.5f);
        }
    }
}
