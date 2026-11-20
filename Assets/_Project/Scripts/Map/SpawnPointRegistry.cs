using System.Collections.Generic;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Map
{
    /// <summary>역할마다 스폰 방식이 달라 씬의 스폰 지점을 역할별로 모아 판마다 나눠 준다.</summary>
    public class SpawnPointRegistry : MonoBehaviour
    {
        private readonly Dictionary<PlayerRole, List<SpawnPoint>> pointsByRole = new();
        private readonly Dictionary<PlayerRole, int> nextIndexByRole = new();

        // 걸러지는 후보가 많아 넉넉하게 잡은 가드 위치 후보 NavMesh 지점 수.
        private const int NavMeshCandidateCount = 60;

        private GuardSpawnSelector awaySelector;
        private bool isCollected;

        /// <summary>Awake 때는 맵 씬이 아직 없을 수 있어 처음 쓸 때 불러온 모든 씬의 스폰 지점을 모은다.</summary>
        private void EnsureCollected()
        {
            if (isCollected)
                return;
            isCollected = true;

            foreach (SpawnPoint point in FindObjectsByType<SpawnPoint>())
            {
                if (!pointsByRole.TryGetValue(point.Role, out List<SpawnPoint> rolePoints))
                {
                    rolePoints = new List<SpawnPoint>();
                    pointsByRole[point.Role] = rolePoints;
                }
                rolePoints.Add(point);
            }

            foreach (List<SpawnPoint> rolePoints in pointsByRole.Values)
                Shuffle(rolePoints);
        }

        /// <summary>
        /// 판마다 같은 사람이 같은 자리에서 시작하지 않도록 배정 순서를 초기화하고 지점을 다시 섞는다.
        /// </summary>
        public void ResetAssignments()
        {
            EnsureCollected();
            nextIndexByRole.Clear();
            foreach (List<SpawnPoint> rolePoints in pointsByRole.Values)
                Shuffle(rolePoints);
            awaySelector = null;
        }

        /// <summary>역할마다 스폰 방식이 달라 정의를 보고 고르고, 가드 방식이 실패하면 놓인 지점을 쓴다.</summary>
        /// <returns>지점도 NavMesh도 없으면 false.</returns>
        public bool TryGetSpawnPose(PlayerRole role, out Pose pose)
        {
            if (RoleDefinitions.Get(role).Placement == SpawnPlacement.RandomInMap)
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

        /// <summary>NavMesh는 지붕 위·경계 밖에도 구워지므로 마네킹 스폰 지점까지 갈 수 있는 후보만 넘기고, 검사가 비싸 판마다 한 번만 만든다.</summary>
        private GuardSpawnSelector CreateAwaySelector(PlayerRole role)
        {
            var mannequinSpawns = new List<Vector3>();
            foreach (SpawnPoint point in GetPoints(PlayerRole.Mannequin))
                mannequinSpawns.Add(point.transform.position);

            var placedCandidates = new List<Vector3>();
            foreach (SpawnPoint point in GetPoints(role))
                placedCandidates.Add(point.transform.position);
            MapNavMesh.EnsureBuilt();
            var rawCandidates = new List<Vector3>(placedCandidates);
            rawCandidates.AddRange(NavMeshPointSampler.Sample(NavMeshCandidateCount, out _));

            List<Vector3> candidates = SpawnCandidateFilter.FilterStandable(rawCandidates, mannequinSpawns, out SpawnCandidateFilter.Report report);
            if (candidates.Count == 0)
            {
                // 무작위 NavMesh 지점에는 외곽 벽 윗면·지붕처럼 갇히는 곳이 섞여 있어, 검사에 실패하면 맵 제작자가 놓은 지점만 쓴다.
                candidates = placedCandidates.Count > 0 ? placedCandidates : rawCandidates;
                Debug.LogWarning($"[SpawnPointRegistry] 갈 수 있는 가드 스폰 후보가 없어 {(placedCandidates.Count > 0 ? "놓인 가드 스폰 지점" : "검사 전 후보")} {candidates.Count}개를 사용합니다. " +
                                 $"({report}) 맵의 NavMesh를 다시 굽거나 스폰 지점을 확인하세요.");
            }

            // 지붕 위 같은 섬이 중심을 끌어가지 않도록 갈 수 있는 후보의 평균을 맵 중심으로 잡는다.
            Vector3 center = candidates.Count > 0 ? Average(candidates) : Average(mannequinSpawns);
            return new GuardSpawnSelector(candidates, center, GameTuning.GuardSpawnSeparation);
        }

        /// <summary>
        /// 0으로 나누지 않도록 비어 있으면 원점을 준다.
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
        /// 인원보다 지점이 적은 맵에서도 스폰이 실패하지 않도록, 다 쓰면 처음부터 다시 쓴다.
        /// </summary>
        /// <returns>해당 역할의 스폰 지점이 하나도 없으면 false.</returns>
        public bool TryGetNext(PlayerRole role, out SpawnPoint point)
        {
            EnsureCollected();
            point = null;
            if (!pointsByRole.TryGetValue(role, out List<SpawnPoint> rolePoints) || rolePoints.Count == 0)
                return false;

            nextIndexByRole.TryGetValue(role, out int nextIndex);
            point = rolePoints[nextIndex % rolePoints.Count];
            nextIndexByRole[role] = nextIndex + 1;
            return true;
        }

        /// <summary>배정 순서를 건드리지 않고 지점 목록만 읽어야 하는 곳을 위해 연다.</summary>
        public IReadOnlyList<SpawnPoint> GetPoints(PlayerRole role)
        {
            EnsureCollected();
            return pointsByRole.TryGetValue(role, out List<SpawnPoint> rolePoints) ? rolePoints : System.Array.Empty<SpawnPoint>();
        }

        /// <summary>
        /// 모든 순서가 같은 확률로 나오도록 Fisher-Yates로 섞는다.
        /// </summary>
        private static void Shuffle(List<SpawnPoint> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
            }
        }
    }
}
