using System.Collections.Generic;
using System.Linq;
using GuardVsMannequin.Core;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace GuardVsMannequin.Editor
{
    /// <summary>맵 빌더와 맵 편집 도구가 함께 쓰는 환경 정리·바닥 분석·경계 생성 도우미.</summary>
    public static class MapSceneBuilder
    {
        private const int FloorSampleCount = 600;

        // 룸 설정의 전원 장치 수 최댓값과 맞춘다.
        internal const int GeneratorCandidateCount = 12;

        /// <summary>가드 스폰 후보 지점을 모아 두는 그룹 이름.</summary>
        internal const string GuardSpawnGroup = "GuardSpawnPoints";

        private const float SpawnEdgeClearance = 0.5f;
        internal const float GeneratorEdgeClearance = 1.0f;
        private const float IndoorCeilingCheck = 12f;

        #region 환경 정리

        /// <summary>에셋 팩 씬의 카메라·플레이어·스크립트가 게임 시스템과 충돌하므로 제거하고 물리 오브젝트는 고정한다.</summary>
        internal static void CleanEnvironment(Transform environment)
        {
            // 프리팹 인스턴스 안의 오브젝트는 지울 수 없으므로 먼저 연결을 푼다.
            // 맵 물체 프리팹은 정리할 스크립트가 없어 연결을 유지한다.
            foreach (Transform child in environment.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || !PrefabUtility.IsOutermostPrefabInstanceRoot(child.gameObject))
                    continue;
                if (MapObjectCatalog.IsObjectPrefab(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject)))
                    continue;
                PrefabUtility.UnpackPrefabInstance(child.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            foreach (Transform child in environment.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (child == null || child == environment)
                    continue;

                GameObject childObject = child.gameObject;
                if (childObject.GetComponent<Camera>() != null || childObject.GetComponent<CharacterController>() != null
                    || childObject.GetComponent<Canvas>() != null || childObject.GetComponent<UnityEngine.EventSystems.EventSystem>() != null)
                {
                    Object.DestroyImmediate(childObject);
                    continue;
                }

                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(childObject);
                foreach (MonoBehaviour behaviour in childObject.GetComponents<MonoBehaviour>())
                {
                    string typeNamespace = behaviour.GetType().Namespace ?? string.Empty;
                    if (!typeNamespace.StartsWith("UnityEngine"))
                        Object.DestroyImmediate(behaviour);
                }

                foreach (AudioListener audioListener in childObject.GetComponents<AudioListener>())
                    Object.DestroyImmediate(audioListener);
                foreach (Rigidbody body in childObject.GetComponents<Rigidbody>())
                    body.isKinematic = true;
            }
        }

        /// <summary>충돌체 없는 에셋 팩 모델을 플레이어가 통과하지 않도록 메시 충돌체를 붙인다.</summary>
        internal static int AddMissingColliders(Transform environment)
        {
            HashSet<Renderer> lowerLodRenderers = GetLowerLodRenderers(environment);
            int addedCount = 0;
            foreach (MeshRenderer meshRenderer in environment.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (lowerLodRenderers.Contains(meshRenderer) || meshRenderer.GetComponent<Collider>() != null)
                    continue;

                MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null)
                    continue;
                if (meshRenderer.bounds.size.magnitude < 0.1f)
                    continue;

                MeshCollider meshCollider = meshRenderer.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                addedCount++;
            }
            return addedCount;
        }

        /// <summary>
        /// 충돌체를 가장 자세한 단계(LOD0)에만 붙이려고 나머지 단계의 렌더러를 미리 모아 둔다.
        /// </summary>
        private static HashSet<Renderer> GetLowerLodRenderers(Transform environment)
        {
            var lowerLodRenderers = new HashSet<Renderer>();
            foreach (LODGroup lodGroup in environment.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = lodGroup.GetLODs();
                for (int i = 1; i < lods.Length; i++)
                {
                    foreach (Renderer lodRenderer in lods[i].renderers)
                    {
                        if (lodRenderer != null)
                            lowerLodRenderers.Add(lodRenderer);
                    }
                }
            }
            return lowerLodRenderers;
        }

        #endregion

        #region 바닥 분석과 배치

        /// <summary>지붕 위처럼 떨어진 곳을 빼도록 NavMesh에서 가장 넓게 이어진 바닥의 점만 뽑는다.</summary>
        internal static List<Vector3> SampleFloorPoints(NavMeshSurface surface, bool indoor)
        {
            surface.BuildNavMesh();
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            List<Vector3> floorPoints;
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
                Vector3 vertexA = triangulation.vertices[triangulation.indices[i * 3]];
                Vector3 vertexB = triangulation.vertices[triangulation.indices[i * 3 + 1]];
                Vector3 vertexC = triangulation.vertices[triangulation.indices[i * 3 + 2]];
                total += Vector3.Cross(vertexB - vertexA, vertexC - vertexA).magnitude * 0.5f;
                cumulative[i] = total;
            }

            // 윗면만 있는 천장 메시가 많아 위로 쏘는 천장 검사는 뒷면도 맞히게 한다.
            bool savedHitBackfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            try
            {
                floorPoints = DrawPoints(triangulation, cumulative, total, indoor);
            }
            finally
            {
                Physics.queriesHitBackfaces = savedHitBackfaces;
            }

            // 천장이 있는 바닥이 거의 없으면 천장 조건 없이 다시 뽑는다.
            if (indoor && floorPoints.Count < FloorSampleCount / 20)
            {
                Debug.LogWarning("[MapSceneBuilder] 천장이 있는 바닥이 거의 없어 천장 조건 없이 배치합니다.");
                floorPoints = DrawPoints(triangulation, cumulative, total, false);
            }

            return KeepLargestConnectedRegion(floorPoints);
        }

        /// <summary>잘게 쪼개진 곳에 점이 몰리지 않도록 넓이에 비례해 뽑고, 벽에 붙거나 천장이 없는 점은 버린다.</summary>
        private static List<Vector3> DrawPoints(NavMeshTriangulation triangulation, float[] cumulative, float total, bool requireCeiling)
        {
            var sampledPoints = new List<Vector3>();
            var random = new System.Random(20260927);
            for (int sampleIndex = 0; sampleIndex < FloorSampleCount; sampleIndex++)
            {
                float pickedArea = (float)random.NextDouble() * total;
                int triangleIndex = System.Array.BinarySearch(cumulative, pickedArea);
                if (triangleIndex < 0)
                    triangleIndex = ~triangleIndex;
                triangleIndex = Mathf.Clamp(triangleIndex, 0, cumulative.Length - 1);

                Vector3 point = RandomPointInTriangle(triangulation, triangleIndex, random);
                if (NavMeshEdgeTooClose(point, SpawnEdgeClearance))
                    continue;
                if (requireCeiling && !Physics.Raycast(point + Vector3.up * 0.5f, Vector3.up, IndoorCeilingCheck))
                    continue;
                sampledPoints.Add(point);
            }
            return sampledPoints;
        }

        /// <summary>
        /// 난수 두 개를 그대로 무게중심 좌표로 쓰면 한 꼭짓점 쪽에 점이 몰리므로, 첫 난수에 제곱근을 씌워 삼각형 안에 고르게 분포하게 한다.
        /// </summary>
        private static Vector3 RandomPointInTriangle(NavMeshTriangulation triangulation, int triangle, System.Random random)
        {
            Vector3 vertexA = triangulation.vertices[triangulation.indices[triangle * 3]];
            Vector3 vertexB = triangulation.vertices[triangulation.indices[triangle * 3 + 1]];
            Vector3 vertexC = triangulation.vertices[triangulation.indices[triangle * 3 + 2]];
            float sqrtRandom = Mathf.Sqrt((float)random.NextDouble());
            float edgeRandom = (float)random.NextDouble();
            return (1f - sqrtRandom) * vertexA + sqrtRandom * (1f - edgeRandom) * vertexB + sqrtRandom * edgeRandom * vertexC;
        }

        /// <summary>
        /// 지붕 위·닫힌 방처럼 걸어서 갈 수 없는 곳에 스폰하면 갇히므로, 경로가 이어지는 점이 가장 많은 영역만 남긴다.
        /// </summary>
        private static List<Vector3> KeepLargestConnectedRegion(List<Vector3> points)
        {
            if (points.Count < 2)
                return points;

            var navMeshPath = new NavMeshPath();
            List<Vector3> bestRegion = points;
            int bestCount = -1;
            // 후보 기준점 몇 개로 각각 도달 가능한 점을 세고, 가장 많은 쪽을 고른다.
            int seedStep = Mathf.Max(1, points.Count / 12);
            for (int seedIndex = 0; seedIndex < points.Count; seedIndex += seedStep)
            {
                var reachablePoints = new List<Vector3>();
                foreach (Vector3 point in points)
                {
                    if (NavMesh.CalculatePath(points[seedIndex], point, NavMesh.AllAreas, navMeshPath) && navMeshPath.status == NavMeshPathStatus.PathComplete)
                        reachablePoints.Add(point);
                }
                if (reachablePoints.Count > bestCount)
                {
                    bestCount = reachablePoints.Count;
                    bestRegion = reachablePoints;
                }
                if (bestCount > points.Count / 2)
                    break;
            }
            return bestRegion;
        }

        /// <summary>판마다 같은 곳만 쓰이지 않도록 이미 고른 점에서 가장 먼 점을 차례로 골라 퍼뜨린다.</summary>
        internal static List<Vector3> PickSpread(List<Vector3> points, int count, float edgeClearance)
        {
            List<Vector3> candidates = points
                .Where(point => !NavMeshEdgeTooClose(point, edgeClearance))
                .ToList();
            if (candidates.Count == 0)
                candidates = new List<Vector3>(points);

            var chosenPoints = new List<Vector3>();
            if (candidates.Count == 0 || count <= 0)
                return chosenPoints;

            chosenPoints.Add(candidates[candidates.Count / 2]);
            while (chosenPoints.Count < count && chosenPoints.Count < candidates.Count)
            {
                Vector3 farthestPoint = candidates[0];
                float farthestDistance = -1f;
                foreach (Vector3 candidate in candidates)
                {
                    float nearestChosenDistance = chosenPoints.Min(chosenPoint => (chosenPoint - candidate).sqrMagnitude);
                    if (nearestChosenDistance > farthestDistance)
                    {
                        farthestDistance = nearestChosenDistance;
                        farthestPoint = candidate;
                    }
                }
                chosenPoints.Add(farthestPoint);
            }
            return chosenPoints;
        }

        /// <summary>
        /// 벽 위치를 따로 모으지 않아도 되도록 NavMesh 가장자리를 벽으로 보고 거리를 잰다.
        /// </summary>
        private static bool NavMeshEdgeTooClose(Vector3 point, float clearance)
        {
            return NavMesh.FindClosestEdge(point, out NavMeshHit edge, NavMesh.AllAreas) && edge.distance < clearance;
        }

        /// <summary>스폰하자마자 벽을 보지 않도록 플레이 영역 중심을 바라보게 한다.</summary>
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
        /// 전원 장치 후보처럼 컴포넌트 하나만 있으면 되는 표식이 여러 종류라 형식 매개변수로 한 함수에서 만든다.
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

        /// <summary>NavMesh와 변신 대상에서 빠지도록 맵 루트 밖에 투명 벽과 천장을 만들고 천장 높이를 돌려준다.</summary>
        internal static float CreateBoundary(Bounds playArea, Transform environment)
        {
            Bounds environmentBounds = CharacterAssetBuilder.GetRendererBounds(environment.gameObject);
            float floorY = playArea.min.y;
            float ceilingY = Mathf.Min(environmentBounds.max.y + 1f, floorY + GameConstants.GunRange - 10f);
            ceilingY = Mathf.Max(ceilingY, playArea.max.y + 4f);

            Transform boundary = new GameObject("Boundary (Invisible)").transform;
            float wallHeight = ceilingY - floorY + 10f;
            float wallCenterY = floorY - 5f + wallHeight * 0.5f;
            Vector3 areaCenter = playArea.center;
            Vector3 boundarySize = playArea.size + new Vector3(2f, 0f, 2f);

            CreateInvisibleBox(boundary, "Ceiling", new Vector3(areaCenter.x, ceilingY + 0.25f, areaCenter.z), new Vector3(boundarySize.x, 0.5f, boundarySize.z));
            CreateInvisibleBox(boundary, "Wall_North", new Vector3(areaCenter.x, wallCenterY, areaCenter.z + boundarySize.z * 0.5f), new Vector3(boundarySize.x, wallHeight, 0.5f));
            CreateInvisibleBox(boundary, "Wall_South", new Vector3(areaCenter.x, wallCenterY, areaCenter.z - boundarySize.z * 0.5f), new Vector3(boundarySize.x, wallHeight, 0.5f));
            CreateInvisibleBox(boundary, "Wall_East", new Vector3(areaCenter.x + boundarySize.x * 0.5f, wallCenterY, areaCenter.z), new Vector3(0.5f, wallHeight, boundarySize.z));
            CreateInvisibleBox(boundary, "Wall_West", new Vector3(areaCenter.x - boundarySize.x * 0.5f, wallCenterY, areaCenter.z), new Vector3(0.5f, wallHeight, boundarySize.z));
            return ceilingY - floorY;
        }

        /// <summary>
        /// 경계는 막기만 하면 되므로 렌더러 없이 충돌체만 가진 오브젝트로 만든다.
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
