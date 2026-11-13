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
    /// <summary>맵 씬을 가볍게 하려고 갈 수 없는 곳의 오브젝트를 지우고 정적 플래그·오클루전·NavMesh를 굽는다.</summary>
    public static class MapOptimizer
    {
        internal const string EnvironmentPath = "Map/Environment";
        internal const string BoundaryName = "Boundary (Invisible)";

        // 경계 밖이라도 창문 등으로 보일 수 있는 거리
        private const float IndoorMargin = 3f;
        private const float OutdoorMargin = 12f;

        // 가리개로 쓸 최소 크기(m)
        private const float OccluderMinSize = 1f;

        #region 메뉴

        /// <summary>오클루전·NavMesh는 씬 경로 옆에 저장되므로 저장된 씬만 최적화하고, 오브젝트를 지우기 전에 저장 여부를 묻는다.</summary>
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

            // 맵 목록의 씬은 실외, 나머지는 실내 맵으로 본다.
            Optimize(scene, indoor: MapCatalog.FindByScenePath(scene.path) == null);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        #endregion

        /// <summary>지운 오브젝트가 오클루전에 남지 않도록 지우기 → 정적 플래그 → 굽기 순서를 한곳에 둔다.</summary>
        public static void Optimize(Scene scene, bool indoor)
        {
            GameObject environmentObject = FindByPath(scene, EnvironmentPath);
            GameObject boundaryObject = FindByPath(scene, BoundaryName);
            if (environmentObject == null || boundaryObject == null)
            {
                Debug.LogError($"[MapOptimizer] {scene.name}: '{EnvironmentPath}' 또는 '{BoundaryName}'을 찾을 수 없습니다. 맵 생성 도구로 만든 씬인지 확인하세요.");
                return;
            }

            // 실시간 조명만 쓰므로 GI 계산을 끈다.
            SceneLightingSetup.Apply(scene);

            Bounds playArea = GetBoundaryBounds(boundaryObject.transform);
            Bounds keepArea = playArea;
            float margin = indoor ? IndoorMargin : OutdoorMargin;
            keepArea.Expand(new Vector3(margin * 2f, margin * 2f, margin * 2f));

            Transform environment = environmentObject.transform;
            Transform mapRoot = environment.parent;
            int inactiveCount = RemoveInactive(environment);
            int outsideCount = RemoveOutside(environment, keepArea);
            int emptyCount = RemoveEmpty(environment);
            int fixedBoxCount = FixNegativeScaleBoxColliders(environment);
            int areaLightCount = RemoveAreaLights(mapRoot);
            int shadowLightCount = LimitLightShadows(mapRoot);
            int occluderCount = SetStaticFlags(environment);
            int physicsColliderCount = PropColliderBaker.BakeScene(scene);

            bool isNavMeshBaked = BakeNavMesh(scene, mapRoot);
            EnsureOcclusionArea(boundaryObject.transform, playArea);
            BakeOcclusion(scene);

            Debug.Log($"[MapOptimizer] {scene.name}: 꺼진 오브젝트 {inactiveCount}개, 경계 밖 {outsideCount}개, 빈 오브젝트 {emptyCount}개 제거. " +
                      $"음수 크기 상자 충돌체 {fixedBoxCount}개 교체, 튕겨나갈 사물 충돌체 {physicsColliderCount}개 볼록화, 영역 조명 {areaLightCount}개 제거, 그림자 끈 조명 {shadowLightCount}개. " +
                      $"NavMesh {(isNavMeshBaked ? "저장" : "실패")}, 가리개 {occluderCount}개로 오클루전 컬링을 구웠습니다.");
        }

        #region 조명·충돌체·NavMesh

        /// <summary>URP는 실시간 영역 조명을 지원하지 않아 경고만 남기므로 지운다.</summary>
        private static int RemoveAreaLights(Transform mapRoot)
        {
            int removedCount = 0;
            foreach (Light areaLight in mapRoot.GetComponentsInChildren<Light>(true))
            {
                if (areaLight.type != LightType.Rectangle && areaLight.type != LightType.Disc)
                    continue;
                if (areaLight.TryGetComponent(out MapLight mapLight))
                    Object.DestroyImmediate(mapLight);
                if (areaLight.TryGetComponent(out UniversalAdditionalLightData additionalData))
                    Object.DestroyImmediate(additionalData);
                Object.DestroyImmediate(areaLight);
                // 지우지 못했으면(다른 의존 컴포넌트) 세지 않는다.
                if (areaLight == null)
                    removedCount++;
            }
            return removedCount;
        }

        /// <summary>그림자를 내는 실시간 조명이 많으면 화질이 떨어지고 느려지므로 맵 조명의 그림자를 끈다.</summary>
        private static int LimitLightShadows(Transform mapRoot)
        {
            int changedCount = 0;
            foreach (Light mapLight in mapRoot.GetComponentsInChildren<Light>(true))
            {
                if (mapLight.type == LightType.Directional || mapLight.shadows == LightShadows.None)
                    continue;
                mapLight.shadows = LightShadows.None;
                changedCount++;
            }
            return changedCount;
        }

        /// <summary>상자 충돌체는 음수 크기를 지원하지 않아 모양이 틀어지므로 메시 충돌체로 바꾼다.</summary>
        private static int FixNegativeScaleBoxColliders(Transform environment)
        {
            int replacedCount = 0;
            foreach (BoxCollider boxCollider in environment.GetComponentsInChildren<BoxCollider>(true))
            {
                Vector3 worldScale = boxCollider.transform.lossyScale;
                if (worldScale.x >= 0f && worldScale.y >= 0f && worldScale.z >= 0f)
                    continue;

                MeshFilter meshFilter = boxCollider.GetComponent<MeshFilter>();
                GameObject colliderObject = boxCollider.gameObject;
                Object.DestroyImmediate(boxCollider);
                if (meshFilter != null && meshFilter.sharedMesh != null)
                    colliderObject.AddComponent<MeshCollider>().sharedMesh = meshFilter.sharedMesh;
                replacedCount++;
            }
            return replacedCount;
        }

        /// <summary>빌드에서는 실행 중에 NavMesh를 구울 수 없으므로 에디터에서 구워 에셋으로 저장한다.</summary>
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

            // 오클루전 데이터와 같은 씬 폴더에 저장한다.
            string sceneDataFolder = Path.Combine(Path.GetDirectoryName(scene.path) ?? string.Empty, scene.name).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(sceneDataFolder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(sceneDataFolder)?.Replace('\\', '/'), scene.name);

            string navMeshPath = $"{sceneDataFolder}/NavMesh-{mapRoot.name}.asset";
            AssetDatabase.DeleteAsset(navMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, navMeshPath);
            EditorUtility.SetDirty(surface);
            return true;
        }

        #endregion

        #region 제거

        /// <summary>
        /// 맵 오브젝트를 켜는 게임 코드가 없어 꺼진 오브젝트는 용량만 차지하므로 지운다.
        /// </summary>
        private static int RemoveInactive(Transform environment)
        {
            int removedCount = 0;
            foreach (Transform child in environment.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (child != null && child != environment && !child.gameObject.activeSelf)
                {
                    Object.DestroyImmediate(child.gameObject);
                    removedCount++;
                }
            }
            return removedCount;
        }

        /// <summary>볼 수 없는 곳의 오브젝트도 그리기·충돌 비용이 들므로 보관 영역 밖의 렌더러·충돌체·조명을 지운다.</summary>
        private static int RemoveOutside(Transform environment, Bounds keepArea)
        {
            int removedCount = 0;
            foreach (Transform node in environment.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (node == null || node == environment)
                    continue;

                Component[] visualComponents = node.GetComponents<Component>()
                    .Where(component => component is Renderer || component is Collider || component is Light || component is MeshFilter || component is LODGroup)
                    .ToArray();
                if (visualComponents.Length == 0 || !IsOutside(node.gameObject, keepArea))
                    continue;

                if (node.childCount == 0)
                {
                    Object.DestroyImmediate(node.gameObject);
                }
                else
                {
                    // 자식이 영역 안에 있을 수 있으므로 이 오브젝트의 표시·충돌만 지운다.
                    foreach (Component visualComponent in visualComponents.OrderBy(component => component is MeshFilter ? 1 : 0))
                    {
                        if (visualComponent != null)
                            Object.DestroyImmediate(visualComponent);
                    }
                }
                removedCount++;
            }
            return removedCount;
        }

        /// <summary>
        /// 하나라도 영역에 닿으면 보일 수 있으므로 렌더러·충돌체·조명이 모두 영역 밖일 때만 밖으로 본다.
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

        /// <summary>컴포넌트를 지우고 남은 빈 오브젝트는 Hierarchy만 복잡하게 하므로 지운다.</summary>
        private static int RemoveEmpty(Transform environment)
        {
            int removedCount = 0;
            bool isChanged = true;
            while (isChanged)
            {
                isChanged = false;
                foreach (Transform node in environment.GetComponentsInChildren<Transform>(true).ToArray())
                {
                    if (node == null || node == environment || node.childCount > 0)
                        continue;
                    if (node.GetComponents<Component>().Length > 1)
                        continue;

                    Object.DestroyImmediate(node.gameObject);
                    removedCount++;
                    isChanged = true;
                }
            }
            return removedCount;
        }

        #endregion

        #region 정적 플래그와 오클루전

        /// <summary>오클루전 컬링과 정적 배칭이 동작하도록 환경 렌더러에 정적 플래그를 준다.</summary>
        private static int SetStaticFlags(Transform environment)
        {
            int occluderCount = 0;
            foreach (Renderer environmentRenderer in environment.GetComponentsInChildren<Renderer>(true))
            {
                if (environmentRenderer is ParticleSystemRenderer || environmentRenderer is SkinnedMeshRenderer)
                    continue;

                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(environmentRenderer.gameObject);
                TransformableProp prop = environmentRenderer.GetComponentInParent<TransformableProp>(true);

                // 판마다 옮겨지는 변신 사물은 엉뚱하게 가려지지 않도록 오클루전 계산에서 뺀다.
                if (prop != null && !prop.IsWall)
                {
                    flags &= ~(StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
                    GameObjectUtility.SetStaticEditorFlags(environmentRenderer.gameObject, flags);
                    continue;
                }
                flags |= StaticEditorFlags.OccludeeStatic;

                // 변신 사물(자식 조각 포함)은 변신할 때 원래 메시를 복사하고 외곽선도 원래 메시로 그리므로 정적 배칭에서 뺀다.
                if (prop == null)
                    flags |= StaticEditorFlags.BatchingStatic;
                else
                    flags &= ~StaticEditorFlags.BatchingStatic;

                Vector3 rendererSize = environmentRenderer.bounds.size;
                bool isLarge = Mathf.Max(rendererSize.x, rendererSize.y, rendererSize.z) >= OccluderMinSize;
                if (isLarge && IsOpaque(environmentRenderer))
                {
                    flags |= StaticEditorFlags.OccluderStatic;
                    occluderCount++;
                }
                else
                {
                    flags &= ~StaticEditorFlags.OccluderStatic;
                }
                GameObjectUtility.SetStaticEditorFlags(environmentRenderer.gameObject, flags);
            }
            return occluderCount;
        }

        /// <summary>반투명 머티리얼이 있는 메시를 가리개로 쓰면 뒤의 물체가 잘못 사라지므로 불투명인지 본다.</summary>
        private static bool IsOpaque(Renderer targetRenderer)
        {
            return targetRenderer.sharedMaterials.All(material => material != null && material.renderQueue < 2450);
        }

        /// <summary>굽는 시간이 크게 늘지 않도록 오클루전 계산을 플레이 영역으로 한정한다.</summary>
        private static void EnsureOcclusionArea(Transform boundary, Bounds playArea)
        {
            OcclusionArea occlusionArea = boundary.GetComponentInChildren<OcclusionArea>();
            if (occlusionArea == null)
            {
                var areaObject = new GameObject("OcclusionArea");
                areaObject.transform.SetParent(boundary, false);
                occlusionArea = areaObject.AddComponent<OcclusionArea>();
            }

            occlusionArea.transform.position = Vector3.zero;
            occlusionArea.center = playArea.center;
            occlusionArea.size = playArea.size;
        }

        /// <summary>오클루전은 활성 씬 데이터로 구워지므로 대상 씬을 활성으로 만들고 최소 크기를 맵 규모에 맞춘다.</summary>
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
        /// GameObject.Find는 꺼진 오브젝트를 찾지 못하고 여러 씬이 열려 있으면 엉뚱한 씬을 찾으므로, 지정한 씬의 루트부터 "부모/자식" 경로로 따라간다.
        /// </summary>
        internal static GameObject FindByPath(Scene scene, string path)
        {
            string[] pathNames = path.Split('/');
            GameObject rootObject = scene.GetRootGameObjects().FirstOrDefault(candidate => candidate.name == pathNames[0]);
            if (rootObject == null)
                return null;

            Transform current = rootObject.transform;
            for (int i = 1; i < pathNames.Length && current != null; i++)
                current = current.Find(pathNames[i]);
            return current != null ? current.gameObject : null;
        }

        /// <summary>
        /// 플레이 영역을 따로 저장하지 않고, 투명 경계(벽·천장 충돌체)가 둘러싼 범위를 플레이 영역으로 본다.
        /// </summary>
        internal static Bounds GetBoundaryBounds(Transform boundary)
        {
            List<BoxCollider> boundaryBoxes = boundary.GetComponentsInChildren<BoxCollider>(true).ToList();
            Bounds boundaryBounds = boundaryBoxes[0].bounds;
            foreach (BoxCollider boundaryBox in boundaryBoxes)
                boundaryBounds.Encapsulate(boundaryBox.bounds);
            return boundaryBounds;
        }

        #endregion
    }
}
