using Unity.AI.Navigation;
using UnityEngine;

namespace GuardVsMannequin.Map
{
    /// <summary>가드 스폰 후보를 뽑을 수 있도록 판 시작 때 지금 맵의 NavMesh를 준비한다.</summary>
    public static class MapNavMesh
    {
        // 맵 씬이 바뀌면 다시 확인하려고 기억하는 맵.
        private static MapInfo preparedMap;

        /// <summary>굽기는 무거워서 맵마다 한 번만 확인하고 미리 구운 데이터는 건드리지 않는다.</summary>
        public static void EnsureBuilt()
        {
            MapInfo map = MapInfo.Current;
            if (map == null || map == preparedMap)
                return;
            preparedMap = map;

            NavMeshSurface surface = map.GetComponentInChildren<NavMeshSurface>();
            if (surface == null)
            {
                Debug.LogWarning("[MapNavMesh] 맵에 NavMeshSurface가 없어 가드 스폰 후보를 NavMesh에서 뽑을 수 없습니다.", map);
                return;
            }
            if (surface.navMeshData != null)
                return;

            surface.BuildNavMesh();
        }
    }
}
