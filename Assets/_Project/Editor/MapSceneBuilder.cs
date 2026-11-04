using System.Collections.Generic;
using System.Linq;
using GuardVsMannequin.Core;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 맵 빌더(지하 연구소·환승역)와 맵 편집 도구가 함께 쓰는 씬 구성 도우미.
    /// - 환경 정리: 게임에 방해되는 스크립트·카메라 제거, 충돌체 없는 메시에 충돌체 추가
    /// - 바닥 분석: NavMesh로 걸을 수 있는 바닥 점을 뽑고 서로 멀리 떨어진 점 고르기
    /// - 지점·경계: 스폰·전원 장치 표식, 플레이 영역을 둘러싸는 투명 벽과 천장
    /// </summary>
    public static class MapSceneBuilder
    {
        private const int FloorSampleCount = 600;

        // 룸 설정의 전원 장치 수 최댓값(game_settings.csv)과 맞춘다.
        internal const int GeneratorCandidateCount = 12;

        /// <summary>가드 스폰 후보 지점을 모아 두는 그룹 이름.</summary>
        internal const string GuardSpawnGroup = "GuardSpawnPoints";

        private const float SpawnEdgeClearance = 0.5f;
        internal const float GeneratorEdgeClearance = 1.0f;
        private const float IndoorCeilingCheck = 12f;

        #region 환경 정리

        /// <summary>
        /// 게임에 방해되는 것을 제거한다.
        /// - 카메라·플레이어 컨트롤러·UI·이벤트 시스템 오브젝트
        /// - 누락된 스크립트, Unity 기본 컴포넌트가 아닌 스크립트(에셋 데모용 스크립트)
        /// - 물리 오브젝트는 움직이지 않게 고정 (피어마다 따로 시뮬레이션되면 위치가 어긋나므로)
        /// </summary>
        internal static void CleanEnvironment(Transform environment)
        {
            // 프리팹 인스턴스 안의 오브젝트는 지울 수 없으므로 먼저 연결을 푼다. (생성된 맵은 원본 에셋과 독립)
            foreach (Transform child in environment.GetComponentsInChildren<Transform>(true))
            {
                if (child != null && PrefabUtility.IsOutermostPrefabInstanceRoot(child.gameObject))
                    PrefabUtility.UnpackPrefabInstance(child.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            foreach (Transform child in environment.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (child == null || child == environment)
                    continue;

                GameObject target = child.gameObject;
                if (target.GetComponent<Camera>() != null || target.GetComponent<CharacterController>() != null
                    || target.GetComponent<Canvas>() != null || target.GetComponent<UnityEngine.EventSystems.EventSystem>() != null)
                {
                    Object.DestroyImmediate(target);
                    continue;
                }

                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);
                foreach (MonoBehaviour behaviour in target.GetComponents<MonoBehaviour>())
                {
                    string ns = behaviour.GetType().Namespace ?? string.Empty;
                    if (!ns.StartsWith("UnityEngine"))
                        Object.DestroyImmediate(behaviour);
                }

                foreach (AudioListener listener in target.GetComponents<AudioListener>())
                    Object.DestroyImmediate(listener);
                foreach (Rigidbody body in target.GetComponents<Rigidbody>())
                    body.isKinematic = true;
            }
        }

        /// <summary>
        /// 충돌체가 없는 메시에 메시 충돌체를 붙인다. (LOD는 가장 자세한 단계만, 아주 작은 장식은 제외)
        /// </summary>
        internal static int AddMissingColliders(Transform environment)
        {
            HashSet<Renderer> lowerLods = GetLowerLodRenderers(environment);
            int added = 0;
            foreach (MeshRenderer meshRenderer in environment.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (lowerLods.Contains(meshRenderer) || meshRenderer.GetComponent<Collider>() != null)
                    continue;

                MeshFilter filter = meshRenderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;
                if (meshRenderer.bounds.size.magnitude < 0.1f)
                    continue;

                MeshCollider meshCollider = meshRenderer.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = filter.sharedMesh;
                added++;
            }
            return added;
        }

        /// <summary>
        /// LOD 그룹에서 가장 자세한 단계(LOD0)가 아닌 렌더러를 모은다.
        /// </summary>
        private static HashSet<Renderer> GetLowerLodRenderers(Transform environment)
        {
            var result = new HashSet<Renderer>();
            foreach (LODGroup group in environment.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = group.GetLODs();
                for (int i = 1; i < lods.Length; i++)
                {
                    foreach (Renderer lodRenderer in lods[i].renderers)
                    {
                        if (lodRenderer != null)
                            result.Add(lodRenderer);
                    }
                }
            }
            return result;
        }

        #endregion

        #region 바닥 분석과 배치

        /// <summary>
        /// NavMesh를 구워 걸을 수 있는 바닥에서 점을 넓이에 비례해 뽑는다.
        /// 벽에 너무 붙은 점, (indoor면) 천장이 없는 점을 버리고, 가장 넓게 이어진 영역의 점만 남긴다.
        /// (지붕 위처럼 떨어진 곳을 제외하기 위함)
        /// </summary>
        internal static List<Vector3> SampleFloorPoints(NavMeshSurface surface, bool indoor)
        {
            surface.BuildNavMesh();
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            List<Vector3> result;
            int triangleCount = triangulation.indices.Length / 3;
            if (triangleCount == 0)
            {
                Debug.LogError("[MapSceneBuilder] NavMesh를 만들지 못했습니다. 바닥에 충돌체가 있는지 확인하세요.");
                return new List<Vector3>();
            }

            // 삼각형 넓이의 누적 합으로 넓이 비례 추출
            var cumulative = new float[triangleCount];
            float total = 0f;
            for (int i = 0; i < triangleCount; i++)
            {
                Vector3 a = triangulation.vertices[triangulation.indices[i * 3]];
                Vector3 b = triangulation.vertices[triangulation.indices[i * 3 + 1]];
                Vector3 c = triangulation.vertices[triangulation.indices[i * 3 + 2]];
                total += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                cumulative[i] = total;
            }

            // 천장 검사는 아래에서 위로 쏘므로 메시의 뒷면을 맞혀야 한다. (윗면만 있는 천장 메시가 많아,
            // 기본 설정이면 천장을 통과해 "천장 없음"이 되고 지붕 윗면이 바닥으로 잡힌다)
            bool hitBackfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            try
            {
                result = DrawPoints(triangulation, cumulative, total, indoor);
            }
            finally
            {
                Physics.queriesHitBackfaces = hitBackfaces;
            }

            // 천장이 있는 바닥이 거의 없으면 천장 조건 없이 다시 뽑는다.
            if (indoor && result.Count < FloorSampleCount / 20)
            {
                Debug.LogWarning("[MapSceneBuilder] 천장이 있는 바닥이 거의 없어 천장 조건 없이 배치합니다.");
                result = DrawPoints(triangulation, cumulative, total, false);
            }

            return KeepLargestConnectedRegion(result);
        }

        /// <summary>
        /// 넓이 비례로 점을 뽑고, 벽에 너무 붙은 점과 (조건이 있으면) 천장이 없는 점을 버린다.
        /// </summary>
        private static List<Vector3> DrawPoints(NavMeshTriangulation triangulation, float[] cumulative, float total, bool requireCeiling)
        {
            var result = new List<Vector3>();
            var random = new System.Random(20260927);
            for (int n = 0; n < FloorSampleCount; n++)
            {
                float pick = (float)random.NextDouble() * total;
                int index = System.Array.BinarySearch(cumulative, pick);
                if (index < 0)
                    index = ~index;
                index = Mathf.Clamp(index, 0, cumulative.Length - 1);

                Vector3 point = RandomPointInTriangle(triangulation, index, random);
                if (NavMeshEdgeTooClose(point, SpawnEdgeClearance))
                    continue;
                if (requireCeiling && !Physics.Raycast(point + Vector3.up * 0.5f, Vector3.up, IndoorCeilingCheck))
                    continue;
                result.Add(point);
            }
            return result;
        }

        /// <summary>
        /// 삼각형 안의 무작위 점.
        /// </summary>
        private static Vector3 RandomPointInTriangle(NavMeshTriangulation triangulation, int triangle, System.Random random)
        {
            Vector3 a = triangulation.vertices[triangulation.indices[triangle * 3]];
            Vector3 b = triangulation.vertices[triangulation.indices[triangle * 3 + 1]];
            Vector3 c = triangulation.vertices[triangulation.indices[triangle * 3 + 2]];
            float r1 = Mathf.Sqrt((float)random.NextDouble());
            float r2 = (float)random.NextDouble();
            return (1f - r1) * a + r1 * (1f - r2) * b + r1 * r2 * c;
        }

        /// <summary>
        /// 경로가 이어지는 점이 가장 많은 영역만 남긴다.
        /// </summary>
        private static List<Vector3> KeepLargestConnectedRegion(List<Vector3> points)
        {
            if (points.Count < 2)
                return points;

            var path = new NavMeshPath();
            List<Vector3> best = points;
            int bestCount = -1;
            // 후보 기준점 몇 개로 각각 도달 가능한 점을 세고, 가장 많은 쪽을 고른다.
            int seedStep = Mathf.Max(1, points.Count / 12);
            for (int s = 0; s < points.Count; s += seedStep)
            {
                var reachable = new List<Vector3>();
                foreach (Vector3 point in points)
                {
                    if (NavMesh.CalculatePath(points[s], point, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                        reachable.Add(point);
                }
                if (reachable.Count > bestCount)
                {
                    bestCount = reachable.Count;
                    best = reachable;
                }
                if (bestCount > points.Count / 2)
                    break;
            }
            return best;
        }

        /// <summary>
        /// 서로 최대한 멀리 떨어진 점을 개수만큼 고른다. (가장 먼 점 샘플링)
        /// 가장자리 여유가 부족한 점은 제외한다.
        /// </summary>
        internal static List<Vector3> PickSpread(List<Vector3> points, int count, float edgeClearance)
        {
            List<Vector3> candidates = points
                .Where(p => !NavMeshEdgeTooClose(p, edgeClearance))
                .ToList();
            if (candidates.Count == 0)
                candidates = new List<Vector3>(points);

            var chosen = new List<Vector3>();
            if (candidates.Count == 0 || count <= 0)
                return chosen;

            chosen.Add(candidates[candidates.Count / 2]);
            while (chosen.Count < count && chosen.Count < candidates.Count)
            {
                Vector3 farthest = candidates[0];
                float farthestDistance = -1f;
                foreach (Vector3 candidate in candidates)
                {
                    float nearest = chosen.Min(c => (c - candidate).sqrMagnitude);
                    if (nearest > farthestDistance)
                    {
                        farthestDistance = nearest;
                        farthest = candidate;
                    }
                }
                chosen.Add(farthest);
            }
            return chosen;
        }

        /// <summary>
        /// NavMesh 가장자리(벽)까지의 거리가 기준보다 가까운지 확인한다.
        /// </summary>
        private static bool NavMeshEdgeTooClose(Vector3 point, float clearance)
        {
            return NavMesh.FindClosestEdge(point, out NavMeshHit edge, NavMesh.AllAreas) && edge.distance < clearance;
        }

        /// <summary>
        /// 점마다 오브젝트를 만들고 플레이 영역 중심을 바라보게 한다.
        /// </summary>
        internal static void CreatePoints(Transform mapRoot, string groupName, List<Vector3> positions, Vector3 center,
            System.Action<Transform, Vector3, Quaternion> create)
        {
            Transform group = new GameObject(groupName).transform;
            group.SetParent(mapRoot, false);
            foreach (Vector3 position in positions)
            {
                Vector3 toCenter = center - position;
                toCenter.y = 0f;
                Quaternion rotation = toCenter.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toCenter) : Quaternion.identity;
                create(group, position, rotation);
            }
        }

        /// <summary>
        /// 컴포넌트 하나를 가진 표식 오브젝트를 만든다.
        /// </summary>
        internal static void CreateMarker<T>(Transform parent, string name, Vector3 position, Quaternion rotation) where T : Component
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.SetPositionAndRotation(position, rotation);
            marker.AddComponent<T>();
        }

        #endregion

        #region 경계

        /// <summary>
        /// 플레이 영역을 둘러싸는 투명 벽과 천장(충돌체만)을 만든다. 맵 루트 밖에 두어 NavMesh와 변신 대상에서 제외한다.
        /// 맵 정보에 쓸 천장 높이(바닥에서 투명 천장까지)를 반환한다.
        /// </summary>
        internal static float CreateBoundary(Bounds playArea, Transform environment)
        {
            Bounds environmentBounds = CharacterAssetBuilder.GetRendererBounds(environment.gameObject);
            float floorY = playArea.min.y;
            float top = Mathf.Min(environmentBounds.max.y + 1f, floorY + GameConstants.GunRange - 10f);
            top = Mathf.Max(top, playArea.max.y + 4f);

            Transform boundary = new GameObject("Boundary (Invisible)").transform;
            float height = top - floorY + 10f;
            float centerY = floorY - 5f + height * 0.5f;
            Vector3 center = playArea.center;
            Vector3 size = playArea.size + new Vector3(2f, 0f, 2f);

            CreateInvisibleBox(boundary, "Ceiling", new Vector3(center.x, top + 0.25f, center.z), new Vector3(size.x, 0.5f, size.z));
            CreateInvisibleBox(boundary, "Wall_North", new Vector3(center.x, centerY, center.z + size.z * 0.5f), new Vector3(size.x, height, 0.5f));
            CreateInvisibleBox(boundary, "Wall_South", new Vector3(center.x, centerY, center.z - size.z * 0.5f), new Vector3(size.x, height, 0.5f));
            CreateInvisibleBox(boundary, "Wall_East", new Vector3(center.x + size.x * 0.5f, centerY, center.z), new Vector3(0.5f, height, size.z));
            CreateInvisibleBox(boundary, "Wall_West", new Vector3(center.x - size.x * 0.5f, centerY, center.z), new Vector3(0.5f, height, size.z));
            return top - floorY;
        }

        /// <summary>
        /// 보이지 않는 상자 충돌체를 만든다.
        /// </summary>
        private static void CreateInvisibleBox(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var box = new GameObject(name);
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.isStatic = true;
            box.AddComponent<BoxCollider>().size = size;
        }

        #endregion
    }
}
