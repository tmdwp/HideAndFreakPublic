using System.Collections.Generic;
using GuardVsMannequin.Map;
using GuardVsMannequin.Objectives;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static GuardVsMannequin.Editor.MapAssets;

namespace GuardVsMannequin.Editor
{
    /// <summary>Terrain 나무는 변신 대상이 될 수 없으므로 숲 맵에 숨을 곳이 될 덤불을 심어 변신 사물로 등록한다.</summary>
    public static class ForestBushPlacer
    {
        private const string ForestScenePath = "Assets/_Project/Scenes/Custom/Map_Forest.unity";
        private const string GroupName = "Bushes";
        private const int Seed = 2026;

        // 무리 수와 무리당 덤불 수
        private const int ClusterTarget = 70;
        private const int MinPerCluster = 1;
        private const int MaxPerCluster = 3;

        // 거리 조건(m)
        private const float ClusterSpacing = 14f;
        private const float BushSpacing = 2.4f;
        private const float ClusterRadius = 3.5f;
        private const float EdgeMargin = 4f;
        private const float TreeClearance = 2f;
        private const float MarkerClearance = 4f;

        // 심을 수 있는 최대 경사(도), 땅에 묻는 깊이(m), 크기 범위.
        private const float MaxSlope = 28f;
        private const float SinkDepth = 0.05f;
        private const float MinScale = 0.85f;
        private const float MaxScale = 1.15f;

        private const int MaxAttemptsPerCluster = 40;
        private const float TreeCellSize = 4f;

        private static readonly string[] BushModels = { Nature.BushRound, Nature.BushWide, Nature.BushTall, Nature.BushBerry };

        /// <summary>메뉴 하나로 끝나도록 숲 맵을 직접 열어 심고 저장까지 한다.</summary>
        [MenuItem(EditorMenus.Root + "맵 생성/숲 덤불 배치 (Map_Forest)")]
        private static void PlaceInForest()
        {
            MapObjectPrefabBuilder.EnsurePrefabs();
            GameObject[] bushModels = LoadModels();
            if (bushModels == null)
                return;

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ForestScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                scene = EditorSceneManager.OpenScene(ForestScenePath, OpenSceneMode.Single);
            }

            GameObject environment = MapOptimizer.FindByPath(scene, MapOptimizer.EnvironmentPath);
            GameObject boundary = MapOptimizer.FindByPath(scene, MapOptimizer.BoundaryName);
            List<Terrain> terrains = FindTerrains(scene);
            if (environment == null || boundary == null || terrains.Count == 0)
            {
                EditorUtility.DisplayDialog("숲 덤불 배치",
                    $"'{MapOptimizer.EnvironmentPath}', '{MapOptimizer.BoundaryName}' 또는 Terrain을 찾지 못했습니다.", "확인");
                return;
            }

            Transform bushGroup = RecreateGroup(environment.transform);
            Bounds playArea = MapOptimizer.GetBoundaryBounds(boundary.transform);
            int plantedCount = Plant(bushGroup, bushModels, terrains, playArea, CollectMarkers(scene));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ForestBushPlacer] 덤불 {plantedCount}개를 심고 변신 사물로 등록했습니다. ({scene.path})");
            EditorUtility.DisplayDialog("숲 덤불 배치", $"덤불 {plantedCount}개를 심고 씬을 저장했습니다.\n위치가 마음에 들지 않는 덤불은 옮기거나 지운 뒤 저장하세요.", "확인");
        }

        /// <summary>
        /// 일부 모델만으로 심으면 다시 실행했을 때 배치가 달라지므로, 하나라도 없으면 심지 않고 해결 방법을 알린다.
        /// </summary>
        private static GameObject[] LoadModels()
        {
            var models = new GameObject[BushModels.Length];
            for (int i = 0; i < BushModels.Length; i++)
            {
                models[i] = AssetDatabase.LoadAssetAtPath<GameObject>(BushModels[i]);
                if (models[i] == null)
                {
                    EditorUtility.DisplayDialog("숲 덤불 배치", $"덤불 프리팹이 없습니다: {BushModels[i]}\n메뉴 `맵 물체/물체 프리팹 만들기·갱신`을 실행한 뒤 다시 실행하세요.", "확인");
                    return null;
                }
            }
            return models;
        }

        /// <summary>다시 실행할 때 덤불이 두 벌이 되지 않도록 이전 묶음을 지운다.</summary>
        private static Transform RecreateGroup(Transform environment)
        {
            Transform oldGroup = environment.Find(GroupName);
            if (oldGroup != null)
                Undo.DestroyObjectImmediate(oldGroup.gameObject);

            var bushGroup = new GameObject(GroupName);
            bushGroup.transform.SetParent(environment, false);
            Undo.RegisterCreatedObjectUndo(bushGroup, "숲 덤불 배치");
            return bushGroup.transform;
        }

        /// <summary>숨을 곳이 단조롭지 않도록 무리끼리는 멀리, 무리 안은 가깝게 고정 시드로 심는다.</summary>
        private static int Plant(Transform bushGroup, GameObject[] bushModels, List<Terrain> terrains, Bounds playArea, List<Vector3> markers)
        {
            var random = new System.Random(Seed);
            Dictionary<Vector2Int, List<Vector2>> treeCells = CollectTrees(terrains);
            var clusterCenters = new List<Vector2>();
            var plantedPoints = new List<Vector2>();

            float minX = playArea.min.x + EdgeMargin, maxX = playArea.max.x - EdgeMargin;
            float minZ = playArea.min.z + EdgeMargin, maxZ = playArea.max.z - EdgeMargin;
            int maxAttempts = ClusterTarget * MaxAttemptsPerCluster;

            for (int i = 0; i < maxAttempts && clusterCenters.Count < ClusterTarget; i++)
            {
                var clusterCenter = new Vector2(Range(random, minX, maxX), Range(random, minZ, maxZ));
                if (IsNear(clusterCenters, clusterCenter, ClusterSpacing))
                    continue;
                if (!IsPlantable(clusterCenter, terrains, treeCells, markers, plantedPoints, out _))
                    continue;

                clusterCenters.Add(clusterCenter);
                int bushCount = random.Next(MinPerCluster, MaxPerCluster + 1);
                for (int plantedInCluster = 0, tryCount = 0; plantedInCluster < bushCount && tryCount < bushCount * 10; tryCount++)
                {
                    Vector2 point = plantedInCluster == 0 ? clusterCenter : clusterCenter + RandomInCircle(random, ClusterRadius);
                    if (point.x < minX || point.x > maxX || point.y < minZ || point.y > maxZ)
                        continue;
                    if (!IsPlantable(point, terrains, treeCells, markers, plantedPoints, out float groundHeight))
                        continue;

                    GameObject bushModel = bushModels[random.Next(bushModels.Length)];
                    CreateBush(bushGroup, bushModel, new Vector3(point.x, groundHeight - SinkDepth, point.y),
                        Quaternion.Euler(0f, Range(random, 0f, 360f), 0f), Range(random, MinScale, MaxScale));
                    plantedPoints.Add(point);
                    plantedInCluster++;
                }
            }
            return plantedPoints.Count;
        }

        /// <summary>덤불이 떠 보이거나 박히거나 길을 막지 않도록 가파른 곳·나무 밑동·스폰 근처를 피한다.</summary>
        private static bool IsPlantable(Vector2 point, List<Terrain> terrains, Dictionary<Vector2Int, List<Vector2>> treeCells,
            List<Vector3> markers, List<Vector2> plantedPoints, out float height)
        {
            height = 0f;
            if (!TrySampleGround(point, terrains, out height, out float slope) || slope > MaxSlope)
                return false;
            if (IsNear(plantedPoints, point, BushSpacing) || IsNearTree(treeCells, point))
                return false;
            foreach (Vector3 marker in markers)
            {
                if (Vector2.Distance(point, new Vector2(marker.x, marker.z)) < MarkerClearance)
                    return false;
            }
            return true;
        }

        /// <summary>숲은 Terrain 여러 장으로 이어져 있어 그 자리를 덮는 Terrain에서 높이와 경사를 읽는다.</summary>
        private static bool TrySampleGround(Vector2 point, List<Terrain> terrains, out float height, out float slope)
        {
            foreach (Terrain terrain in terrains)
            {
                Vector3 terrainOrigin = terrain.transform.position;
                Vector3 terrainSize = terrain.terrainData.size;
                float normalizedX = (point.x - terrainOrigin.x) / terrainSize.x;
                float normalizedZ = (point.y - terrainOrigin.z) / terrainSize.z;
                if (normalizedX < 0f || normalizedX > 1f || normalizedZ < 0f || normalizedZ > 1f)
                    continue;

                var worldPoint = new Vector3(point.x, 0f, point.y);
                height = terrain.SampleHeight(worldPoint) + terrainOrigin.y;
                slope = terrain.terrainData.GetSteepness(normalizedX, normalizedZ);
                return true;
            }
            height = 0f;
            slope = 0f;
            return false;
        }

        /// <summary>덤불 프리팹을 고치면 모든 덤불에 반영되도록 연결을 유지한 인스턴스로 놓는다.</summary>
        private static void CreateBush(Transform bushGroup, GameObject bushModel, Vector3 position, Quaternion rotation, float scale)
        {
            var bush = (GameObject)PrefabUtility.InstantiatePrefab(bushModel, bushGroup);
            bush.name = $"{bushModel.name} ({bushGroup.childCount})";
            bush.transform.SetPositionAndRotation(position, rotation);
            bush.transform.localScale = Vector3.one * scale;
            Undo.RegisterCreatedObjectUndo(bush, "숲 덤불 배치");
            PropRegistrar.Register(bush, absorbChildren: false);
        }

        /// <summary>높이를 읽을 수 있고 게임에서 보이는 Terrain만 모은다.</summary>
        private static List<Terrain> FindTerrains(Scene scene)
        {
            var terrains = new List<Terrain>();
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                foreach (Terrain terrain in rootObject.GetComponentsInChildren<Terrain>())
                {
                    if (terrain.terrainData != null)
                        terrains.Add(terrain);
                }
            }
            return terrains;
        }

        /// <summary>
        /// 스폰·전원 장치 근처에 덤불이 있으면 시작부터 길을 막거나 장치를 가리므로 그 위치를 모은다.
        /// </summary>
        private static List<Vector3> CollectMarkers(Scene scene)
        {
            var markerPositions = new List<Vector3>();
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                foreach (SpawnPoint spawnPoint in rootObject.GetComponentsInChildren<SpawnPoint>(true))
                    markerPositions.Add(spawnPoint.transform.position);
                foreach (GeneratorSpawnPoint generatorPoint in rootObject.GetComponentsInChildren<GeneratorSpawnPoint>(true))
                    markerPositions.Add(generatorPoint.transform.position);
            }
            return markerPositions;
        }

        /// <summary>
        /// 나무가 수천 그루라 매번 전부 비교하면 느리므로 밑동 위치를 격자 칸별로 나눠 둔다.
        /// </summary>
        private static Dictionary<Vector2Int, List<Vector2>> CollectTrees(List<Terrain> terrains)
        {
            var treeCells = new Dictionary<Vector2Int, List<Vector2>>();
            foreach (Terrain terrain in terrains)
            {
                Vector3 terrainOrigin = terrain.transform.position;
                Vector3 terrainSize = terrain.terrainData.size;
                foreach (TreeInstance tree in terrain.terrainData.treeInstances)
                {
                    var treePoint = new Vector2(terrainOrigin.x + tree.position.x * terrainSize.x, terrainOrigin.z + tree.position.z * terrainSize.z);
                    Vector2Int cell = CellOf(treePoint);
                    if (!treeCells.TryGetValue(cell, out List<Vector2> cellTrees))
                        treeCells[cell] = cellTrees = new List<Vector2>();
                    cellTrees.Add(treePoint);
                }
            }
            return treeCells;
        }

        /// <summary>
        /// 칸 경계 근처의 나무를 놓치지 않도록 자기 칸과 주변 8칸만 확인한다.
        /// </summary>
        private static bool IsNearTree(Dictionary<Vector2Int, List<Vector2>> treeCells, Vector2 point)
        {
            Vector2Int cell = CellOf(point);
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    if (treeCells.TryGetValue(cell + new Vector2Int(offsetX, offsetY), out List<Vector2> cellTrees) && IsNear(cellTrees, point, TreeClearance))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 저장과 검색이 같은 칸 규칙을 쓰도록 한곳에 둔다.
        /// </summary>
        private static Vector2Int CellOf(Vector2 point)
        {
            return new Vector2Int(Mathf.FloorToInt(point.x / TreeCellSize), Mathf.FloorToInt(point.y / TreeCellSize));
        }

        /// <summary>
        /// 제곱근 계산을 피하려고 제곱 거리로 비교한다.
        /// </summary>
        private static bool IsNear(List<Vector2> points, Vector2 point, float distance)
        {
            float sqrDistance = distance * distance;
            foreach (Vector2 otherPoint in points)
            {
                if ((otherPoint - point).sqrMagnitude < sqrDistance)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// UnityEngine.Random은 전역 상태라 시드를 고정해도 다른 코드 영향을 받으므로 System.Random으로 범위 난수를 만든다.
        /// </summary>
        private static float Range(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        /// <summary>
        /// 무리 중심의 덤불과 겹치지 않도록 최소 BushSpacing만큼 떨어진 원 안의 위치를 고른다.
        /// </summary>
        private static Vector2 RandomInCircle(System.Random random, float radius)
        {
            float angle = Range(random, 0f, Mathf.PI * 2f);
            float distance = Range(random, BushSpacing, radius);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        }
    }
}
