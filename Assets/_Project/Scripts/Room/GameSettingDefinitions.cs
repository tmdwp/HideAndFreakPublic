using System;
using System.Collections.Generic;
using System.Globalization;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Mods;
using UnityEngine;

namespace GuardVsMannequin.Room
{
    /// <summary>룸에서 조정할 수 있는 게임 설정 항목.</summary>
    public enum GameSettingId : byte
    {
        Map,
        // 룸 UI의 기존 줄을 그대로 쓰도록 예전 항목 번호를 비워 둔다.
        HuntDelay,
        PlayTime,
        HuntTime,
        GuardCount,
        ObjectiveCount,
        GeneratorCount,
        GeneratorDestroyTime,
        GunCooldown,
        // 9번은 예전 DetectorCooldown(탐지기) 자리.
        GrenadeCooldown,
        GuardMaxHealth,
        MannequinMaxHealth,
        HuntHealthMultiplier,
        MannequinAttackRange,
        GuardMoveSpeed,
        MannequinMoveSpeed,
        // UI 프리팹이 번호로 저장하므로 새 항목은 끝에 추가한다.
        GunDamage,
        MannequinAttackDamage,
        GunMagazineSize,
        GunReloadTime
    }

    /// <summary>빌드 후에도 조정할 수 있도록 설정 항목의 단위·범위·순서를 CSV에서 읽어 GameSettings 읽기/쓰기와 함께 정의한다.</summary>
    public static class GameSettingDefinitions
    {
        private const string TablePath = "Data/game_settings";

        /// <summary>
        /// 설정 항목 하나의 정의.
        /// </summary>
        public readonly struct Definition
        {
            public readonly GameSettingId id;
            public readonly float step;
            public readonly float min;
            public readonly float max;
            public readonly string unitKey;

            /// <summary>CSV 한 줄이나 기본값에서 같은 형태로 만들 수 있도록 값을 묶는다.</summary>
            public Definition(GameSettingId id, float step, float min, float max, string unitKey)
            {
                this.id = id;
                this.step = step;
                this.min = min;
                this.max = max;
                this.unitKey = unitKey;
            }

            /// <summary>언어를 바꾸면 바로 반영되도록 저장하지 않고 매번 문구를 찾는다.</summary>
            public string Label => Loc.Get("setting." + id);
        }

        /// <summary>CSV를 읽지 못했거나 새 항목이 CSV에 아직 없어도 룸 UI가 비지 않도록 둔 기본 정의.</summary>
        private static readonly Definition[] fallbackDefinitions =
        {
            new(GameSettingId.Map, 1f, 0f, 4f, string.Empty),
            new(GameSettingId.HuntDelay, 5f, 10f, 120f, "unit.seconds"),
            new(GameSettingId.PlayTime, 30f, GameSettingsValidator.MinPlayTime, GameSettingsValidator.MaxPlayTime, "unit.seconds"),
            new(GameSettingId.HuntTime, 15f, 30f, GameSettingsValidator.MaxHuntTime, "unit.seconds"),
            new(GameSettingId.GuardCount, 1f, 1f, GameConstants.MaxPlayers / 2, "unit.people"),
            new(GameSettingId.ObjectiveCount, 1f, 0f, GameConstants.MaxPlayers + 2, "unit.times"),
            new(GameSettingId.GeneratorCount, 1f, 1f, 12f, "unit.count"),
            new(GameSettingId.GeneratorDestroyTime, 1f, 3f, 30f, "unit.seconds"),
            new(GameSettingId.GunCooldown, 0.5f, 0.5f, 10f, "unit.seconds"),
            new(GameSettingId.GrenadeCooldown, 1f, 5f, 60f, "unit.seconds"),
            new(GameSettingId.GuardMaxHealth, 1f, 1f, 100f, string.Empty),
            new(GameSettingId.MannequinMaxHealth, 1f, 1f, 500f, string.Empty),
            new(GameSettingId.HuntHealthMultiplier, 1f, 1f, 100f, "unit.multiplier"),
            new(GameSettingId.MannequinAttackRange, 0.5f, 1f, 30f, "unit.meters"),
            new(GameSettingId.GuardMoveSpeed, 0.1f, GameSettingsValidator.MinMoveSpeed, GameSettingsValidator.MaxMoveSpeed, "unit.speed"),
            new(GameSettingId.MannequinMoveSpeed, 0.1f, GameSettingsValidator.MinMoveSpeed, GameSettingsValidator.MaxMoveSpeed, "unit.speed"),
            new(GameSettingId.GunDamage, 1f, 1f, 500f, string.Empty),
            new(GameSettingId.MannequinAttackDamage, 1f, 1f, 500f, string.Empty),
            new(GameSettingId.GunMagazineSize, 1f, 1f, 10f, "unit.rounds"),
            new(GameSettingId.GunReloadTime, 0.5f, 0.5f, 5f, "unit.seconds")
        };

        /// <summary>호스트가 바꾸면 균형이 쉽게 깨지는 항목이라 룸 UI에서 숨긴다.</summary>
        private static readonly HashSet<GameSettingId> hiddenInRoom = new()
        {
            GameSettingId.MannequinAttackRange,
            GameSettingId.GuardMoveSpeed,
            GameSettingId.MannequinMoveSpeed
        };

        private static List<Definition> definitions;

        /// <summary>
        /// 룸 UI와 서버 조정이 같은 기준으로 숨김 항목을 거르도록 한곳에서 판단한다.
        /// </summary>
        public static bool IsShownInRoom(GameSettingId id)
        {
            return !hiddenInRoom.Contains(id);
        }

        /// <summary>CSV를 매번 읽지 않도록 처음 쓸 때 한 번 읽어 표시 순서대로 둔다.</summary>
        public static IReadOnlyList<Definition> All
        {
            get
            {
                definitions ??= LoadDefinitions();
                return definitions;
            }
        }

        /// <summary>
        /// 에디터 도구가 CSV 수정 내용을 바로 반영할 수 있도록 다음 사용 때 다시 읽게 한다.
        /// </summary>
        public static void ClearCache()
        {
            definitions = null;
        }

        /// <summary>맵 수는 켜진 모드에 따라 바뀌므로 맵 항목만 지금 맵 목록 수로 범위를 만든다.</summary>
        public static Definition Get(GameSettingId id)
        {
            if (id == GameSettingId.Map)
                return new Definition(GameSettingId.Map, 1f, 0f, Mathf.Max(0, ContentRegistry.Instance.Maps.Count - 1), string.Empty);

            foreach (Definition definition in All)
            {
                if (definition.id == id)
                    return definition;
            }
            return All[0];
        }

        /// <summary>맵마다 어울리는 범위가 달라 게임 범위와 맵 범위가 겹치는 구간을 쓰고, 겹치지 않으면 게임 범위를 쓴다.</summary>
        public static Definition GetEffective(GameSettingId id, GameSettings settings)
        {
            Definition definition = Get(id);
            if (id == GameSettingId.Map || !MapSettingRules.TryGetRange(settings.mapId, id, out MapSettingRange range))
                return definition;

            float min = Mathf.Max(definition.min, range.min);
            float max = Mathf.Min(definition.max, range.max);
            return min <= max ? new Definition(id, definition.step, min, max, definition.unitKey) : definition;
        }

        /// <summary>
        /// UI가 항목마다 필드 이름을 알 필요 없도록 ID로 값을 읽는 창구를 둔다.
        /// </summary>
        public static float GetValue(GameSettings settings, GameSettingId id)
        {
            return id switch
            {
                GameSettingId.Map => settings.mapId,
                GameSettingId.HuntDelay => settings.huntDelay,
                GameSettingId.PlayTime => settings.playTime,
                GameSettingId.HuntTime => settings.huntTime,
                GameSettingId.GuardCount => settings.guardCount,
                GameSettingId.ObjectiveCount => settings.objectiveCount,
                GameSettingId.GeneratorCount => settings.generatorCount,
                GameSettingId.GeneratorDestroyTime => settings.generatorDestroyTime,
                GameSettingId.GunCooldown => settings.gunCooldown,
                GameSettingId.GrenadeCooldown => settings.grenadeCooldown,
                GameSettingId.GuardMaxHealth => settings.guardMaxHealth,
                GameSettingId.MannequinMaxHealth => settings.mannequinMaxHealth,
                GameSettingId.HuntHealthMultiplier => settings.huntHealthMultiplier,
                GameSettingId.MannequinAttackRange => settings.mannequinAttackRange,
                GameSettingId.GuardMoveSpeed => settings.guardMoveSpeed,
                GameSettingId.MannequinMoveSpeed => settings.mannequinMoveSpeed,
                GameSettingId.GunDamage => settings.gunDamage,
                GameSettingId.MannequinAttackDamage => settings.mannequinAttackDamage,
                GameSettingId.GunMagazineSize => settings.gunMagazineSize,
                GameSettingId.GunReloadTime => settings.gunReloadTime,
                _ => 0f
            };
        }

        /// <summary>
        /// 화살표 버튼 한 번에 정해진 단위만큼 움직이고, 맵 범위를 넘지 않도록 자른다.
        /// </summary>
        public static GameSettings Adjust(GameSettings settings, GameSettingId id, int direction)
        {
            Definition definition = GetEffective(id, settings);
            float value = GetValue(settings, id) + definition.step * Mathf.Sign(direction);
            // 0.1 단위 조정에서 생기는 부동소수 오차 제거
            value = (float)Math.Round(value, 2);
            return SetValue(settings, id, Mathf.Clamp(value, definition.min, definition.max));
        }

        /// <summary>키보드로 아무 값이나 입력할 수 있어 범위로 자르고 소수 둘째 자리까지만 남긴다.</summary>
        public static GameSettings SetClamped(GameSettings settings, GameSettingId id, float value)
        {
            Definition definition = GetEffective(id, settings);
            value = (float)Math.Round(value, 2);
            return SetValue(settings, id, Mathf.Clamp(value, definition.min, definition.max));
        }

        /// <summary>
        /// 입력 칸은 다시 읽혀야 하므로 단위 없이, 지역 설정과 관계없이 소수점 '.'로 숫자만 보여 준다.
        /// </summary>
        public static string FormatNumber(GameSettings settings, GameSettingId id)
        {
            float value = GetValue(settings, id);
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 쉼표 소수점을 쓰는 지역의 사용자도 그대로 입력할 수 있도록 쉼표를 '.'로 바꿔 읽는다.
        /// </summary>
        public static bool TryParseInput(string text, out float value)
        {
            return float.TryParse((text ?? string.Empty).Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>게스트마다 맵 목록이 달라 맵은 방 상태의 이름을 쓰고, 나머지는 단위 문구를 붙인다.</summary>
        public static string Format(GameSettings settings, GameSettingId id)
        {
            float value = GetValue(settings, id);
            if (id == GameSettingId.Map)
                return RoomState.Instance != null ? RoomState.Instance.SelectedMapDisplayName : "?";

            Definition definition = Get(id);
            string number = Mathf.Approximately(value, Mathf.Round(value)) ? Mathf.RoundToInt(value).ToString() : value.ToString("0.0");
            return string.IsNullOrEmpty(definition.unitKey) ? number : Loc.Format(definition.unitKey, number);
        }

        /// <summary>정수 필드에 소수가 들어가지 않도록 정수 항목은 반올림해서 쓴다.</summary>
        private static GameSettings SetValue(GameSettings settings, GameSettingId id, float value)
        {
            int rounded = Mathf.RoundToInt(value);
            switch (id)
            {
                case GameSettingId.Map: settings.mapId = rounded; break;
                case GameSettingId.HuntDelay: settings.huntDelay = value; break;
                case GameSettingId.PlayTime: settings.playTime = value; break;
                case GameSettingId.HuntTime: settings.huntTime = value; break;
                case GameSettingId.GuardCount: settings.guardCount = rounded; break;
                case GameSettingId.ObjectiveCount: settings.objectiveCount = rounded; break;
                case GameSettingId.GeneratorCount: settings.generatorCount = rounded; break;
                case GameSettingId.GeneratorDestroyTime: settings.generatorDestroyTime = value; break;
                case GameSettingId.GunCooldown: settings.gunCooldown = value; break;
                case GameSettingId.GrenadeCooldown: settings.grenadeCooldown = value; break;
                case GameSettingId.GuardMaxHealth: settings.guardMaxHealth = rounded; break;
                case GameSettingId.MannequinMaxHealth: settings.mannequinMaxHealth = rounded; break;
                case GameSettingId.HuntHealthMultiplier: settings.huntHealthMultiplier = value; break;
                case GameSettingId.MannequinAttackRange: settings.mannequinAttackRange = value; break;
                case GameSettingId.GuardMoveSpeed: settings.guardMoveSpeed = value; break;
                case GameSettingId.MannequinMoveSpeed: settings.mannequinMoveSpeed = value; break;
                case GameSettingId.GunDamage: settings.gunDamage = rounded; break;
                case GameSettingId.MannequinAttackDamage: settings.mannequinAttackDamage = rounded; break;
                case GameSettingId.GunMagazineSize: settings.gunMagazineSize = rounded; break;
                case GameSettingId.GunReloadTime: settings.gunReloadTime = value; break;
            }
            return settings;
        }

        /// <summary>외부 CSV를 먼저 읽고, 빠진 항목이 UI에서 사라지지 않도록 기본 정의를 뒤에 붙인다.</summary>
        private static List<Definition> LoadDefinitions()
        {
            if (!DataFiles.TryLoad<List<Definition>>(TablePath, ".csv", TryParseTable, out List<Definition> result))
            {
                Debug.LogWarning($"[GameSettingDefinitions] {TablePath}.csv를 사용할 수 없어 기본 정의를 사용합니다.");
                result = new List<Definition>();
            }

            foreach (Definition fallback in fallbackDefinitions)
            {
                if (!Contains(result, fallback.id))
                    result.Add(fallback);
            }
            return result;
        }

        /// <summary>
        /// 한 줄이 잘못돼도 나머지는 쓸 수 있도록 읽을 수 없는 줄만 건너뛰고, 하나도 못 읽었으면 깨진 파일로 보고 내장 원본으로 물러나게 한다.
        /// </summary>
        private static bool TryParseTable(string csv, out List<Definition> result)
        {
            result = new List<Definition>();
            List<string[]> rows = CsvParser.Parse(csv);
            // 0번 행은 머리글이다.
            for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                if (TryParseRow(rows[rowIndex], out Definition definition) && !Contains(result, definition.id))
                    result.Add(definition);
                else
                    Debug.LogWarning($"[GameSettingDefinitions] game_settings.csv {rowIndex + 1}번째 데이터 줄을 읽을 수 없어 건너뜁니다: {string.Join(",", rows[rowIndex])}");
            }
            return result.Count > 0;
        }

        /// <summary>단위가 0이거나 범위가 뒤집히면 값을 정할 수 없어 그런 줄은 거부한다.</summary>
        private static bool TryParseRow(string[] row, out Definition definition)
        {
            definition = default;
            if (row.Length < 4 || !Enum.TryParse(row[0], out GameSettingId id))
                return false;
            if (!TryParseFloat(row[1], out float step) || !TryParseFloat(row[2], out float min) || !TryParseFloat(row[3], out float max))
                return false;
            if (step <= 0f || max < min)
                return false;

            string unitKey = row.Length > 4 ? row[4] : string.Empty;
            definition = new Definition(id, step, min, max, unitKey);
            return true;
        }

        /// <summary>
        /// 데이터 파일은 '.' 소수점으로 적으므로, 쉼표 소수점 지역에서도 같은 값으로 읽히도록 InvariantCulture를 쓴다.
        /// </summary>
        private static bool TryParseFloat(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// 같은 항목이 두 번 들어가면 UI에 줄이 중복되므로 넣기 전에 확인한다.
        /// </summary>
        private static bool Contains(List<Definition> list, GameSettingId id)
        {
            foreach (Definition item in list)
            {
                if (item.id == id)
                    return true;
            }
            return false;
        }
    }
}
