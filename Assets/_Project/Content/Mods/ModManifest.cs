using System;
using System.Collections.Generic;

namespace GuardVsMannequin.Mods
{
    /// <summary>모드 정보 파일(mod.json)로, JsonUtility로 읽으므로 필드 이름이 곧 JSON 키다.</summary>
    [Serializable]
    public class ModManifest
    {
        /// <summary>전역 고유 ID(영문 소문자·숫자·점·밑줄·하이픈).</summary>
        public string id;

        public string name;

        /// <summary>같은 모드라도 버전이 다르면 함께 플레이할 수 없다.</summary>
        public string version;

        public string author;

        public string description;

        /// <summary>모드가 없는 플레이어에게 설치 안내로 보여 줄 주소.</summary>
        public string url;

        /// <summary>필요한 최소 게임 버전(비우면 확인하지 않음).</summary>
        public string gameVersion;

        /// <summary>번들을 만든 Unity 버전으로, 게임과 맞지 않으면 번들을 읽을 수 없다.</summary>
        public string unityVersion;

        /// <summary>먼저 켜져 있어야 하는 다른 모드 ID 목록.</summary>
        public List<string> dependencies = new();

        /// <summary>에셋 번들 파일 이름(없으면 비움).</summary>
        public string assetBundle;

        /// <summary>씬 번들 파일 이름(맵이 없으면 비움).</summary>
        public string sceneBundle;

        /// <summary>같은 모드 파일인지 비교하는 번들 내용 지문.</summary>
        public string contentHash;

        /// <summary>이 모드가 더하는 맵으로, 맵 ID는 "&lt;모드 ID&gt;."로 시작해야 한다.</summary>
        public List<MapManifest> maps = new();
    }
}
