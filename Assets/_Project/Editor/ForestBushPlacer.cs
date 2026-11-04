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
    /// <summary>
    /// 숲 맵(Map_Forest)의 Terrain 위에 덤불 모델(MapKit Nature)을 흩어 심고 변신 사물로 등록한다.
    /// Terrain Tree는 GameObject가 아니라 변신 대상이 될 수 없으므로, 변신용 숨을 곳은 이 덤불이 맡는다.
    /// - 2~3개씩 무리 지어 심는다. (무리끼리는 떨어지게, 무리 안은 가깝게)
    /// - 플레이 영역(투명 경계) 가장자리, 가파른 경사, Terrain 나무 밑동, 스폰·전원 장치 지점 근처는 피한다.
    /// - 고정 시드라 다시 실행해도 같은 자리에 심는다. 다시 실행하면 이전 덤불(Map/Environment/Bushes)을 지우고 새로 심는다.
    /// 끝나면 씬을 저장한다. (저장할 때 사물 번호가 매겨진다)
    /// </summary>
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

        // 심을 수 있는 최대 경사(도), 땅에 묻는 깊이(m), 크기 범위
        private const float MaxSlope = 28f;
        private const float SinkDepth = 0.05f;
        private const float MinScale = 0.85f;
        private const float MaxScale = 1.15f;

        private const int MaxAttemptsPerCluster = 40;
        private const float TreeCellSize = 4f;

        private static readonly string[] BushModels = { Nature.BushRound, Nature.BushWide, Nature.BushTall, Nature.BushBerry };

        /// <summary>
        /// 숲 맵을 열고 덤불을 다시 심은 뒤 저장한다.
        /// </summary>
        [MenuItem(EditorMenus.Root + "맵 생성/숲 덤불 배치 (Map_Forest)")]
        private static void PlaceInForest()
        {
            GameObject[] models = LoadModels();
            if (models == null)
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

            Transform group = RecreateGroup(environment.transform);
            Bounds playArea = MapOptimizer.GetBoundaryBounds(boundary.transform);
            int placed = Plant(group, models, terrains, playArea, CollectMarkers(scene));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ForestBushPlacer] 덤불 {placed}개를 심고 변신 사물로 등록했습니다. ({scene.path})");
            EditorUtility.DisplayDialog("숲 덤불 배치", $"덤불 {placed}개를 심고 씬을 저장했습니다.\n위치가 마음에 들지 않는 덤불은 옮기거나 지운 뒤 저장하세요.", "확인");
        }

        /// <summary>
        /// 덤불 모델을 불러온다. 하나라도 없으면 알리고 null.
        /// </summary>
        private static GameObject[] LoadModels()
        {
            var models = new GameObject[BushModels.Length];
            for (int i = 0; i < BushModels.Length; i++)
            {
                models[i] = AssetDatabase.LoadAssetAtPath<GameObject>(BushModels[i]);
                if (models[i] == null)
                {
                    EditorUtility.DisplayDialog("숲 덤불 배치", $"덤불 모델이 없습니다: {BushModels[i]}\nUnity가 모델을 가져온 뒤 다시 실행하세요.", "확인");
                    return null;
                }
            }
            return models;
        }

        /// <summary>
        /// 이전 덤불 묶음을 지우고 빈 묶음을 만든다.
        /// </summary>
        private static Transform RecreateGroup(Transform environment)
        {
            Transform old = environment.Find(GroupName);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);

            var group = new GameObject(GroupName);
            group.transform.SetParent(environment, false);
            Undo.RegisterCreatedObjectUndo(group, "숲 덤불 배치");
            return group.transform;
        }

        /// <summary>
        /// 무리 중심을 고르고 그 둘레에 덤불을 심는다. 심은 수를 반환한다.
        /// </summary>
        private static int Plant(Transform group, GameObject[] models, List<Terrain> terrains, Bounds playArea, List<Vector3> markers)
        {
            var random = new System.Random(Seed);
            Dictionary<Vector2Int, List<Vector2>> trees = CollectTrees(terrains);
            var clusterCenters = new List<Vector2>();
            var bushes = new List<Vector2>();

            float minX = playArea.min.x + EdgeMargin, maxX = playArea.max.x - EdgeMargin;
            float minZ = playArea.min.z + EdgeMargin, maxZ = playArea.max.z - EdgeMargin;
            int attempts = ClusterTarget * MaxAttemptsPerCluster;

            for (int i = 0; i < attempts && clusterCenters.Count < ClusterTarget; i++)
            {
                var center = new Vector2(Range(random, minX, maxX), Range(random, minZ, maxZ));
                if (IsNear(clusterCenters, center, ClusterSpacing))
                    continue;
                if (!IsPlantable(center, terrains, trees, markers, bushes, out _))
                    continue;

                clusterCenters.Add(center);
                int count = random.Next(MinPerCluster, MaxPerCluster + 1);
                for (int k = 0, tries = 0; k < count && tries < count * 10; tries++)
                {
                    Vector2 point = k == 0 ? center : center + RandomInCircle(random, ClusterRadius);
                    if (point.x < minX || point.x > maxX || point.y < minZ || point.y > maxZ)
                        continue;
                    if (!IsPlantable(point, terrains, trees, markers, bushes, out float height))
                        continue;

                    GameObject model = models[random.Next(models.Length)];
                    CreateBush(group, model, new Vector3(point.x, height - SinkDepth, point.y),
                        Quaternion.Euler(0f, Range(random, 0f, 360f), 0f), Range(random, MinScale, MaxScale));
                    bushes.Add(point);
                    k++;
                }
            }
            return bushes.Count;
        }

        /// <summary>
        /// 그 자리에 덤불을 심을 수 있는지 확인하고 땅 높이를 구한다.
        /// (Terrain 위, 경사 제한 안, 다른 덤불·나무·스폰 지점에서 떨어진 곳)
        /// </summary>
        private static bool IsPlantable(Vector2 point, List<Terrain> terrains, Dictionary<Vector2Int, List<Vector2>> trees,
            List<Vector3> markers, List<Vector2> bushes, out float height)
        {
            height = 0f;
            if (!TrySampleGround(point, terrains, out height, out float slope) || slope > MaxSlope)
                return false;
            if (IsNear(bushes, point, BushSpacing) || IsNearTree(trees, point))
                return false;
            foreach (Vector3 marker in markers)
            {
                if (Vector2.Distance(point, new Vector2(marker.x, marker.z)) < MarkerClearance)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 그 자리를 덮는 Terrain에서 땅 높이와 경사(도)를 읽는다.
        /// </summary>
        private static bool TrySampleGround(Vector2 point, List<Terrain> terrains, out float height, out float slope)
        {
            foreach (Terrain terrain in terrains)
            {
                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                float u = (point.x - origin.x) / size.x;
                float v = (point.y - origin.z) / size.z;
                if (u < 0f || u > 1f || v < 0f || v > 1f)
                    continue;

                var world = new Vector3(point.x, 0f, point.y);
                height = terrain.SampleHeight(world) + origin.y;
                slope = terrain.terrainData.GetSteepness(u, v);
                return true;
            }
            height = 0f;
            slope = 0f;
            return false;
        }

        /// <summary>
        /// 덤불 하나를 모델 프리팹 인스턴스로 놓고 변신 사물로 등록한다.
        /// </summary>
        private static void CreateBush(Transform group, GameObject model, Vector3 position, Quaternion rotation, float scale)
        {
            var bush = (GameObject)PrefabUtility.InstantiatePrefab(model, group);
            bush.name = $"{model.name.Replace("SM_", string.Empty)} ({group.childCount})";
            bush.transform.SetPositionAndRotation(position, rotation);
            bush.transform.localScale = Vector3.one * scale;
            Undo.RegisterCreatedObjectUndo(bush, "숲 덤불 배치");
            PropRegistrar.Register(bush, isWall: false, absorbChildren: false);
        }

        /// <summary>
        /// 씬의 켜진 Terrain을 모은다.
        /// </summary>
        private static List<Terrain> FindTerrains(Scene scene)
        {
            var result = new List<Terrain>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>())
                {
                    if (terrain.terrainData != null)
                        result.Add(terrain);
                }
            }
            return result;
        }

        /// <summary>
        /// 피해야 할 지점(플레이어 스폰, 전원 장치 후보)의 위치를 모은다.
        /// </summary>
        private static List<Vector3> CollectMarkers(Scene scene)
        {
            var result = new List<Vector3>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (SpawnPoint spawn in root.GetComponentsInChildren<SpawnPoint>(true))
                    result.Add(spawn.transform.position);
                foreach (GeneratorSpawnPoint generator in root.GetComponentsInChildren<GeneratorSpawnPoint>(true))
                    result.Add(generator.transform.position);
            }
            return result;
        }

        /// <summary>
        /// Terrain 나무 밑동의 XZ 위치를 격자 칸별로 모은다. (가까운 나무를 빨리 찾기 위함)
        /// </summary>
        private static Dictionary<Vector2Int, List<Vector2>> CollectTrees(List<Terrain> terrains)
        {
            var cells = new Dictionary<Vector2Int, List<Vector2>>();
            foreach (Terrain terrain in terrains)
            {
                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                foreach (TreeInstance tree in terrain.terrainData.treeInstances)
                {
                    var point = new Vector2(origin.x + tree.position.x * size.x, origin.z + tree.position.z * size.z);
                    Vector2Int cell = CellOf(point);
                    if (!cells.TryGetValue(cell, out List<Vector2> list))
                        cells[cell] = list = new List<Vector2>();
                    list.Add(point);
                }
            }
            return cells;
        }

        /// <summary>
        /// 주변 격자 칸에 나무 밑동이 가까이 있는지 확인한다.
        /// </summary>
        private static bool IsNearTree(Dictionary<Vector2Int, List<Vector2>> trees, Vector2 point)
        {
            Vector2Int cell = CellOf(point);
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (trees.TryGetValue(cell + new Vector2Int(x, y), out List<Vector2> list) && IsNear(list, point, TreeClearance))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 나무 격자 칸 번호.
        /// </summary>
        private static Vector2Int CellOf(Vector2 point)
        {
            return new Vector2Int(Mathf.FloorToInt(point.x / TreeCellSize), Mathf.FloorToInt(point.y / TreeCellSize));
        }

        /// <summary>
        /// 목록에 distance보다 가까운 점이 있는지 확인한다.
        /// </summary>
        private static bool IsNear(List<Vector2> points, Vector2 point, float distance)
        {
            float sqr = distance * distance;
            foreach (Vector2 other in points)
            {
                if ((other - point).sqrMagnitude < sqr)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// min~max 사이의 난수.
        /// </summary>
        private static float Range(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        /// <summary>
        /// 반지름 radius 원 안의 난수 위치. (최소 BushSpacing만큼은 떨어뜨린다)
        /// </summary>
        private static Vector2 RandomInCircle(System.Random random, float radius)
        {
            float angle = Range(random, 0f, Mathf.PI * 2f);
            float distance = Range(random, BushSpacing, radius);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        }
    }
}
