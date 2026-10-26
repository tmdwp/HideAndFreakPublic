using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuardVsMannequin.Map;
using GuardVsMannequin.Props;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 생성된 맵 씬을 가볍게 만든다. (<see cref="MapSceneBuilder"/>가 만든 맵 대상, 수동으로 옮긴 배치는 유지)
    /// 1. 플레이어가 갈 수 없는 곳(투명 경계 밖 + 여유 거리)에 있는 환경 오브젝트와 꺼져 있는 오브젝트를 지운다.
    /// 2. 남은 환경에 정적 플래그를 준다: 가려질 수 있음(Occludee), 큰 불투명 메시는 가리개(Occluder), 정적 배칭.
    /// 3. 플레이 영역에 Occlusion Area를 두고 오클루전 컬링을 굽는다. (보이지 않는 오브젝트를 실행 중에 그리지 않음)
    /// 4. 총에 맞으면 튕겨나갈 변신 사물의 충돌체를 볼록으로 바꾼다(<see cref="PropColliderBaker"/>).
    /// </summary>
    public static class MapOptimizer
    {
        private const string EnvironmentPath = "Map/Environment";
        private const string BoundaryName = "Boundary (Invisible)";

        // 경계 밖이라도 창문 등으로 보일 수 있는 거리
        private const float IndoorMargin = 3f;
        private const float OutdoorMargin = 12f;

        // 가리개로 쓸 최소 크기(m)
        private const float OccluderMinSize = 1f;

        #region 메뉴

        [MenuItem(EditorMenus.Root + "맵 최적화/전체 맵 최적화")]
        private static void OptimizeAllMaps()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            foreach (MapCatalog.Entry map in MapCatalog.Maps.Where(m => !string.IsNullOrEmpty(m.sourceScenePath)))
                OptimizeSceneFile(map);
        }

        /// <summary>
        /// 열려 있는 맵 씬(직접 만든 맵 포함)을 최적화하고 저장한다. 먼저 씬을 저장해야 한다. (맵 제작 도우미의 "현재 맵 최적화")
        /// </summary>
        public static void OptimizeActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("맵 최적화", "씬을 먼저 저장하세요.", "확인");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // 목록에 없는 씬은 실내 맵으로 본다. (경계 밖 여유 거리가 짧음)
            MapCatalog.Entry entry = MapCatalog.FindByScenePath(scene.path);
            Optimize(scene, entry == null || entry.indoor);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        #endregion

        /// <summary>
        /// 맵 씬 파일을 열어 최적화하고 저장한다.
        /// </summary>
        private static void OptimizeSceneFile(MapCatalog.Entry map)
        {
            if (!File.Exists(map.ScenePath))
            {
                Debug.LogWarning($"[MapOptimizer] 맵 씬이 없어 건너뜁니다: {map.ScenePath} ('맵 생성'을 먼저 실행하세요)");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(map.ScenePath, OpenSceneMode.Single);
            Optimize(scene, map.indoor);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// 열려 있는 맵 씬을 최적화한다. 오클루전을 굽기 전에 씬이 저장된 경로를 가지고 있어야 한다.
        /// </summary>
        public static void Optimize(Scene scene, bool indoor)
        {
            GameObject environmentObject = FindByPath(scene, EnvironmentPath);
            GameObject boundaryObject = FindByPath(scene, BoundaryName);
            if (environmentObject == null || boundaryObject == null)
            {
                Debug.LogError($"[MapOptimizer] {scene.name}: '{EnvironmentPath}' 또는 '{BoundaryName}'을 찾을 수 없습니다. 맵 생성 도구로 만든 씬인지 확인하세요.");
                return;
            }

            // 실시간 조명만 쓰므로 GI 계산을 끈다. (에디터가 GI를 다시 계산하다 메모리가 부족해지는 것 방지)
            SceneLightingSetup.Apply(scene);

            Bounds playArea = GetBoundaryBounds(boundaryObject.transform);
            Bounds keepArea = playArea;
            float margin = indoor ? IndoorMargin : OutdoorMargin;
            keepArea.Expand(new Vector3(margin * 2f, margin * 2f, margin * 2f));

            Transform environment = environmentObject.transform;
            Transform mapRoot = environment.parent;
            int inactive = RemoveInactive(environment);
            int outside = RemoveOutside(environment, keepArea);
            int empty = RemoveEmpty(environment);
            int fixedBoxes = FixNegativeScaleBoxColliders(environment);
            int areaLights = RemoveAreaLights(mapRoot);
            int shadowLights = LimitLightShadows(mapRoot);
            int occluders = SetStaticFlags(environment);
            int physicsColliders = PropColliderBaker.BakeScene(scene);

            bool navMeshBaked = BakeNavMesh(scene, mapRoot);
            EnsureOcclusionArea(boundaryObject.transform, playArea);
            BakeOcclusion(scene);

            Debug.Log($"[MapOptimizer] {scene.name}: 꺼진 오브젝트 {inactive}개, 경계 밖 {outside}개, 빈 오브젝트 {empty}개 제거. " +
                      $"음수 크기 상자 충돌체 {fixedBoxes}개 교체, 튕겨나갈 사물 충돌체 {physicsColliders}개 볼록화, 영역 조명 {areaLights}개 제거, 그림자 끈 조명 {shadowLights}개. " +
                      $"NavMesh {(navMeshBaked ? "저장" : "실패")}, 가리개 {occluders}개로 오클루전 컬링을 구웠습니다.");
        }

        #region 조명·충돌체·NavMesh

        /// <summary>
        /// 영역(Area) 조명을 지운다. URP는 실시간 영역 조명을 지원하지 않아 빛을 내지 못하고 경고만 남기기 때문이다.
        /// URP 보조 컴포넌트(UniversalAdditionalLightData)가 Light에 의존하므로 먼저 지워야 Light를 지울 수 있다.
        /// </summary>
        private static int RemoveAreaLights(Transform mapRoot)
        {
            int removed = 0;
            foreach (Light target in mapRoot.GetComponentsInChildren<Light>(true))
            {
                if (target.type != LightType.Rectangle && target.type != LightType.Disc)
                    continue;
                if (target.TryGetComponent(out MapLight mapLight))
                    Object.DestroyImmediate(mapLight);
                if (target.TryGetComponent(out UniversalAdditionalLightData additionalData))
                    Object.DestroyImmediate(additionalData);
                Object.DestroyImmediate(target);
                // 지우지 못했으면(다른 의존 컴포넌트) 세지 않는다.
                if (target == null)
                    removed++;
            }
            return removed;
        }

        /// <summary>
        /// 맵 조명(방향광 제외)의 그림자를 끈다. 그림자를 내는 실시간 조명이 많으면 그림자 지도가 부족해 화질이 떨어지고 느려진다.
        /// (사냥 시간 손전등은 가드에 붙어 있어 영향받지 않는다)
        /// </summary>
        private static int LimitLightShadows(Transform mapRoot)
        {
            int changed = 0;
            foreach (Light target in mapRoot.GetComponentsInChildren<Light>(true))
            {
                if (target.type == LightType.Directional || target.shadows == LightShadows.None)
                    continue;
                target.shadows = LightShadows.None;
                changed++;
            }
            return changed;
        }

        /// <summary>
        /// 크기가 음수(거울 배치)인 상자 충돌체를 메시 충돌체로 바꾼다. 상자 충돌체는 음수 크기를 지원하지 않아 경고와 함께 모양이 틀어진다.
        /// </summary>
        private static int FixNegativeScaleBoxColliders(Transform environment)
        {
            int replaced = 0;
            foreach (BoxCollider box in environment.GetComponentsInChildren<BoxCollider>(true))
            {
                Vector3 scale = box.transform.lossyScale;
                if (scale.x >= 0f && scale.y >= 0f && scale.z >= 0f)
                    continue;

                MeshFilter filter = box.GetComponent<MeshFilter>();
                GameObject target = box.gameObject;
                Object.DestroyImmediate(box);
                if (filter != null && filter.sharedMesh != null)
                    target.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                replaced++;
            }
            return replaced;
        }

        /// <summary>
        /// 맵의 NavMesh를 에디터에서 구워 씬 폴더에 에셋으로 저장한다.
        /// 빌드에서는 읽기가 허용되지 않은 메시를 실행 중에 구울 수 없으므로, 서버는 이 데이터를 그대로 쓴다.
        /// </summary>
        private static bool BakeNavMesh(Scene scene, Transform mapRoot)
        {
            NavMeshSurface surface = mapRoot != null ? mapRoot.GetComponent<NavMeshSurface>() : null;
            if (surface == null)
            {
                Debug.LogWarning($"[MapOptimizer] {scene.name}: Map에 NavMeshSurface가 없어 NavMesh를 굽지 않았습니다.");
                return false;
            }

            Physics.SyncTransforms();
            surface.RemoveData();
            surface.BuildNavMesh();
            if (surface.navMeshData == null)
                return false;

            // 씬 이름과 같은 폴더(오클루전 데이터와 같은 곳)에 저장한다.
            string folder = Path.Combine(Path.GetDirectoryName(scene.path) ?? string.Empty, scene.name).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(folder)?.Replace('\\', '/'), scene.name);

            string path = $"{folder}/NavMesh-{mapRoot.name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(surface.navMeshData, path);
            EditorUtility.SetDirty(surface);
            return true;
        }

        #endregion

        #region 제거

        /// <summary>
        /// 꺼져 있는 환경 오브젝트를 지운다. (게임 중에 켜지지 않으므로 불필요)
        /// </summary>
        private static int RemoveInactive(Transform environment)
        {
            int removed = 0;
            foreach (Transform child in environment.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (child != null && child != environment && !child.gameObject.activeSelf)
                {
                    Object.DestroyImmediate(child.gameObject);
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>
        /// 보관 영역과 전혀 겹치지 않는 렌더러·충돌체·조명을 지운다.
        /// 자식이 없는 오브젝트는 통째로, 자식이 있으면 해당 컴포넌트만 지운다.
        /// </summary>
        private static int RemoveOutside(Transform environment, Bounds keepArea)
        {
            int removed = 0;
            foreach (Transform node in environment.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (node == null || node == environment)
                    continue;

                Component[] parts = node.GetComponents<Component>()
                    .Where(c => c is Renderer || c is Collider || c is Light || c is MeshFilter || c is LODGroup)
                    .ToArray();
                if (parts.Length == 0 || !IsOutside(node.gameObject, keepArea))
                    continue;

                if (node.childCount == 0)
                {
                    Object.DestroyImmediate(node.gameObject);
                }
                else
                {
                    // 자식이 영역 안에 있을 수 있으므로 이 오브젝트의 표시·충돌만 지운다.
                    foreach (Component part in parts.OrderBy(p => p is MeshFilter ? 1 : 0))
                    {
                        if (part != null)
                            Object.DestroyImmediate(part);
                    }
                }
                removed++;
            }
            return removed;
        }

        /// <summary>
        /// 오브젝트의 렌더러·충돌체·조명이 모두 보관 영역 밖에 있는지 확인한다.
        /// </summary>
        private static bool IsOutside(GameObject target, Bounds keepArea)
        {
            foreach (Renderer targetRenderer in target.GetComponents<Renderer>())
            {
                if (targetRenderer.bounds.Intersects(keepArea))
                    return false;
            }
            foreach (Collider targetCollider in target.GetComponents<Collider>())
            {
                if (targetCollider.bounds.Intersects(keepArea))
                    return false;
            }
            foreach (Light targetLight in target.GetComponents<Light>())
            {
                // 방향광은 어디에 있든 맵 전체를 비춘다.
                if (targetLight.type == LightType.Directional)
                    return false;
                var lightBounds = new Bounds(targetLight.transform.position, Vector3.one * targetLight.range * 2f);
                if (lightBounds.Intersects(keepArea))
                    return false;
            }
            // 지형은 넓게 걸쳐 있으므로 지우지 않는다.
            return target.GetComponent<Terrain>() == null;
        }

        /// <summary>
        /// 컴포넌트도 자식도 없는 빈 오브젝트를 아래에서부터 지운다.
        /// </summary>
        private static int RemoveEmpty(Transform environment)
        {
            int removed = 0;
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (Transform node in environment.GetComponentsInChildren<Transform>(true).ToArray())
                {
                    if (node == null || node == environment || node.childCount > 0)
                        continue;
                    if (node.GetComponents<Component>().Length > 1)
                        continue;

                    Object.DestroyImmediate(node.gameObject);
                    removed++;
                    changed = true;
                }
            }
            return removed;
        }

        #endregion

        #region 정적 플래그와 오클루전

        /// <summary>
        /// 환경 렌더러에 정적 플래그를 준다. 가리개로 지정한 개수를 반환한다.
        /// 불투명하고 충분히 큰 메시만 가리개로 쓴다. (반투명·작은 장식은 뒤를 가리지 못하므로)
        /// </summary>
        private static int SetStaticFlags(Transform environment)
        {
            int occluders = 0;
            foreach (Renderer target in environment.GetComponentsInChildren<Renderer>(true))
            {
                if (target is ParticleSystemRenderer || target is SkinnedMeshRenderer)
                    continue;

                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(target.gameObject);
                TransformableProp prop = target.GetComponentInParent<TransformableProp>(true);

                // 판마다 자리를 옮기는 변신 사물(벽 조각 제외)은 움직이지 않는다고 가정한 오클루전 계산에서 뺀다.
                // (넣어 두면 옮긴 뒤 엉뚱하게 사라지거나, 원래 자리에서 뒤쪽을 가린다)
                if (prop != null && !prop.IsWall)
                {
                    flags &= ~(StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
                    GameObjectUtility.SetStaticEditorFlags(target.gameObject, flags);
                    continue;
                }
                flags |= StaticEditorFlags.OccludeeStatic;

                // 변신 사물(자식 조각 포함)은 변신할 때 원래 메시를 복사하고 외곽선도 원래 메시로 그리므로 정적 배칭에서 뺀다.
                if (prop == null)
                    flags |= StaticEditorFlags.BatchingStatic;
                else
                    flags &= ~StaticEditorFlags.BatchingStatic;

                Vector3 size = target.bounds.size;
                bool large = Mathf.Max(size.x, size.y, size.z) >= OccluderMinSize;
                if (large && IsOpaque(target))
                {
                    flags |= StaticEditorFlags.OccluderStatic;
                    occluders++;
                }
                else
                {
                    flags &= ~StaticEditorFlags.OccluderStatic;
                }
                GameObjectUtility.SetStaticEditorFlags(target.gameObject, flags);
            }
            return occluders;
        }

        /// <summary>
        /// 모든 머티리얼이 불투명(알파 테스트 이전 큐)인지 확인한다.
        /// </summary>
        private static bool IsOpaque(Renderer target)
        {
            return target.sharedMaterials.All(m => m != null && m.renderQueue < 2450);
        }

        /// <summary>
        /// 경계 안쪽을 덮는 Occlusion Area를 둔다. 오클루전 계산을 플레이 영역으로 한정해 굽는 시간을 줄인다.
        /// </summary>
        private static void EnsureOcclusionArea(Transform boundary, Bounds playArea)
        {
            OcclusionArea area = boundary.GetComponentInChildren<OcclusionArea>();
            if (area == null)
            {
                var areaObject = new GameObject("OcclusionArea");
                areaObject.transform.SetParent(boundary, false);
                area = areaObject.AddComponent<OcclusionArea>();
            }

            area.transform.position = Vector3.zero;
            area.center = playArea.center;
            area.size = playArea.size;
        }

        /// <summary>
        /// 오클루전 컬링 데이터를 굽는다. (씬 크기에 따라 수 분이 걸릴 수 있음)
        /// </summary>
        private static void BakeOcclusion(Scene scene)
        {
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError("[MapOptimizer] 저장되지 않은 씬은 오클루전을 구울 수 없습니다.");
                return;
            }

            SceneManager.SetActiveScene(scene);
            StaticOcclusionCulling.smallestOccluder = 2f;
            StaticOcclusionCulling.smallestHole = 0.25f;
            StaticOcclusionCulling.backfaceThreshold = 100f;
            if (!StaticOcclusionCulling.Compute())
                Debug.LogError($"[MapOptimizer] {scene.name}: 오클루전 컬링 굽기에 실패했습니다.");
        }

        #endregion

        #region 공용

        /// <summary>
        /// 씬 루트부터 "부모/자식" 경로로 오브젝트를 찾는다.
        /// </summary>
        private static GameObject FindByPath(Scene scene, string path)
        {
            string[] parts = path.Split('/');
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == parts[0]);
            if (root == null)
                return null;

            Transform current = root.transform;
            for (int i = 1; i < parts.Length && current != null; i++)
                current = current.Find(parts[i]);
            return current != null ? current.gameObject : null;
        }

        /// <summary>
        /// 투명 경계(벽·천장 충돌체)가 둘러싼 영역.
        /// </summary>
        private static Bounds GetBoundaryBounds(Transform boundary)
        {
            List<BoxCollider> boxes = boundary.GetComponentsInChildren<BoxCollider>(true).ToList();
            Bounds bounds = boxes[0].bounds;
            foreach (BoxCollider box in boxes)
                bounds.Encapsulate(box.bounds);
            return bounds;
        }

        #endregion
    }
}
