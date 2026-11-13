using GuardVsMannequin.Mods;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace GuardVsMannequin.Room
{
    /// <summary>룸·게임 시스템·맵 씬 이름을 다루고 이 기기가 목표 씬에 도착했는지 판단한다.</summary>
    public sealed class RoomSceneSet
    {
        private readonly string roomSceneName;
        private readonly string systemsSceneName;
        private readonly IMapCatalog catalog;

        /// <summary>테스트에서 가짜 맵 목록을 넣을 수 있도록 씬 이름과 목록을 밖에서 받는다.</summary>
        public RoomSceneSet(string roomSceneName, string systemsSceneName, IMapCatalog catalog)
        {
            this.roomSceneName = roomSceneName;
            this.systemsSceneName = systemsSceneName;
            this.catalog = catalog;
        }

        public string RoomSceneName => roomSceneName;

        /// <summary>빌드 설정을 갱신하지 않아도 판이 열리도록 범위 밖이거나 빌드에 없는 내장 맵은 첫 번째 맵으로 대신한다.</summary>
        public string ResolveGameScene(int mapId)
        {
            var maps = catalog.Maps;
            MapEntry map = maps[Mathf.Clamp(mapId, 0, maps.Count - 1)];
            if (!map.IsBuiltIn || Application.CanStreamedLevelBeLoaded(map.SceneName))
                return map.SceneName;

            Debug.LogWarning($"[RoomState] 맵 씬 '{map.SceneName}'이 빌드 설정에 없어 '{maps[0].SceneName}'을 사용합니다. 'GameSettingTools/맵 생성'을 실행하세요.");
            return maps[0].SceneName;
        }

        /// <summary>전환 중에는 이전·다음 씬이 함께 열려 있을 수 있어 목표 씬만 남아야 도착으로 본다.</summary>
        public bool IsTransitionPending(bool isInGame)
        {
            bool isRoomLoaded = UnitySceneManager.GetSceneByName(roomSceneName).isLoaded;
            bool isMapLoaded = IsAnyMapSceneLoaded();
            bool isSystemsLoaded = UnitySceneManager.GetSceneByName(systemsSceneName).isLoaded;
            return isInGame ? (!(isMapLoaded && isSystemsLoaded) || isRoomLoaded) : (isMapLoaded || isSystemsLoaded || !isRoomLoaded);
        }

        /// <summary>
        /// 어느 맵이 열려 있는지는 상관없이 맵 씬이 열렸는지만 알면 되어서 목록 전체를 본다.
        /// </summary>
        private bool IsAnyMapSceneLoaded()
        {
            foreach (MapEntry map in catalog.Maps)
            {
                if (UnitySceneManager.GetSceneByName(map.SceneName).isLoaded)
                    return true;
            }
            return false;
        }
    }
}
