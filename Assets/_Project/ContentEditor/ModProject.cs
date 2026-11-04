using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 모드 하나의 설정(에디터 전용 에셋). "모드 내보내기"가 이 설정으로 번들과 mod.json을 만든다.
    /// 만들기: Project 창 우클릭 > Create > Hide&amp;Freak > Mod Project
    /// </summary>
    [CreateAssetMenu(menuName = EditorMenus.Root + "Mod Project", fileName = "ModProject")]
    public class ModProject : ScriptableObject
    {
        /// <summary>맵 하나의 내보내기 설정.</summary>
        [Serializable]
        public class MapSetting
        {
            [Tooltip("맵 씬. 씬 안에 MapInfo가 하나 있어야 한다.")]
            public SceneAsset scene;

            [Tooltip("맵 ID의 뒷부분(영문 소문자·숫자·밑줄). 전체 ID는 '<모드 ID>.<이것>'.")]
            public string mapKey = "map";

            [Tooltip("표시 이름. 번역은 strings.csv에 'mod.<모드 ID>.<mapKey>' 줄로 넣는다.")]
            public string displayName = "My Map";

            [Tooltip("이 맵을 고를 수 있는 최소·최대 인원. 0이면 게임 기본값(2~10명).")]
            public int minPlayers;
            public int maxPlayers;

            [Tooltip("맵별 룸 설정 기본값·범위. id는 GuardCount, PlayTime, MannequinMaxHealth 같은 설정 이름.")]
            public List<RangeSetting> settings = new();
        }

        /// <summary>맵별 룸 설정 항목 하나.</summary>
        [Serializable]
        public class RangeSetting
        {
            public string id = "GuardCount";
            public float value = 1f;
            public float min = 1f;
            public float max = 2f;
        }

        [Header("모드 정보")]
        [Tooltip("전역 고유 ID. 영문 소문자·숫자·. _ -  예: myname.spookymaps")]
        public string modId = "myname.mymod";
        public string modName = "My Mod";
        [Tooltip("모드 버전. 바꾸면 이전 버전을 가진 사람과 함께 플레이할 수 없다.")]
        public string version = "1.0.0";
        public string author = "";
        [TextArea] public string description = "";

        [Tooltip("모드를 받을 수 있는 주소. 모드가 없는 플레이어에게 안내한다. https://github.com/... 또는 Steam 창작마당 주소만 열 수 있다. (비워도 된다)")]
        public string downloadUrl = "";

        [Tooltip("필요한 최소 게임 버전(로비 왼쪽 아래 v 뒤의 앞부분, 예: 0.6.0).")]
        public string minGameVersion = "0.6.0";

        [Tooltip("필요한 다른 모드 ID. (그 모드의 외형·사물 프리팹을 쓸 때)")]
        public List<string> dependencies = new();

        [Header("콘텐츠")]
        [Tooltip("맵 씬 목록.")]
        public List<MapSetting> maps = new();

        [Tooltip("에셋 번들에 넣을 것(캐릭터 외형 CharacterSkin, 사물 프리팹 등). 맵 씬이 쓰는 것은 자동으로 씬 번들에 들어가므로, 다른 모드와 함께 쓰게 할 것만 넣는다.")]
        public List<UnityEngine.Object> sharedAssets = new();

        [Tooltip("번역 파일(strings.csv와 같은 형식). 키는 'mod.<모드 ID>.'로 시작해야 한다. (선택)")]
        public TextAsset strings;

        [Header("내보내기")]
        [Tooltip("내보낸 모드를 복사할 게임의 Mods 폴더. (비우면 복사하지 않음, 예: C:/Games/HideAndFreak/Mods)")]
        public string copyToModsFolder = "";
    }
}
