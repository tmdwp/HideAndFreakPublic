using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuardVsMannequin.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>숲 맵의 산 지형을 걸어 다닐 수 있도록 Terrain 여러 장을 한 격자로 합쳐 높이·경사를 다듬는다.</summary>
    public static class ForestTerrainShaper
    {
        private const string ForestScenePath = "Assets/_Project/Scenes/Custom/Map_Forest.unity";
        private const string OutputFolder = "Temp/ForestTerrain";

        /// <summary>캐릭터·NavMesh 한계(45°)보다 여유 있게 잡은 다듬은 뒤의 최대 경사(°).</summary>
        private const float TargetSlope = 32f;
        /// <summary>놀이 영역 바닥에서 최대로 솟을 높이(m).</summary>
        private const float MaxRise = 20f;
        /// <summary>놀이 영역 밖에서 원래 지형으로 돌아가는 거리(m).</summary>
        private const float Margin = 40f;
        /// <summary>바닥 높이로 쓸 낮은 쪽 비율.</summary>
        private const float BasePercentile = 0.05f;
        /// <summary>8방향 격자 거리가 실제 거리보다 길게 잡히는 정도(최대 약 8%)를 보정한다.</summary>
        private const float ChamferError = 1.09f;
        /// <summary>지면 높이를 따라 옮길 물체의 최대 높이(지면에서 m).</summary>
        private const float FollowHeight = 8f;

        /// <summary>Terrain 여러 장을 합친 높이 격자(덮이지 않은 칸은 NaN).</summary>
        private sealed class HeightGrid
        {
            public Vector2 origin;
            public float cell;
            public int width, depth;
            public float[,] heights;

            /// <summary>칸 번호로 놀이 영역과의 거리를 재려고 월드 XZ 위치로 바꾼다.</summary>
            public Vector2 WorldOf(int x, int z) => origin + new Vector2(x * cell, z * cell);
        }

        /// <summary>지형을 크게 바꾸는 작업이라 백업 위치를 알리고 확인을 받은 뒤 숲 맵을 직접 열어 진행한다.</summary>
        [MenuItem(EditorMenus.Root + "맵 생성/숲 지형 다듬기 (Map_Forest)")]
        private static void ShapeFromMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ForestScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                scene = EditorSceneManager.OpenScene(ForestScenePath, OpenSceneMode.Single);
            }

            GameObject boundary = MapOptimizer.FindByPath(scene, MapOptimizer.BoundaryName);
            List<Terrain> terrains = FindTerrains(scene);
            if (boundary == null || terrains.Count == 0)
            {
                EditorUtility.DisplayDialog("숲 지형 다듬기", $"'{MapOptimizer.BoundaryName}' 또는 Terrain을 찾지 못했습니다.", "확인");
                return;
            }
            if (!TryBuildGrid(terrains, out HeightGrid grid, out string problem))
            {
                EditorUtility.DisplayDialog("숲 지형 다듬기", problem, "확인");
                return;
            }

            Bounds playArea = MapOptimizer.GetBoundaryBounds(boundary.transform);
            if (!EditorUtility.DisplayDialog("숲 지형 다듬기",
                    $"놀이 영역({playArea.size.x:F0}×{playArea.size.z:F0}m)의 지형을 최대 경사 {TargetSlope}°, 바닥에서 최대 {MaxRise}m로 다듬고 " +
                    "나무·덤불·지점을 새 지면에 맞춘 뒤 NavMesh를 다시 굽습니다.\n" +
                    $"처리 전 Terrain 데이터는 {OutputFolder}/Backup에 복사합니다. 계속할까요?", "다듬기", "취소"))
                return;

            BackupTerrainData(terrains);
            Shape(scene, terrains, grid, playArea);
        }

        /// <summary>지형만 바꾸면 물체가 뜨거나 묻히고 NavMesh가 어긋나므로 물체 이동과 최적화까지 한 번에 한다.</summary>
        private static void Shape(Scene scene, List<Terrain> terrains, HeightGrid grid, Bounds playArea)
        {
            RectInt workRegion = WorkRegion(grid, playArea);
            float[,] originalHeights = Crop(grid.heights, workRegion);
            float[,] weights = Weights(grid, workRegion, playArea);
            float baseHeight = Percentile(originalHeights, weights, BasePercentile);

            float[,] shapedHeights = Compress(originalHeights, baseHeight);
            LimitSlope(shapedHeights, grid.cell, Mathf.Tan(TargetSlope * Mathf.Deg2Rad) / ChamferError);
            Smooth(shapedHeights, passes: 2);
            float[,] finalHeights = Blend(originalHeights, shapedHeights, weights);

            string beforeSummary = Describe("다듬기 전", originalHeights, weights, grid.cell);
            string afterSummary = Describe("다듬은 뒤", finalHeights, weights, grid.cell);
            Directory.CreateDirectory(OutputFolder);
            WriteSlopeMap(Path.Combine(OutputFolder, "slope_before.png"), originalHeights, weights, grid.cell);
            WriteSlopeMap(Path.Combine(OutputFolder, "slope_after.png"), finalHeights, weights, grid.cell);

            // 물체의 원래 지면 높이를 먼저 기록하고, 지형을 바꾼 뒤 그 차이만큼 옮긴다.
            List<(Transform target, float oldGround)> followers = CollectFollowers(scene, terrains, playArea);
            int changedTileCount = WriteBack(terrains, grid, workRegion, finalHeights);
            int movedTreeCount = RegroundTrees(terrains);
            int movedObjectCount = MoveFollowers(followers, terrains);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            MapOptimizer.Optimize(scene, indoor: false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            string report = $"바닥 높이 {baseHeight:F1}m, 최대 경사 {TargetSlope}°, 최대 솟음 {MaxRise}m, 영역 밖 되돌림 {Margin}m\n" +
                            $"바뀐 Terrain {changedTileCount}장, 다시 세운 나무 {movedTreeCount}그루, 지면 따라 옮긴 물체 {movedObjectCount}개\n\n" +
                            beforeSummary + "\n" + afterSummary + "\n" +
                            $"경사 지도: {OutputFolder}/slope_before.png, slope_after.png (초록 ≤30°, 노랑 ≤45°, 빨강 >45°, 놀이 영역 밖은 어둡게)\n" +
                            $"되돌리기: {OutputFolder}/Backup의 .asset을 Map_Forest 폴더에 덮어쓰거나 git으로 Terrain 데이터와 씬을 되돌린다.";
            File.WriteAllText(Path.Combine(OutputFolder, "report.txt"), report);
            Debug.Log("[ForestTerrainShaper] 숲 지형 다듬기 완료\n" + report);
            EditorUtility.DisplayDialog("숲 지형 다듬기", $"완료했습니다. 결과는 {OutputFolder}/report.txt와 경사 지도를 보세요.\n\n{beforeSummary}\n{afterSummary}", "확인");
        }

        #region 높이 격자

        /// <summary>높이를 읽을 수 있고 게임에서 보이는 Terrain만 모은다.</summary>
        private static List<Terrain> FindTerrains(Scene scene)
        {
            return scene.GetRootGameObjects()
                .SelectMany(rootObject => rootObject.GetComponentsInChildren<Terrain>())
                .Where(terrain => terrain.terrainData != null)
                .ToList();
        }

        /// <summary>조각마다 따로 다듬으면 경계에 틈이 생기므로 모든 Terrain을 하나의 격자로 합친다.</summary>
        private static bool TryBuildGrid(List<Terrain> terrains, out HeightGrid grid, out string problem)
        {
            grid = null;
            TerrainData firstData = terrains[0].terrainData;
            float cellSize = firstData.size.x / (firstData.heightmapResolution - 1);
            foreach (Terrain terrain in terrains)
            {
                TerrainData terrainData = terrain.terrainData;
                float cellX = terrainData.size.x / (terrainData.heightmapResolution - 1);
                float cellZ = terrainData.size.z / (terrainData.heightmapResolution - 1);
                if (Mathf.Abs(cellX - cellSize) > 0.001f || Mathf.Abs(cellZ - cellSize) > 0.001f)
                {
                    problem = $"Terrain '{terrain.name}'의 높이 격자 간격이 다른 Terrain과 달라 합칠 수 없습니다.";
                    return false;
                }
            }

            var gridOrigin = new Vector2(terrains.Min(terrain => terrain.GetPosition().x), terrains.Min(terrain => terrain.GetPosition().z));
            float maxX = terrains.Max(terrain => terrain.GetPosition().x + terrain.terrainData.size.x);
            float maxZ = terrains.Max(terrain => terrain.GetPosition().z + terrain.terrainData.size.z);
            grid = new HeightGrid
            {
                origin = gridOrigin,
                cell = cellSize,
                width = Mathf.RoundToInt((maxX - gridOrigin.x) / cellSize) + 1,
                depth = Mathf.RoundToInt((maxZ - gridOrigin.y) / cellSize) + 1
            };
            grid.heights = new float[grid.width, grid.depth];
            for (int x = 0; x < grid.width; x++)
                for (int z = 0; z < grid.depth; z++)
                    grid.heights[x, z] = float.NaN;

            foreach (Terrain terrain in terrains)
            {
                if (!TryOffset(grid, terrain, out Vector2Int cellOffset))
                {
                    problem = $"Terrain '{terrain.name}'이 다른 Terrain과 격자에 맞게 붙어 있지 않습니다.";
                    return false;
                }
                TerrainData terrainData = terrain.terrainData;
                int resolution = terrainData.heightmapResolution;
                float[,] normalizedHeights = terrainData.GetHeights(0, 0, resolution, resolution); // [z, x]
                float terrainBaseY = terrain.GetPosition().y;
                for (int z = 0; z < resolution; z++)
                    for (int x = 0; x < resolution; x++)
                        grid.heights[cellOffset.x + x, cellOffset.y + z] = terrainBaseY + normalizedHeights[z, x] * terrainData.size.y;
            }
            problem = null;
            return true;
        }

        /// <summary>조각을 격자에 놓으려고 첫 높이 칸이 격자의 몇 번째 칸인지 구한다.</summary>
        private static bool TryOffset(HeightGrid grid, Terrain terrain, out Vector2Int offset)
        {
            Vector3 terrainPosition = terrain.GetPosition();
            float cellOffsetX = (terrainPosition.x - grid.origin.x) / grid.cell;
            float cellOffsetZ = (terrainPosition.z - grid.origin.y) / grid.cell;
            offset = new Vector2Int(Mathf.RoundToInt(cellOffsetX), Mathf.RoundToInt(cellOffsetZ));
            return Mathf.Abs(cellOffsetX - offset.x) < 0.01f && Mathf.Abs(cellOffsetZ - offset.y) < 0.01f;
        }

        /// <summary>배경 산 모양이 바뀌지 않도록 놀이 영역과 되돌림 거리만 처리한다.</summary>
        private static RectInt WorkRegion(HeightGrid grid, Bounds playArea)
        {
            int startX = Mathf.Clamp(Mathf.FloorToInt((playArea.min.x - Margin - grid.origin.x) / grid.cell), 0, grid.width - 1);
            int startZ = Mathf.Clamp(Mathf.FloorToInt((playArea.min.z - Margin - grid.origin.y) / grid.cell), 0, grid.depth - 1);
            int endX = Mathf.Clamp(Mathf.CeilToInt((playArea.max.x + Margin - grid.origin.x) / grid.cell), 0, grid.width - 1);
            int endZ = Mathf.Clamp(Mathf.CeilToInt((playArea.max.z + Margin - grid.origin.y) / grid.cell), 0, grid.depth - 1);
            return new RectInt(startX, startZ, endX - startX + 1, endZ - startZ + 1);
        }

        /// <summary>NaN이 계산에 섞이면 결과 전체가 NaN이 되므로 처리 범위를 복사하며 빈 칸을 메운다.</summary>
        private static float[,] Crop(float[,] heights, RectInt region)
        {
            var croppedHeights = new float[region.width, region.height];
            float fallbackHeight = 0f;
            for (int x = 0; x < region.width; x++)
            {
                for (int z = 0; z < region.height; z++)
                {
                    float height = heights[region.x + x, region.y + z];
                    if (float.IsNaN(height))
                        height = fallbackHeight;
                    croppedHeights[x, z] = height;
                    fallbackHeight = height;
                }
            }
            return croppedHeights;
        }

        /// <summary>놀이 영역 경계에서 지형이 뚝 끊기지 않도록 다듬은 지형 비율을 smoothstep으로 줄인다.</summary>
        private static float[,] Weights(HeightGrid grid, RectInt region, Bounds playArea)
        {
            var weights = new float[region.width, region.height];
            for (int x = 0; x < region.width; x++)
            {
                for (int z = 0; z < region.height; z++)
                {
                    Vector2 worldPoint = grid.WorldOf(region.x + x, region.y + z);
                    float outsideX = Mathf.Max(0f, Mathf.Max(playArea.min.x - worldPoint.x, worldPoint.x - playArea.max.x));
                    float outsideZ = Mathf.Max(0f, Mathf.Max(playArea.min.z - worldPoint.y, worldPoint.y - playArea.max.z));
                    float fadeProgress = Mathf.Clamp01(Mathf.Sqrt(outsideX * outsideX + outsideZ * outsideZ) / Margin);
                    weights[x, z] = 1f - fadeProgress * fadeProgress * (3f - 2f * fadeProgress);
                }
            }
            return weights;
        }

        #endregion

        #region 지형 처리

        /// <summary>
        /// 가장 낮은 칸을 바닥으로 쓰면 구덩이 하나에 좌우되므로, 놀이 영역 안(가중치 1) 칸 높이의 아래쪽 비율 값을 바닥으로 쓴다.
        /// </summary>
        private static float Percentile(float[,] heights, float[,] weights, float ratio)
        {
            var insideHeights = new List<float>();
            for (int x = 0; x < heights.GetLength(0); x++)
                for (int z = 0; z < heights.GetLength(1); z++)
                    if (weights[x, z] >= 0.999f)
                        insideHeights.Add(heights[x, z]);
            if (insideHeights.Count == 0)
                return 0f;
            insideHeights.Sort();
            return insideHeights[Mathf.Clamp(Mathf.FloorToInt(insideHeights.Count * ratio), 0, insideHeights.Count - 1)];
        }

        /// <summary>높이를 잘라 평평한 고원이 생기지 않도록 tanh 곡선으로 부드럽게 누른다.</summary>
        private static float[,] Compress(float[,] heights, float baseHeight)
        {
            int gridWidth = heights.GetLength(0), gridDepth = heights.GetLength(1);
            var compressedHeights = new float[gridWidth, gridDepth];
            for (int x = 0; x < gridWidth; x++)
            {
                for (int z = 0; z < gridDepth; z++)
                {
                    float rise = heights[x, z] - baseHeight;
                    compressedHeights[x, z] = rise > 0f ? baseHeight + MaxRise * Tanh(rise / MaxRise) : heights[x, z];
                }
            }
            return compressedHeights;
        }

        /// <summary>멀리 떨어진 두 점 사이 경사도 제한되도록 8방향 거리 변환으로 깎는다.</summary>
        private static void LimitSlope(float[,] heights, float cell, float maxGradient)
        {
            int gridWidth = heights.GetLength(0), gridDepth = heights.GetLength(1);
            float straightRise = maxGradient * cell;
            float diagonalRise = straightRise * Mathf.Sqrt(2f);

            for (int z = 0; z < gridDepth; z++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    float limitedHeight = heights[x, z];
                    if (x > 0) limitedHeight = Mathf.Min(limitedHeight, heights[x - 1, z] + straightRise);
                    if (z > 0)
                    {
                        limitedHeight = Mathf.Min(limitedHeight, heights[x, z - 1] + straightRise);
                        if (x > 0) limitedHeight = Mathf.Min(limitedHeight, heights[x - 1, z - 1] + diagonalRise);
                        if (x < gridWidth - 1) limitedHeight = Mathf.Min(limitedHeight, heights[x + 1, z - 1] + diagonalRise);
                    }
                    heights[x, z] = limitedHeight;
                }
            }
            for (int z = gridDepth - 1; z >= 0; z--)
            {
                for (int x = gridWidth - 1; x >= 0; x--)
                {
                    float limitedHeight = heights[x, z];
                    if (x < gridWidth - 1) limitedHeight = Mathf.Min(limitedHeight, heights[x + 1, z] + straightRise);
                    if (z < gridDepth - 1)
                    {
                        limitedHeight = Mathf.Min(limitedHeight, heights[x, z + 1] + straightRise);
                        if (x < gridWidth - 1) limitedHeight = Mathf.Min(limitedHeight, heights[x + 1, z + 1] + diagonalRise);
                        if (x > 0) limitedHeight = Mathf.Min(limitedHeight, heights[x - 1, z + 1] + diagonalRise);
                    }
                    heights[x, z] = limitedHeight;
                }
            }
        }

        /// <summary>경사 제한이 남긴 모서리를 경사를 키우지 않는 3×3 평균으로 부드럽게 한다.</summary>
        private static void Smooth(float[,] heights, int passes)
        {
            int gridWidth = heights.GetLength(0), gridDepth = heights.GetLength(1);
            var buffer = new float[gridWidth, gridDepth];
            for (int pass = 0; pass < passes; pass++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    for (int z = 0; z < gridDepth; z++)
                    {
                        float heightSum = 0f;
                        int neighborCount = 0;
                        for (int offsetX = -1; offsetX <= 1; offsetX++)
                        {
                            for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
                            {
                                int neighborX = x + offsetX, neighborZ = z + offsetZ;
                                if (neighborX < 0 || neighborZ < 0 || neighborX >= gridWidth || neighborZ >= gridDepth)
                                    continue;
                                heightSum += heights[neighborX, neighborZ];
                                neighborCount++;
                            }
                        }
                        buffer[x, z] = heightSum / neighborCount;
                    }
                }
                System.Array.Copy(buffer, heights, buffer.Length);
            }
        }

        /// <summary>
        /// 놀이 영역 밖 배경 산은 원래 모양을 유지해야 하므로 가중치에 따라 원래 지형과 다듬은 지형을 섞는다.
        /// </summary>
        private static float[,] Blend(float[,] original, float[,] shaped, float[,] weights)
        {
            int gridWidth = original.GetLength(0), gridDepth = original.GetLength(1);
            var blendedHeights = new float[gridWidth, gridDepth];
            for (int x = 0; x < gridWidth; x++)
                for (int z = 0; z < gridDepth; z++)
                    blendedHeights[x, z] = Mathf.Lerp(original[x, z], shaped[x, z], weights[x, z]);
            return blendedHeights;
        }

        /// <summary>Mathf에 tanh가 없어 System.Math를 float로 감싼다.</summary>
        private static float Tanh(float value) => (float)System.Math.Tanh(value);

        #endregion

        #region 되쓰기·물체 옮기기

        /// <summary>이음매가 맞도록 같은 격자 값을 쓰고, 바뀐 칸이 없는 조각은 저장 비용을 아끼려고 건너뛴다.</summary>
        private static int WriteBack(List<Terrain> terrains, HeightGrid grid, RectInt work, float[,] final)
        {
            int changedTileCount = 0;
            foreach (Terrain terrain in terrains)
            {
                TryOffset(grid, terrain, out Vector2Int cellOffset);
                TerrainData terrainData = terrain.terrainData;
                int resolution = terrainData.heightmapResolution;
                var tileRect = new RectInt(cellOffset.x, cellOffset.y, resolution, resolution);
                if (!tileRect.Overlaps(work))
                    continue;

                float[,] normalizedHeights = terrainData.GetHeights(0, 0, resolution, resolution);
                float terrainBaseY = terrain.GetPosition().y;
                bool hasChange = false;
                for (int z = 0; z < resolution; z++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        int workX = cellOffset.x + x - work.x, workZ = cellOffset.y + z - work.y;
                        if (workX < 0 || workZ < 0 || workX >= work.width || workZ >= work.height)
                            continue;
                        float normalizedHeight = Mathf.Clamp01((final[workX, workZ] - terrainBaseY) / terrainData.size.y);
                        if (Mathf.Abs(normalizedHeight - normalizedHeights[z, x]) > 1e-6f)
                        {
                            normalizedHeights[z, x] = normalizedHeight;
                            hasChange = true;
                        }
                    }
                }
                if (!hasChange)
                    continue;
                Undo.RegisterCompleteObjectUndo(terrainData, "숲 지형 다듬기");
                terrainData.SetHeights(0, 0, normalizedHeights);
                EditorUtility.SetDirty(terrainData);
                changedTileCount++;
            }
            AssetDatabase.SaveAssets();
            return changedTileCount;
        }

        /// <summary>Terrain 나무는 높이를 비율로 저장해 지형을 바꾸면 뜨거나 묻히므로 새 지면에 다시 세운다.</summary>
        private static int RegroundTrees(List<Terrain> terrains)
        {
            int movedCount = 0;
            foreach (Terrain terrain in terrains)
            {
                TerrainData terrainData = terrain.terrainData;
                TreeInstance[] trees = terrainData.treeInstances;
                bool hasChange = false;
                for (int i = 0; i < trees.Length; i++)
                {
                    float normalizedGround = terrainData.GetInterpolatedHeight(trees[i].position.x, trees[i].position.z) / terrainData.size.y;
                    if (Mathf.Abs(trees[i].position.y - normalizedGround) < 1e-5f)
                        continue;
                    trees[i].position.y = normalizedGround;
                    hasChange = true;
                    movedCount++;
                }
                if (hasChange)
                {
                    terrainData.treeInstances = trees;
                    EditorUtility.SetDirty(terrainData);
                }
            }
            return movedCount;
        }

        /// <summary>바꾼 뒤에는 원래 지면 높이를 알 수 없어 옮길 물체와 지면 높이를 미리 기록한다.</summary>
        private static List<(Transform, float)> CollectFollowers(Scene scene, List<Terrain> terrains, Bounds playArea)
        {
            var followers = new List<(Transform, float)>();
            Bounds followArea = playArea;
            followArea.Expand(new Vector3(Margin * 2f, 0f, Margin * 2f));

            // 계층을 재귀로 돌며 대상 물체를 모은다.
            void VisitNode(Transform node)
            {
                if (node.name == MapOptimizer.BoundaryName || node.GetComponent<Terrain>() != null || node.GetComponent<Camera>() != null)
                    return;
                if (node.TryGetComponent(out Light nodeLight) && nodeLight.type == LightType.Directional)
                    return;
                if (IsGroup(node))
                {
                    foreach (Transform child in node)
                        VisitNode(child);
                    return;
                }

                Vector3 nodePosition = node.position;
                if (nodePosition.x < followArea.min.x || nodePosition.x > followArea.max.x || nodePosition.z < followArea.min.z || nodePosition.z > followArea.max.z)
                    return;
                if (!TrySampleGround(terrains, nodePosition, out float groundHeight) || nodePosition.y - groundHeight > FollowHeight || groundHeight - nodePosition.y > FollowHeight)
                    return;
                followers.Add((node, groundHeight));
            }

            foreach (GameObject rootObject in scene.GetRootGameObjects())
                VisitNode(rootObject.transform);
            return followers;
        }

        /// <summary>옮기는 단위를 정하려고 자식만 담는 빈 묶음인지 구별한다.</summary>
        private static bool IsGroup(Transform node)
        {
            if (node.childCount == 0)
                return false;
            foreach (Component component in node.GetComponents<Component>())
            {
                if (component is Transform || component is MapInfo || component is Unity.AI.Navigation.NavMeshSurface)
                    continue;
                return false;
            }
            return true;
        }

        /// <summary>받침 위 물건처럼 떠 있던 높이를 유지하도록 지면 높이 차이만큼만 옮긴다.</summary>
        private static int MoveFollowers(List<(Transform target, float oldGround)> followers, List<Terrain> terrains)
        {
            int movedCount = 0;
            foreach ((Transform target, float oldGround) in followers)
            {
                if (target == null || !TrySampleGround(terrains, target.position, out float newGround))
                    continue;
                float heightDelta = newGround - oldGround;
                if (Mathf.Abs(heightDelta) < 0.005f)
                    continue;
                Undo.RecordObject(target, "숲 지형 다듬기");
                target.position += Vector3.up * heightDelta;
                movedCount++;
            }
            return movedCount;
        }

        /// <summary>Terrain이 여러 장이라 점을 덮는 Terrain을 찾아 지면 높이를 읽는다.</summary>
        private static bool TrySampleGround(List<Terrain> terrains, Vector3 point, out float height)
        {
            foreach (Terrain terrain in terrains)
            {
                Vector3 terrainOrigin = terrain.GetPosition();
                Vector3 terrainSize = terrain.terrainData.size;
                if (point.x < terrainOrigin.x || point.z < terrainOrigin.z || point.x > terrainOrigin.x + terrainSize.x || point.z > terrainOrigin.z + terrainSize.z)
                    continue;
                height = terrain.SampleHeight(point) + terrainOrigin.y;
                return true;
            }
            height = 0f;
            return false;
        }

        #endregion

        #region 백업·보고

        /// <summary>결과가 마음에 들지 않을 때 git 없이도 되돌리도록 Terrain 데이터를 Temp에 백업한다.</summary>
        private static void BackupTerrainData(List<Terrain> terrains)
        {
            AssetDatabase.SaveAssets();
            string backupFolder = Path.Combine(OutputFolder, "Backup");
            Directory.CreateDirectory(backupFolder);
            foreach (string assetPath in terrains.Select(terrain => AssetDatabase.GetAssetPath(terrain.terrainData)).Where(path => !string.IsNullOrEmpty(path)).Distinct())
                File.Copy(assetPath, Path.Combine(backupFolder, Path.GetFileName(assetPath)), overwrite: true);
        }

        /// <summary>보고서와 경사 지도가 같은 기준을 쓰도록 칸의 경사를 한곳에서 구한다.</summary>
        private static float SlopeAt(float[,] heights, int x, int z, float cell)
        {
            int gridWidth = heights.GetLength(0), gridDepth = heights.GetLength(1);
            float gradientX = (heights[Mathf.Min(x + 1, gridWidth - 1), z] - heights[Mathf.Max(x - 1, 0), z]) / (cell * (Mathf.Min(x + 1, gridWidth - 1) - Mathf.Max(x - 1, 0)));
            float gradientZ = (heights[x, Mathf.Min(z + 1, gridDepth - 1)] - heights[x, Mathf.Max(z - 1, 0)]) / (cell * (Mathf.Min(z + 1, gridDepth - 1) - Mathf.Max(z - 1, 0)));
            return Mathf.Atan(Mathf.Sqrt(gradientX * gradientX + gradientZ * gradientZ)) * Mathf.Rad2Deg;
        }

        /// <summary>다듬기 전후를 숫자로 비교하도록 높이 범위와 경사 분포를 한 줄로 정리한다.</summary>
        private static string Describe(string label, float[,] heights, float[,] weights, float cell)
        {
            float minHeight = float.MaxValue, maxHeight = float.MinValue, steepestSlope = 0f;
            int totalCount = 0, over35Count = 0, over45Count = 0;
            for (int x = 0; x < heights.GetLength(0); x++)
            {
                for (int z = 0; z < heights.GetLength(1); z++)
                {
                    if (weights[x, z] < 0.999f)
                        continue;
                    float slope = SlopeAt(heights, x, z, cell);
                    minHeight = Mathf.Min(minHeight, heights[x, z]);
                    maxHeight = Mathf.Max(maxHeight, heights[x, z]);
                    steepestSlope = Mathf.Max(steepestSlope, slope);
                    totalCount++;
                    if (slope > 35f) over35Count++;
                    if (slope > 45f) over45Count++;
                }
            }
            if (totalCount == 0)
                return $"{label}: 놀이 영역 안 칸 없음";
            return $"{label}: 높이 {minHeight:F1}~{maxHeight:F1}m (차 {maxHeight - minHeight:F1}m), 가장 가파른 곳 {steepestSlope:F0}°, " +
                   $"35° 넘는 곳 {100f * over35Count / totalCount:F1}%, 45° 넘는 곳(못 오름) {100f * over45Count / totalCount:F1}%";
        }

        /// <summary>어디가 가파른지 눈으로 보도록 경사를 색으로 칠한 PNG를 남긴다.</summary>
        private static void WriteSlopeMap(string path, float[,] heights, float[,] weights, float cell)
        {
            int gridWidth = heights.GetLength(0), gridDepth = heights.GetLength(1);
            float minHeight = float.MaxValue, maxHeight = float.MinValue;
            foreach (float height in heights)
            {
                minHeight = Mathf.Min(minHeight, height);
                maxHeight = Mathf.Max(maxHeight, height);
            }
            var texture = new Texture2D(gridWidth, gridDepth, TextureFormat.RGB24, false);
            try
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    for (int z = 0; z < gridDepth; z++)
                    {
                        float slope = SlopeAt(heights, x, z, cell);
                        Color slopeTint = slope <= 30f ? new Color(0.2f, 0.75f, 0.3f) : slope <= 45f ? new Color(0.95f, 0.8f, 0.2f) : new Color(0.9f, 0.15f, 0.1f);
                        float brightness = Mathf.Lerp(0.45f, 1f, Mathf.InverseLerp(minHeight, maxHeight, heights[x, z]));
                        if (weights[x, z] < 0.999f)
                            brightness *= 0.35f;
                        texture.SetPixel(x, z, slopeTint * brightness);
                    }
                }
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        #endregion
    }
}
