using System.Linq;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 게임에서 고를 수 있는 맵 목록. 순서가 곧 맵 ID(룸 설정의 맵 번호)이다.
    /// 맵은 Scenes/Custom 폴더에 직접 만든 씬이며(<see cref="MapMakerWindow"/>, 맵 빌더), 파일 이름 순으로 번호가 붙는다.
    /// 테스트 씬 Game_Test는 게임에 넣지 않는다. (모드 SDK 시험용 원본, 모드 맵과 이름이 겹치지 않게)
    /// 맵이 늘거나 줄면 "맵 목록 갱신"(<see cref="MapRegistrySync"/>)이 빌드 설정·룸 맵 목록·맵 이름 문구·설정 범위를 맞춘다.
    /// </summary>
    public static class MapCatalog
    {
        /// <summary>
        /// 맵 하나의 정보.
        /// </summary>
        public sealed class Entry
        {
            /// <summary>게임 씬 이름. (빌드 설정·RoomState에 등록되는 이름)</summary>
            public string sceneName;

            /// <summary>표시 이름. (새로 등록할 때의 맵 이름, 로그용)</summary>
            public string displayName;

            /// <summary>씬 경로.</summary>
            public string ScenePath => $"{CustomMapFolder}/{sceneName}.unity";
        }

        /// <summary>맵 씬을 두는 폴더. 이 폴더의 씬이 자동으로 맵 목록이 된다. (파일 이름 순)</summary>
        public static string CustomMapFolder => $"{TestEnvironmentSetupTool.SceneFolder}/Custom";

        /// <summary>
        /// 전체 맵 목록 (인덱스 = 맵 ID).
        /// 맵을 추가·삭제하면 그 뒤 맵들의 번호가 바뀌므로 "맵 목록 갱신"을 실행한다.
        /// </summary>
        public static Entry[] AllMaps
        {
            get
            {
                if (!System.IO.Directory.Exists(CustomMapFolder))
                    return new Entry[0];

                return System.IO.Directory.GetFiles(CustomMapFolder, "*.unity")
                    .OrderBy(p => p)
                    .Select(System.IO.Path.GetFileNameWithoutExtension)
                    .Select(sceneName => new Entry { sceneName = sceneName, displayName = sceneName })
                    .ToArray();
            }
        }

        /// <summary>맵 ID 순서의 게임 씬 이름. (RoomState에 등록)</summary>
        public static string[] SceneNames => AllMaps.Select(map => map.sceneName).ToArray();

        /// <summary>
        /// 씬 경로로 맵 항목을 찾는다. 없으면 null.
        /// </summary>
        public static Entry FindByScenePath(string scenePath)
        {
            return AllMaps.FirstOrDefault(map => map.ScenePath == scenePath);
        }
    }
}
