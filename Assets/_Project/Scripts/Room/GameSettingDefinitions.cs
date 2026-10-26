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
        // 1번은 예전 PreparationTime(가드 대기 시간) 자리. 번호를 유지해 룸 UI의 기존 줄을 그대로 쓴다.
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
        // 새 항목은 끝에 추가한다. (UI 프리팹이 번호로 저장하므로 중간에 넣으면 기존 줄이 어긋남) 표시 순서는 CSV가 정한다.
        GunDamage,
        MannequinAttackDamage,
        GunMagazineSize,
        GunReloadTime
    }

    /// <summary>
    /// 설정 항목의 조정 단위, 범위, 표시 순서와 GameSettings 읽기/쓰기를 정의한다.
    /// 조정 단위·범위·순서는 Resources/Data/game_settings.csv에서, 이름·단위 문구는 strings.csv에서 읽는다.
    /// 룸 UI(표시)와 RoomState(서버 조정)가 같은 정의를 사용한다.
    /// 인원에 따라 달라지는 최종 범위는 <see cref="GameSettingsValidator"/>가 다시 보정한다.
    /// </summary>
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

            /// <summary>정의를 만든다.</summary>
            public Definition(GameSettingId id, float step, float min, float max, string unitKey)
            {
                this.id = id;
                this.step = step;
                this.min = min;
                this.max = max;
                this.unitKey = unitKey;
            }

            /// <summary>현재 언어의 항목 이름.</summary>
            public string Label => Loc.Get("setting." + id);
        }

        /// <summary>CSV를 읽지 못했거나 빠진 항목에 쓰는 기본 정의.</summary>
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
            new(GameSettingId.GunReloadTime, 0.5f, 1f, 15f, "unit.seconds")
        };

        /// <summary>
        /// 룸 UI에 보이지 않고 호스트가 바꿀 수 없는 항목. 값은 default_game_settings.json(과 맵 기본값)으로만 정해진다.
        /// </summary>
        private static readonly HashSet<GameSettingId> hiddenInRoom = new()
        {
            GameSettingId.MannequinAttackRange,
            GameSettingId.GuardMoveSpeed,
            GameSettingId.MannequinMoveSpeed
        };

        private static List<Definition> definitions;

        /// <summary>
        /// 룸 UI에 표시하고 호스트가 조정할 수 있는 항목인지 여부.
        /// </summary>
        public static bool IsShownInRoom(GameSettingId id)
        {
            return !hiddenInRoom.Contains(id);
        }

        /// <summary>표시 순서대로 나열한 모든 설정 항목. (처음 사용할 때 CSV를 읽음)</summary>
        public static IReadOnlyList<Definition> All
        {
            get
            {
                definitions ??= LoadDefinitions();
                return definitions;
            }
        }

        /// <summary>
        /// 다음 사용 때 CSV를 다시 읽도록 캐시를 비운다. (에디터 도구가 CSV 수정 내용을 바로 반영할 때 사용)
        /// </summary>
        public static void ClearCache()
        {
            definitions = null;
        }

        /// <summary>
        /// 항목 정의를 찾는다. 맵 항목의 범위는 맵 목록(내장 맵 + 켜진 모드) 수에 맞춘다.
        /// </summary>
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

        /// <summary>
        /// 지금 고른 맵에서 쓰는 항목 정의. 게임 범위와 맵의 범위(map.json)가 겹치는 구간이다.
        /// 맵이 범위를 정하지 않았거나 두 범위가 겹치지 않으면 게임 범위를 쓴다.
        /// </summary>
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
        /// 설정에서 항목 값을 읽는다.
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
        /// 항목 값을 한 단계 올리거나 내린 설정을 반환한다. (항목 범위로 제한)
        /// </summary>
        public static GameSettings Adjust(GameSettings settings, GameSettingId id, int direction)
        {
            Definition definition = GetEffective(id, settings);
            float value = GetValue(settings, id) + definition.step * Mathf.Sign(direction);
            // 0.1 단위 조정에서 생기는 부동소수 오차 제거
            value = (float)Math.Round(value, 2);
            return SetValue(settings, id, Mathf.Clamp(value, definition.min, definition.max));
        }

        /// <summary>
        /// 항목 값을 직접 지정한 설정을 반환한다. (항목 범위로 제한, 정수 항목은 반올림, 소수는 둘째 자리까지)
        /// 키보드로 값을 입력할 때 사용한다.
        /// </summary>
        public static GameSettings SetClamped(GameSettings settings, GameSettingId id, float value)
        {
            Definition definition = GetEffective(id, settings);
            value = (float)Math.Round(value, 2);
            return SetValue(settings, id, Mathf.Clamp(value, definition.min, definition.max));
        }

        /// <summary>
        /// 입력 칸에 보여줄 숫자만의 문자열. (단위 없이, 소수점은 항상 '.')
        /// </summary>
        public static string FormatNumber(GameSettings settings, GameSettingId id)
        {
            float value = GetValue(settings, id);
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 입력한 문자열을 숫자로 읽는다. 쉼표 소수점도 받아준다.
        /// </summary>
        public static bool TryParseInput(string text, out float value)
        {
            return float.TryParse((text ?? string.Empty).Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// 화면에 표시할 값 문자열을 만든다. (단위 문구는 현재 언어)
        /// </summary>
        public static string Format(GameSettings settings, GameSettingId id)
        {
            float value = GetValue(settings, id);
            if (id == GameSettingId.Map)
                return ContentRegistry.Instance.TryGetMap(Mathf.RoundToInt(value), out MapEntry map) ? map.DisplayName : "?";

            Definition definition = Get(id);
            string number = Mathf.Approximately(value, Mathf.Round(value)) ? Mathf.RoundToInt(value).ToString() : value.ToString("0.0");
            return string.IsNullOrEmpty(definition.unitKey) ? number : Loc.Format(definition.unitKey, number);
        }

        /// <summary>
        /// 설정에 항목 값을 쓴 결과를 반환한다. (정수 항목은 반올림)
        /// </summary>
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

        /// <summary>
        /// CSV에서 항목 정의를 읽는다. 읽을 수 없는 줄은 건너뛰고, 빠진 항목은 기본 정의를 뒤에 붙인다.
        /// </summary>
        private static List<Definition> LoadDefinitions()
        {
            // 빌드 폴더의 GameData/Data/game_settings.csv를 먼저 보고, 없거나 깨졌으면 내장 원본을 쓴다.
            if (!DataFiles.TryLoad<List<Definition>>(TablePath, ".csv", TryParseTable, out List<Definition> result))
            {
                Debug.LogWarning($"[GameSettingDefinitions] {TablePath}.csv를 사용할 수 없어 기본 정의를 사용합니다.");
                result = new List<Definition>();
            }

            // 파일에서 빠진 항목은 기본 정의를 뒤에 붙인다.
            foreach (Definition fallback in fallbackDefinitions)
            {
                if (!Contains(result, fallback.id))
                    result.Add(fallback);
            }
            return result;
        }

        /// <summary>
        /// CSV를 항목 정의 목록으로 읽는다. 읽을 수 없는 줄은 건너뛰고, 읽은 줄이 하나도 없으면 깨진 파일로 본다.
        /// </summary>
        private static bool TryParseTable(string csv, out List<Definition> result)
        {
            result = new List<Definition>();
            List<string[]> rows = CsvParser.Parse(csv);
            // 0번 행은 머리글(id,step,min,max,unit)
            for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                if (TryParseRow(rows[rowIndex], out Definition definition) && !Contains(result, definition.id))
                    result.Add(definition);
                else
                    Debug.LogWarning($"[GameSettingDefinitions] game_settings.csv {rowIndex + 1}번째 데이터 줄을 읽을 수 없어 건너뜁니다: {string.Join(",", rows[rowIndex])}");
            }
            return result.Count > 0;
        }

        /// <summary>
        /// CSV 한 줄을 정의로 바꾼다. (id, step, min, max, unit)
        /// </summary>
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
        /// 시스템 지역 설정과 관계없이 소수점(.)으로 숫자를 읽는다.
        /// </summary>
        private static bool TryParseFloat(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// 목록에 같은 항목이 이미 있는지 확인한다.
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
