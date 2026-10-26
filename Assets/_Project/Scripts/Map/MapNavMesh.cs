using Unity.AI.Navigation;
using UnityEngine;

namespace GuardVsMannequin.Map
{
    /// <summary>
    /// 지금 맵(<see cref="MapInfo.Current"/>)의 NavMesh를 판 시작 때 쓸 수 있게 준비한다. (서버, 가드 스폰 후보)
    /// 에디터에서 미리 구운 NavMesh가 있으면 그대로 쓴다. 빌드에서는 읽기가 허용되지 않은 메시(에셋 팩 모델 대부분)를
    /// 실행 중에 구울 수 없어 NavMesh에 구멍이 생기므로, 미리 굽지 않은 맵(일부 모드 맵)에서만 실행 중에 굽는다.
    /// </summary>
    public static class MapNavMesh
    {
        // 이미 확인한 맵. 맵 씬이 바뀌면 다시 확인한다.
        private static MapInfo preparedMap;

        /// <summary>
        /// 지금 맵의 NavMesh가 없으면 한 번 굽는다. 맵에 NavMeshSurface가 없으면 아무것도 하지 않는다.
        /// </summary>
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
