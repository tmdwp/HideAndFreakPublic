using System;
using System.Collections.Generic;

namespace GuardVsMannequin.Mods
{
    /// <summary>
    /// 모드 정보 파일(Mods/&lt;모드 폴더&gt;/mod.json). SDK의 "모드 내보내기"가 만든다. JsonUtility로 읽으므로 필드 이름이 곧 JSON 키다.
    /// </summary>
    [Serializable]
    public class ModManifest
    {
        /// <summary>전역 고유 ID. 영문 소문자·숫자·점·밑줄·하이픈. 예: "author.mymaps"</summary>
        public string id;

        /// <summary>표시 이름.</summary>
        public string name;

        /// <summary>모드 버전. (같은 모드라도 버전이 다르면 함께 플레이할 수 없다)</summary>
        public string version;

        /// <summary>만든 사람.</summary>
        public string author;

        /// <summary>설명.</summary>
        public string description;

        /// <summary>모드를 받을 수 있는 주소. 없는 플레이어에게 설치 안내로 보여 준다. (<see cref="ModLinkPolicy"/>를 통과한 주소만 열 수 있다)</summary>
        public string url;

        /// <summary>필요한 최소 게임 버전(Player Settings Version, 예: "0.6.0"). 비우면 확인하지 않는다.</summary>
        public string gameVersion;

        /// <summary>번들을 만든 Unity 버전. 게임과 다르면 번들을 읽을 수 없어 오류로 처리한다.</summary>
        public string unityVersion;

        /// <summary>먼저 켜져 있어야 하는 다른 모드 ID 목록. (예: 다른 모드의 외형·사물을 쓰는 맵)</summary>
        public List<string> dependencies = new();

        /// <summary>에셋 번들 파일 이름(외형·사물 프리팹·머티리얼). 없으면 비운다.</summary>
        public string assetBundle;

        /// <summary>씬 번들 파일 이름(맵 씬). 맵이 없으면 비운다.</summary>
        public string sceneBundle;

        /// <summary>번들 내용 지문(내보내기 도구가 번들 파일로 계산). 모드 일치 확인에서 같은 모드 파일인지 비교한다.</summary>
        public string contentHash;

        /// <summary>이 모드가 더하는 맵. 맵 ID는 "&lt;모드 ID&gt;."로 시작해야 한다.</summary>
        public List<MapManifest> maps = new();
    }
}
