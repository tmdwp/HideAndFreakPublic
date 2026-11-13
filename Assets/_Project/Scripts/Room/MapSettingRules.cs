using System;
using GuardVsMannequin.Core;
using GuardVsMannequin.Mods;
using UnityEngine;

namespace GuardVsMannequin.Room
{
    /// <summary>맵마다 어울리는 설정이 달라 map.json의 기본값·범위·인원으로 서버 설정을 보정한다.</summary>
    public static class MapSettingRules
    {
        /// <summary>
        /// 맵 제작자가 대소문자를 다르게 적어도 찾도록 대소문자를 무시하고 항목을 찾는다.
        /// </summary>
        public static bool TryGetRange(int mapId, GameSettingId id, out MapSettingRange range)
        {
            range = null;
            if (!ContentRegistry.Instance.TryGetMap(mapId, out MapEntry map) || map.Manifest.settings == null)
                return false;

            string idName = id.ToString();
            foreach (MapSettingRange candidate in map.Manifest.settings)
            {
                if (candidate != null && string.Equals(candidate.id, idName, StringComparison.OrdinalIgnoreCase))
                {
                    range = candidate;
                    return true;
                }
            }
            return false;
        }

        /// <summary>맵마다 어울리는 시간·인원이 달라 맵을 바꾸면 그 맵의 기본값을 넣되, 맵 번호 자체는 건너뛴다.</summary>
        public static GameSettings ApplyDefaults(GameSettings settings)
        {
            settings = ClampMapId(settings);
            if (!ContentRegistry.Instance.TryGetMap(settings.mapId, out MapEntry map) || map.Manifest.settings == null)
                return settings;

            foreach (MapSettingRange range in map.Manifest.settings)
            {
                if (range != null && Enum.TryParse(range.id, true, out GameSettingId id) && id != GameSettingId.Map)
                    settings = GameSettingDefinitions.SetClamped(settings, id, range.value);
                else
                    Debug.LogWarning($"[MapSettingRules] 맵 '{map.Id}'의 설정 항목 '{range?.id}'을 알 수 없어 무시합니다.");
            }
            return settings;
        }

        /// <summary>
        /// 호스트가 고른 값이 맵 범위를 벗어나지 않도록, 지금 값을 그대로 다시 넣어 맵 범위로 보정한다.
        /// </summary>
        public static GameSettings Clamp(GameSettings settings)
        {
            settings = ClampMapId(settings);
            if (!ContentRegistry.Instance.TryGetMap(settings.mapId, out MapEntry map) || map.Manifest.settings == null)
                return settings;

            foreach (MapSettingRange range in map.Manifest.settings)
            {
                if (range != null && Enum.TryParse(range.id, true, out GameSettingId id) && id != GameSettingId.Map)
                    settings = GameSettingDefinitions.SetClamped(settings, id, GameSettingDefinitions.GetValue(settings, id));
            }
            return settings;
        }

        /// <summary>인원이 맵 크기에 맞지 않으면 판이 성립하지 않아 맵이 정한 인원 범위로 시작을 막는다.</summary>
        /// <param name="minPlayers">호스트에게 이유를 보여 주도록 적용된 최소 인원을 돌려준다.</param>
        /// <param name="maxPlayers">호스트에게 이유를 보여 주도록 적용된 최대 인원을 돌려준다.</param>
        public static bool AllowsPlayerCount(GameSettings settings, int playerCount, out int minPlayers, out int maxPlayers)
        {
            minPlayers = GameConstants.MinPlayers;
            maxPlayers = GameConstants.MaxPlayers;
            if (ContentRegistry.Instance.TryGetMap(settings.mapId, out MapEntry map))
            {
                if (map.Manifest.minPlayers > 0)
                    minPlayers = Mathf.Max(minPlayers, map.Manifest.minPlayers);
                if (map.Manifest.maxPlayers > 0)
                    maxPlayers = Mathf.Min(maxPlayers, map.Manifest.maxPlayers);
            }
            return playerCount >= minPlayers && playerCount <= maxPlayers;
        }

        /// <summary>
        /// 모드를 끄면 맵 수가 줄어 저장된 번호가 범위를 벗어날 수 있어서 0 ~ (맵 수 - 1)로 맞춘다.
        /// </summary>
        private static GameSettings ClampMapId(GameSettings settings)
        {
            settings.mapId = Mathf.Clamp(settings.mapId, 0, Mathf.Max(0, ContentRegistry.Instance.Maps.Count - 1));
            return settings;
        }
    }
}
