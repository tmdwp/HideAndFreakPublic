using System.Collections.Generic;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Map
{
    /// <summary>
    /// 씬의 스폰 지점을 역할별로 모아 두고, 판마다 스폰 위치를 나눠 준다.
    /// 역할의 스폰 방식(<see cref="RoleDefinition.Placement"/>)에 따라
    /// 놓인 지점을 차례로 쓰거나(마네킹), 마네킹 스폰 지점에서 먼 무작위 위치를 고른다(가드, <see cref="GuardSpawnSelector"/>).
    /// </summary>
    public class SpawnPointRegistry : MonoBehaviour
    {
        private readonly Dictionary<PlayerRole, List<SpawnPoint>> pointsByRole = new();
        private readonly Dictionary<PlayerRole, int> nextIndexByRole = new();

        // 가드 무작위 위치 후보로 뽑을 NavMesh 지점 수
        // (갈 수 없는 곳·마네킹이 보이는 곳이 걸러지므로 넉넉하게 뽑는다)
        private const int NavMeshCandidateCount = 60;

        private GuardSpawnSelector awaySelector;
        private bool collected;

        /// <summary>
        /// 불러온 모든 씬의 스폰 지점을 처음 쓸 때 한 번 수집한다.
        /// (이 컴포넌트는 공용 시스템 씬에 있고 맵 씬은 함께 불러오므로, Awake 시점에는 맵이 아직 없을 수 있다)
        /// </summary>
        private void EnsureCollected()
        {
            if (collected)
                return;
            collected = true;

            foreach (SpawnPoint point in FindObjectsByType<SpawnPoint>())
            {
                if (!pointsByRole.TryGetValue(point.Role, out List<SpawnPoint> list))
                {
                    list = new List<SpawnPoint>();
                    pointsByRole[point.Role] = list;
                }
                list.Add(point);
            }

            foreach (List<SpawnPoint> list in pointsByRole.Values)
                Shuffle(list);
        }

        /// <summary>
        /// 새 판을 시작할 때 배정 순서를 초기화하고 스폰 지점을 다시 섞는다.
        /// </summary>
        public void ResetAssignments()
        {
            EnsureCollected();
            nextIndexByRole.Clear();
            foreach (List<SpawnPoint> list in pointsByRole.Values)
                Shuffle(list);
            awaySelector = null;
        }

        /// <summary>
        /// 역할의 스폰 방식에 맞는 위치와 방향을 고른다.
        /// </summary>
        /// <returns>위치를 정할 수 있으면 true. (지점도 NavMesh도 없으면 false)</returns>
        public bool TryGetSpawnPose(PlayerRole role, out Pose pose)
        {
            if (RoleDefinitions.Get(role).Placement == SpawnPlacement.AwayFromMannequins)
            {
                awaySelector ??= CreateAwaySelector(role);
                if (awaySelector.TrySelect(out pose))
                    return true;
            }

            if (TryGetNext(role, out SpawnPoint point))
            {
                pose = new Pose(point.transform.position, point.transform.rotation);
                return true;
            }

            pose = default;
            return false;
        }

        /// <summary>
        /// 이번 판의 "마네킹에서 먼 위치" 선택기를 만든다.
        /// 후보는 역할의 스폰 지점과 NavMesh 무작위 지점 중 마네킹 스폰 지점까지 걸어서 갈 수 있고 서 있을 공간이 있는 곳이다.
        /// (NavMesh는 투명 천장 위·지붕 위·경계 밖에도 구워지므로 걸러야 한다)
        /// </summary>
        private GuardSpawnSelector CreateAwaySelector(PlayerRole role)
        {
            var mannequinSpawns = new List<Vector3>();
            foreach (SpawnPoint point in GetPoints(PlayerRole.Mannequin))
                mannequinSpawns.Add(point.transform.position);

            var rawCandidates = new List<Vector3>();
            foreach (SpawnPoint point in GetPoints(role))
                rawCandidates.Add(point.transform.position);
            MapNavMesh.EnsureBuilt();
            rawCandidates.AddRange(NavMeshPointSampler.Sample(NavMeshCandidateCount, out _));

            List<Vector3> candidates = SpawnCandidateFilter.FilterStandable(rawCandidates, mannequinSpawns);
            if (candidates.Count == 0)
            {
                // 모두 걸러졌으면(NavMesh 이상 등) 예전처럼 거르지 않은 후보를 쓴다.
                Debug.LogWarning($"[SpawnPointRegistry] 갈 수 있는 가드 스폰 후보가 없어 검사 전 후보 {rawCandidates.Count}개를 사용합니다. 맵의 NavMesh와 스폰 지점을 확인하세요.");
                candidates = rawCandidates;
            }

            // 시점 기준인 맵 중심은 갈 수 있는 후보의 평균으로 잡는다. (지붕 위 같은 섬이 중심을 끌어가지 않도록)
            Vector3 center = candidates.Count > 0 ? Average(candidates) : Average(mannequinSpawns);
            float sightDistance = GameTuning.GuardSpawnSightDistance;

            return new GuardSpawnSelector(candidates, mannequinSpawns, center,
                GameTuning.GuardSpawnMinDistance, GameTuning.GuardSpawnSeparation,
                point => SpawnCandidateFilter.CanSeeAny(point, mannequinSpawns, sightDistance));
        }

        /// <summary>
        /// 점들의 평균. 비면 원점.
        /// </summary>
        private static Vector3 Average(List<Vector3> points)
        {
            if (points.Count == 0)
                return Vector3.zero;
            Vector3 sum = Vector3.zero;
            foreach (Vector3 point in points)
                sum += point;
            return sum / points.Count;
        }

        /// <summary>
        /// 역할에 맞는 다음 스폰 지점을 반환한다. 지점이 부족하면 처음부터 다시 사용한다.
        /// </summary>
        /// <param name="role">스폰할 역할.</param>
        /// <param name="point">배정된 스폰 지점.</param>
        /// <returns>해당 역할의 스폰 지점이 존재하는지 여부.</returns>
        public bool TryGetNext(PlayerRole role, out SpawnPoint point)
        {
            EnsureCollected();
            point = null;
            if (!pointsByRole.TryGetValue(role, out List<SpawnPoint> list) || list.Count == 0)
                return false;

            nextIndexByRole.TryGetValue(role, out int index);
            point = list[index % list.Count];
            nextIndexByRole[role] = index + 1;
            return true;
        }

        /// <summary>
        /// 역할의 스폰 지점 전체. (배정 순서와 무관하게 읽기만 함, 없으면 빈 목록)
        /// </summary>
        public IReadOnlyList<SpawnPoint> GetPoints(PlayerRole role)
        {
            EnsureCollected();
            return pointsByRole.TryGetValue(role, out List<SpawnPoint> list) ? list : System.Array.Empty<SpawnPoint>();
        }

        /// <summary>
        /// 목록의 순서를 무작위로 섞는다.
        /// </summary>
        private static void Shuffle(List<SpawnPoint> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
