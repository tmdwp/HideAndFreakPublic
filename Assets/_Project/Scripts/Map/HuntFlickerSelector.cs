using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GuardVsMannequin.Map
{
    /// <summary>보이는 밝은 곳이 PC마다 달라지지 않도록 조명 위치로 사냥 시간에 깜빡이며 남을 조명을 고른다.</summary>
    public static class HuntFlickerSelector
    {
        // 부동소수 오차로 PC마다 해시가 달라지지 않도록 반올림하는 단위(cm).
        private const float PositionPrecision = 100f;

        /// <summary>목록을 보내지 않아도 모든 PC가 같은 조명을 고르도록 위치 해시로 정렬해 비율만큼 자른다.</summary>
        public static HashSet<MapLight> Choose(IReadOnlyList<MapLight> mapLights, float ratio)
        {
            List<MapLight> validLights = mapLights.Where(mapLight => mapLight != null).ToList();
            int count = Mathf.RoundToInt(Mathf.Clamp01(ratio) * validLights.Count);
            return validLights
                .OrderBy(mapLight => PositionHash(mapLight.transform.position))
                .ThenBy(mapLight => mapLight.transform.position.x)
                .ThenBy(mapLight => mapLight.transform.position.z)
                .Take(count)
                .ToHashSet();
        }

        /// <summary>
        /// string.GetHashCode는 실행마다 달라질 수 있어서, PC·실행과 관계없이 같은 FNV-1a로 cm 단위 정수 위치를 섞는다.
        /// </summary>
        private static uint PositionHash(Vector3 position)
        {
            uint hash = 2166136261u;
            foreach (float axis in new[] { position.x, position.y, position.z })
            {
                int roundedValue = Mathf.RoundToInt(axis * PositionPrecision);
                for (int shift = 0; shift < 32; shift += 8)
                {
                    hash ^= (uint)(roundedValue >> shift) & 0xFFu;
                    hash *= 16777619u;
                }
            }
            return hash;
        }
    }
}
