using System;
using System.Collections.Generic;

namespace GuardVsMannequin.Mods
{
    /// <summary>맵 하나의 정보(map.json)로, JsonUtility로 읽으므로 필드 이름이 곧 JSON 키다.</summary>
    [Serializable]
    public class MapManifest
    {
        /// <summary>전역 고유 ID(내장 맵은 "builtin.&lt;이름&gt;", 모드 맵은 "&lt;모드 ID&gt;.&lt;이름&gt;").</summary>
        public string id;

        /// <summary>불러올 씬 이름(모드 맵은 번들 안 씬 이름).</summary>
        public string scene;

        /// <summary>표시 이름 문장 key로, 번역이 없으면 <see cref="name"/>을 쓴다.</summary>
        public string nameKey;

        /// <summary>표시 이름 (번역이 없을 때).</summary>
        public string name;

        /// <summary>이 맵을 고를 수 있는 최소 인원(0이면 게임 기본값).</summary>
        public int minPlayers;

        /// <summary>이 맵을 고를 수 있는 최대 인원(0이면 게임 기본값).</summary>
        public int maxPlayers;

        /// <summary>맵별 룸 설정 기본값·범위로, 적지 않은 항목은 게임 기본값을 쓴다.</summary>
        public List<MapSettingRange> settings = new();
    }

    /// <summary>맵별 룸 설정 항목 하나의 기본값과 범위로, 범위는 게임 범위와 겹치는 구간만 쓰인다.</summary>
    [Serializable]
    public class MapSettingRange
    {
        /// <summary>설정 항목 이름(GameSettingId 이름).</summary>
        public string id;

        /// <summary>맵을 고르면 적용되는 기본값.</summary>
        public float value;

        public float min;

        public float max;
    }

    /// <summary>내장 맵 목록 파일(maps.json)의 형식으로, 순서가 곧 맵 목록 순서다.</summary>
    [Serializable]
    public class BuiltInMapList
    {
        public List<MapManifest> maps = new();
    }
}
