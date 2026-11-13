using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Audio
{
    /// <summary>소음 종류별 소리와 전달 거리 설정으로, 클립이 없으면 임시 합성음을 쓴다.</summary>
    [CreateAssetMenu(fileName = "NoiseLibrary", menuName = "GameSettingTools/Noise Library")]
    public class NoiseLibrary : ScriptableObject
    {
        /// <summary>
        /// 소음 한 종류의 설정.
        /// </summary>
        [Serializable]
        public class Entry
        {
            public NoiseType type;

            [Tooltip("비어 있으면 임시 합성음을 사용한다.")]
            public AudioClip clip;

            [Range(0f, 1f)] public float volume = 1f;

            [Tooltip("이 거리 밖에서는 들리지 않는다.")]
            public float maxDistance = 25f;
        }

        [SerializeField] private List<Entry> entries = new();

        /// <summary>
        /// 새 소음 종류를 추가한 뒤 에셋을 아직 갱신하지 않았어도 동작하도록, 없으면 기본값을 만들어 준다.
        /// </summary>
        public Entry Get(NoiseType type)
        {
            foreach (Entry entry in entries)
            {
                if (entry.type == type)
                    return entry;
            }
            return new Entry { type = type };
        }

        /// <summary>
        /// 사운드 에셋이 준비되기 전에도 소리가 나도록, 클립이 없으면 임시 합성음을 준다.
        /// </summary>
        public AudioClip GetClip(NoiseType type)
        {
            AudioClip clip = Get(type).clip;
            return clip != null ? clip : ProceduralNoiseClips.Get(type);
        }

        /// <summary>지정한 클립이 덮어써지지 않도록 설정이 없는 종류만 기본값으로 추가한다.</summary>
        /// <returns>에셋을 저장해야 하는지 알 수 있도록 추가된 항목이 있는지 알려 준다.</returns>
        public bool AddMissingDefaults()
        {
            bool hasAdded = false;
            foreach (Entry defaultEntry in CreateDefaults())
            {
                if (entries.Exists(entry => entry.type == defaultEntry.type))
                    continue;

                entries.Add(defaultEntry);
                hasAdded = true;
            }
            return hasAdded;
        }

        /// <summary>
        /// 새 종류를 추가할 때 크기·거리를 한곳에서 정하도록 기본 설정을 모아 둔다.
        /// </summary>
        private static List<Entry> CreateDefaults()
        {
            return new List<Entry>
            {
                new Entry { type = NoiseType.GunShot, volume = 1f, maxDistance = 60f },
                new Entry { type = NoiseType.GeneratorDestroyed, volume = 1f, maxDistance = 70f },
                new Entry { type = NoiseType.PowerDown, volume = 1f, maxDistance = 500f },
                new Entry { type = NoiseType.MannequinAttack, volume = 0.9f, maxDistance = 15f },
                new Entry { type = NoiseType.FlashlightClick, volume = 0.5f, maxDistance = 8f },
                new Entry { type = NoiseType.GrenadeExplosion, volume = 1f, maxDistance = 40f },
                new Entry { type = NoiseType.GhoulBreath, volume = 0.7f, maxDistance = 14f },
                new Entry { type = NoiseType.GhoulStep, volume = 0.6f, maxDistance = 16f },
                new Entry { type = NoiseType.GhoulShriek, volume = 1f, maxDistance = 60f },
                new Entry { type = NoiseType.GuardHeartbeat, volume = 0.9f, maxDistance = 1f }
            };
        }
    }
}
