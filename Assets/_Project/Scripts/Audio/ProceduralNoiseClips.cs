using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Audio
{
    /// <summary>
    /// 사운드 에셋이 준비되기 전까지 사용할 임시 합성음을 만든다.
    /// 종류별로 한 번만 생성해 캐시한다.
    /// </summary>
    public static class ProceduralNoiseClips
    {
        private const int SampleRate = 44100;

        private static readonly Dictionary<NoiseType, AudioClip> cache = new();

        /// <summary>
        /// 소음 종류에 맞는 합성음을 반환한다.
        /// </summary>
        public static AudioClip Get(NoiseType type)
        {
            if (cache.TryGetValue(type, out AudioClip clip) && clip != null)
                return clip;

            clip = type switch
            {
                NoiseType.GunShot => Create("GunShot", 0.3f, GunShot),
                NoiseType.DisguiseCreak => Create("DisguiseCreak", 0.3f, ShortCreak),
                NoiseType.GeneratorDestroyed => Create("GeneratorDestroyed", 1.0f, Boom),
                NoiseType.PowerDown => Create("PowerDown", 1.8f, PowerDown),
                NoiseType.MannequinAttack => Create("MannequinAttack", 0.25f, Swipe),
                NoiseType.FlashlightClick => Create("FlashlightClick", 0.05f, Click),
                NoiseType.GrenadeExplosion => Create("GrenadeExplosion", 0.9f, Blast),
                NoiseType.GhoulBreath => Create("GhoulBreath", 2.4f, GhoulBreath),
                NoiseType.GhoulStep => Create("GhoulStep", 0.2f, GhoulStep),
                NoiseType.GhoulShriek => Create("GhoulShriek", 1.4f, GhoulShriek),
                NoiseType.GuardHeartbeat => Create("GuardHeartbeat", 0.9f, Heartbeat),
                _ => Create("Default", 0.2f, Ping)
            };

            cache[type] = clip;
            return clip;
        }

        /// <summary>
        /// 시간(초)과 길이(초)를 받아 샘플 값을 돌려주는 함수로 모노 클립을 만든다.
        /// </summary>
        private static AudioClip Create(string name, float length, System.Func<float, float, float> sampler)
        {
            int count = Mathf.CeilToInt(length * SampleRate);
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
                samples[i] = Mathf.Clamp(sampler((float)i / SampleRate, length), -1f, 1f);

            AudioClip clip = AudioClip.Create($"Procedural_{name}", count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>총소리: 빠르게 감쇠하는 백색 소음.</summary>
        private static float GunShot(float t, float length)
        {
            return (Random.value * 2f - 1f) * Mathf.Exp(-t * 18f);
        }

        /// <summary>기본음: 높은 사인파 핑.</summary>
        private static float Ping(float t, float length)
        {
            return Mathf.Sin(2f * Mathf.PI * 1200f * t) * Mathf.Exp(-t * 8f) * 0.6f;
        }

        /// <summary>변신 소음: 짧은 삐걱임.</summary>
        private static float ShortCreak(float t, float length)
        {
            float frequency = 320f - t * 400f;
            float saw = 2f * (t * frequency - Mathf.Floor(t * frequency + 0.5f));
            return saw * Mathf.Sin(Mathf.PI * t / length) * 0.4f;
        }

        /// <summary>전원 차단: "쿵" 하는 저음과 함께 낮아지며 사라지는 험 노이즈.</summary>
        private static float PowerDown(float t, float length)
        {
            float thump = Mathf.Sin(2f * Mathf.PI * 45f * t) * Mathf.Exp(-t * 6f);
            float frequency = Mathf.Lerp(120f, 30f, t / length);
            float hum = Mathf.Sin(2f * Mathf.PI * frequency * t) * (1f - t / length) * 0.4f;
            return thump * 0.8f + hum;
        }

        /// <summary>마네킹 공격: 짧게 휘두르는 바람 소리.</summary>
        private static float Swipe(float t, float length)
        {
            return (Random.value * 2f - 1f) * Mathf.Sin(Mathf.PI * t / length) * 0.6f;
        }

        /// <summary>손전등 스위치: 아주 짧은 클릭.</summary>
        private static float Click(float t, float length)
        {
            return Mathf.Sin(2f * Mathf.PI * 2500f * t) * Mathf.Exp(-t * 120f) * 0.5f;
        }

        /// <summary>수류탄 폭발: 짧고 강한 잡음 + 저음 쿵.</summary>
        private static float Blast(float t, float length)
        {
            float noise = Random.value * 2f - 1f;
            float thump = Mathf.Sin(2f * Mathf.PI * 70f * t);
            return (noise * 0.8f * Mathf.Exp(-t * 9f)) + (thump * 0.6f * Mathf.Exp(-t * 5f));
        }

        /// <summary>구울 숨소리: 들이쉬고 내쉬는 거친 잡음 위에 낮게 떨리는 으르렁. (반복해도 이음매가 없도록 처음·끝이 0)</summary>
        private static float GhoulBreath(float t, float length)
        {
            float phase = t / length;
            // 앞 40%는 들숨(작게), 뒤 60%는 날숨(크게)
            float envelope = phase < 0.4f ? Mathf.Sin(Mathf.PI * phase / 0.4f) * 0.45f : Mathf.Sin(Mathf.PI * (phase - 0.4f) / 0.6f);
            float rasp = (Random.value * 2f - 1f) * 0.5f;
            float growlFrequency = 62f + Mathf.Sin(2f * Mathf.PI * 3f * t) * 6f;
            float growl = 2f * (t * growlFrequency - Mathf.Floor(t * growlFrequency + 0.5f));
            float flutter = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 23f * t);
            return (rasp + growl * 0.55f * flutter) * envelope * 0.6f;
        }

        /// <summary>구울 발소리: 손발이 바닥을 짚는 짧은 쿵 + 손톱 긁힘.</summary>
        private static float GhoulStep(float t, float length)
        {
            float thud = Mathf.Sin(2f * Mathf.PI * 85f * t) * Mathf.Exp(-t * 30f);
            float scratch = (Random.value * 2f - 1f) * Mathf.Exp(-t * 45f) * 0.5f;
            return (thud + scratch) * 0.8f;
        }

        /// <summary>구울 비명: 어긋난 톱니파 여러 개가 높아지며 떨리는 소리 + 잡음.</summary>
        private static float GhoulShriek(float t, float length)
        {
            float phase = t / length;
            float baseFrequency = Mathf.Lerp(380f, 920f, Mathf.Sqrt(phase));
            float sum = 0f;
            foreach (float detune in new[] { 1f, 1.013f, 0.981f, 1.5f })
            {
                float frequency = baseFrequency * detune;
                sum += 2f * (t * frequency - Mathf.Floor(t * frequency + 0.5f));
            }
            float tremolo = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * 17f * t);
            float noise = (Random.value * 2f - 1f) * 0.3f;
            float envelope = Mathf.Min(1f, t / 0.05f) * (1f - phase) * (1f - phase * 0.3f);
            return (sum * 0.22f * tremolo + noise) * envelope;
        }

        /// <summary>심장 소리: "쿵-쿵" 두 번의 낮은 박동. (반복 재생, 빠르기는 재생 속도로 조절)</summary>
        private static float Heartbeat(float t, float length)
        {
            float first = Mathf.Sin(2f * Mathf.PI * 52f * t) * Mathf.Exp(-t * 22f);
            float second = t > 0.27f ? Mathf.Sin(2f * Mathf.PI * 46f * (t - 0.27f)) * Mathf.Exp(-(t - 0.27f) * 26f) * 0.7f : 0f;
            return (first + second) * 0.9f;
        }

        /// <summary>전원 장치 파괴: 저음 폭발 + 잡음.</summary>
        private static float Boom(float t, float length)
        {
            float low = Mathf.Sin(2f * Mathf.PI * 55f * t);
            float noise = Random.value * 2f - 1f;
            return (low * 0.7f + noise * 0.4f) * Mathf.Exp(-t * 3.5f);
        }
    }
}
