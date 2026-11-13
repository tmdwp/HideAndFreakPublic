using System.Linq;

namespace GuardVsMannequin.Editor
{
    /// <summary>맵 목록을 따로 관리하지 않도록 Scenes/Custom 폴더의 씬을 파일 이름 순으로 맵 목록으로 쓴다.</summary>
    public static class MapCatalog
    {
        /// <summary>
        /// 맵 하나의 정보.
        /// </summary>
        public sealed class Entry
        {
            /// <summary>빌드 설정·RoomState에 등록되는 이름.</summary>
            public string sceneName;

            /// <summary>새로 등록할 때의 맵 이름. 로그에도 쓴다.</summary>
            public string displayName;

            public string ScenePath => $"{CustomMapFolder}/{sceneName}.unity";
        }

        /// <summary>이 폴더의 씬이 자동으로 맵 목록이 된다.</summary>
        public static string CustomMapFolder => $"{TestEnvironmentSetupTool.SceneFolder}/Custom";

        /// <summary>목록 파일은 빠뜨리기 쉬워 폴더를 직접 읽고, 순서가 맵 ID이므로 PC마다 같도록 파일 이름 순으로 정렬한다.</summary>
        public static Entry[] AllMaps
        {
            get
            {
                if (!System.IO.Directory.Exists(CustomMapFolder))
                    return new Entry[0];

                return System.IO.Directory.GetFiles(CustomMapFolder, "*.unity")
                    .OrderBy(scenePath => scenePath)
                    .Select(System.IO.Path.GetFileNameWithoutExtension)
                    .Select(sceneName => new Entry { sceneName = sceneName, displayName = sceneName })
                    .ToArray();
            }
        }

        /// <summary>RoomState에 맵 ID 순서대로 등록하려고 씬 이름만 뽑는다.</summary>
        public static string[] SceneNames => AllMaps.Select(map => map.sceneName).ToArray();

        /// <summary>열린 씬이 맵인지 판단하는 도구가 있어 경로로 찾는다.</summary>
        public static Entry FindByScenePath(string scenePath)
        {
            return AllMaps.FirstOrDefault(map => map.ScenePath == scenePath);
        }
    }
}
