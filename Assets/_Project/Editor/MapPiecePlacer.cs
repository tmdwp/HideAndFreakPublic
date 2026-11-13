using System.Collections.Generic;
using GuardVsMannequin.Core;
using GuardVsMannequin.Map;
using GuardVsMannequin.Props;
using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>원점이 제각각인 에셋을 겉모습 기준으로 놓고 테마 머티리얼·변신 사물 등록·빈자리 찾기를 맡는다.</summary>
    internal sealed class MapPiecePlacer
    {
        private readonly Dictionary<string, Material> materialRemap;
        private readonly Dictionary<string, Vector3> measuredSizes = new Dictionary<string, Vector3>();
        private readonly List<GameObject> propCandidates = new List<GameObject>();

        /// <summary>맵 빌더가 끝에 에셋이 빠졌는지 알려 줄 수 있도록 센다.</summary>
        public int MissingCount { get; private set; }

        /// <summary>맵마다 테마 색이 달라 바꿔 끼울 머티리얼 표를 받아 둔다.</summary>
        /// <param name="materialRemap">원래 머티리얼 이름 → 바꿀 머티리얼.</param>
        public MapPiecePlacer(Dictionary<string, Material> materialRemap)
        {
            this.materialRemap = materialRemap ?? new Dictionary<string, Material>();
        }

        #region 배치

        /// <summary>에셋마다 원점이 제각각이라 렌더러 경계의 바닥 중심을 floorCenter에 맞춘다.</summary>
        /// <param name="fitHeight">0보다 크면 높이가 이 값이 되도록 균일하게 키우거나 줄인다.</param>
        /// <param name="scale">에셋 원래 크기에 곱할 배율(null이면 그대로).</param>
        /// <param name="remap">true면 머티리얼을 이름표대로 바꿔 끼운다.</param>
        /// <param name="tilt">좌우 회전 전에 먼저 적용할 회전.</param>
        public GameObject Place(string assetPath, Transform parent, Vector3 floorCenter, float yaw,
            float fitHeight = 0f, Vector3? scale = null, bool remap = false, string name = null, Quaternion? tilt = null)
        {
            GameObject sourceAsset = LoadAsset(assetPath, out Vector3 pathScale);
            if (sourceAsset == null)
            {
                MissingCount++;
                Debug.LogWarning($"[MapPiecePlacer] 에셋을 찾지 못해 건너뜁니다: {assetPath}");
                return null;
            }

            var instance = PrefabUtility.InstantiatePrefab(sourceAsset, parent) as GameObject;
            if (instance == null)
                instance = Object.Instantiate(sourceAsset, parent);
            // 맵 물체 프리팹(Prefabs/Obj)은 연결을 유지해 프리팹을 고치면 맵에도 반영되게 하고, 그 밖의 에셋은 풀어서 맵과 독립시킨다.
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(instance) && !MapObjectCatalog.IsObjectPrefab(AssetDatabase.GetAssetPath(sourceAsset)))
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            instance.name = name ?? sourceAsset.name;
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * (tilt ?? Quaternion.identity) * sourceAsset.transform.rotation;
            instance.transform.localScale = Vector3.Scale(instance.transform.localScale, Vector3.Scale(pathScale, scale ?? Vector3.one));

            Bounds placedBounds = GetBounds(instance);
            if (!measuredSizes.ContainsKey(assetPath))
                measuredSizes[assetPath] = Quaternion.Euler(0f, -yaw, 0f) * placedBounds.size;
            if (fitHeight > 0f && placedBounds.size.y > 0.01f)
            {
                instance.transform.localScale *= fitHeight / placedBounds.size.y;
                placedBounds = GetBounds(instance);
            }

            instance.transform.position += floorCenter - new Vector3(placedBounds.center.x, placedBounds.min.y, placedBounds.center.z);
            if (remap)
                RemapMaterials(instance);
            return instance;
        }

        /// <summary>크기만 다른 같은 물체를 프리팹 하나로 쓰려고 경로 끝의 "?scale="을 크기 배율로 읽는다.</summary>
        public static GameObject LoadAsset(string assetPath, out Vector3 scale)
        {
            scale = Vector3.one;
            int queryIndex = assetPath.IndexOf("?scale=", System.StringComparison.Ordinal);
            if (queryIndex < 0)
                return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            string[] scaleValues = assetPath.Substring(queryIndex + 7).Split(',');
            // 문화권과 무관하게 소수를 읽는다.
            float ParseInvariantFloat(string text) => float.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
            scale = scaleValues.Length == 3
                ? new Vector3(ParseInvariantFloat(scaleValues[0]), ParseInvariantFloat(scaleValues[1]), ParseInvariantFloat(scaleValues[2]))
                : Vector3.one * ParseInvariantFloat(scaleValues[0]);
            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath.Substring(0, queryIndex));
        }

        /// <summary>다른 물체 위에 놓을 때 높이를 손으로 맞추지 않도록 받침 경계의 윗면에 놓는다.</summary>
        public GameObject PlaceOn(string assetPath, GameObject support, Vector2 offset, float yaw, float fitHeight = 0f)
        {
            if (support == null)
                return null;
            Bounds supportBounds = GetBounds(support);
            var topPosition = new Vector3(supportBounds.center.x + offset.x, supportBounds.max.y, supportBounds.center.z + offset.y);
            return Place(assetPath, support.transform.parent, topPosition, yaw, fitHeight);
        }

        /// <summary>정리 단계에서 스크립트를 지우면 등록이 사라지므로 후보로만 모아 두고 나중에 등록한다.</summary>
        public GameObject AsProp(GameObject placed)
        {
            if (placed != null)
                propCandidates.Add(placed);
            return placed;
        }

        /// <summary>맵 검사에 걸리지 않도록 너무 크거나 복사할 수 없는 부분이 있는 사물은 건너뛴다.</summary>
        public void RegisterCandidates(out int propCount, out int skipped)
        {
            propCount = 0;
            skipped = 0;
            foreach (GameObject candidate in propCandidates)
            {
                if (candidate == null)
                    continue;
                Bounds candidateBounds = GetBounds(candidate);
                float largestSize = Mathf.Max(candidateBounds.size.x, candidateBounds.size.y, candidateBounds.size.z);
                if (largestSize > GameTuning.DisguiseMaxSize || HasUncopiedParts(candidate))
                {
                    skipped++;
                    continue;
                }
                if (PropRegistrar.Register(candidate, absorbChildren: true))
                    propCount++;
            }
        }

        /// <summary>
        /// 배치 좌표를 다듬을 때 실제 크기를 알아야 하므로, 처음 놓을 때 잰 에셋별 크기(회전 전 기준)를 로그용 문자열로 만든다.
        /// </summary>
        public string DescribeSizes()
        {
            var builder = new System.Text.StringBuilder();
            foreach (KeyValuePair<string, Vector3> entry in measuredSizes)
                builder.AppendLine($"{System.IO.Path.GetFileNameWithoutExtension(entry.Key)}: {entry.Value.x:F2} x {entry.Value.y:F2} x {entry.Value.z:F2}");
            return builder.ToString();
        }

        #endregion

        #region 경계·머티리얼

        /// <summary>원점이 제각각인 에셋을 겉모습 기준으로 놓으려고 메시 렌더러 전체의 월드 경계를 구한다.</summary>
        public static Bounds GetBounds(GameObject root)
        {
            bool hasBounds = false;
            var combinedBounds = new Bounds(root.transform.position, Vector3.zero);
            foreach (Renderer childRenderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(childRenderer is MeshRenderer) && !(childRenderer is SkinnedMeshRenderer))
                    continue;
                if (!hasBounds)
                {
                    combinedBounds = childRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(childRenderer.bounds);
                }
            }
            return combinedBounds;
        }

        /// <summary>MapKit 모델은 기본 색으로 임포트되므로 머티리얼 이름으로 찾아 맵 테마 색으로 바꿔 끼운다.</summary>
        private void RemapMaterials(GameObject instance)
        {
            foreach (Renderer childRenderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = childRenderer.sharedMaterials;
                bool isChanged = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && materialRemap.TryGetValue(materials[i].name, out Material replacement))
                    {
                        materials[i] = replacement;
                        isChanged = true;
                    }
                }
                if (isChanged)
                    childRenderer.sharedMaterials = materials;
            }
        }

        /// <summary>외형 복사는 MeshRenderer만 다루므로 다른 표시 요소가 있으면 변신 모습이 달라진다.</summary>
        private static bool HasUncopiedParts(GameObject target)
        {
            return target.GetComponentInChildren<ParticleSystem>(true) != null
                   || target.GetComponentInChildren<Light>(true) != null
                   || target.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
        }

        #endregion

        #region 빈자리

        /// <summary>지점이 사물과 겹쳐 시작부터 끼이지 않도록 원하는 위치 근처에서 가까운 빈자리부터 찾는다.</summary>
        /// <param name="bodyRadius">비어 있어야 하는 기둥 반지름.</param>
        /// <param name="propClearance">0보다 크면 이 거리 안에 변신 사물(벽 제외)이 없어야 한다.</param>
        public static Vector3 FindFreeSpot(Vector3 desired, float bodyRadius, float propClearance, string label)
        {
            const int angleSteps = 12;
            for (float ringRadius = 0f; ringRadius <= 4f; ringRadius += 0.5f)
            {
                int stepCount = ringRadius <= 0f ? 1 : angleSteps;
                for (int i = 0; i < stepCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / stepCount;
                    Vector3 candidate = desired + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringRadius;
                    if (IsFree(candidate, bodyRadius, propClearance))
                        return candidate;
                }
            }
            Debug.LogWarning($"[MapPiecePlacer] {label}: {desired} 근처에서 빈자리를 찾지 못해 그대로 둡니다.");
            return desired;
        }

        /// <summary>떨어지거나 끼이지 않도록 바닥과 머리 높이를 확인하고, 전원 장치는 사물에 가려지지 않게 한다.</summary>
        private static bool IsFree(Vector3 point, float bodyRadius, float propClearance)
        {
            if (!Physics.Raycast(point + Vector3.up * 0.5f, Vector3.down, 1f))
                return false;

            Vector3 capsuleBottom = point + Vector3.up * (bodyRadius + 0.15f);
            Vector3 capsuleTop = point + Vector3.up * Mathf.Max(bodyRadius + 0.15f, 1.9f - bodyRadius);
            if (Physics.CheckCapsule(capsuleBottom, capsuleTop, bodyRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;

            if (propClearance <= 0f)
                return true;
            foreach (Collider nearby in Physics.OverlapSphere(point + Vector3.up * 0.8f, propClearance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                TransformableProp prop = nearby.GetComponentInParent<TransformableProp>();
                if (prop != null && !prop.IsWall)
                    return false;
            }
            return true;
        }

        #endregion

        #region 조명

        /// <summary>사냥 시간에 빛과 함께 판도 꺼져 보이도록 켜짐/꺼짐 머티리얼을 연결한 형광등을 만든다.</summary>
        public static GameObject CreatePanelLamp(Transform parent, Vector3 ceilingPoint, Color color, float intensity, float range,
            Material onMaterial, Material offMaterial, float yaw = 0f)
        {
            var lamp = new GameObject("PanelLamp");
            lamp.transform.SetParent(parent, false);
            lamp.transform.SetPositionAndRotation(ceilingPoint, Quaternion.Euler(0f, yaw, 0f));

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Bulb";
            Object.DestroyImmediate(panel.GetComponent<Collider>());
            panel.transform.SetParent(lamp.transform, false);
            panel.transform.localPosition = Vector3.down * 0.03f;
            panel.transform.localScale = new Vector3(1.4f, 0.05f, 0.3f);
            Renderer bulbRenderer = panel.GetComponent<Renderer>();
            bulbRenderer.sharedMaterial = onMaterial;
            bulbRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            AddPointLight(lamp.transform, Vector3.down * 0.3f, color, intensity, range);

            MapLight mapLight = lamp.AddComponent<MapLight>();
            TestEnvironmentSetupTool.SetField(mapLight, "bulbRenderer", bulbRenderer);
            TestEnvironmentSetupTool.SetField(mapLight, "onMaterial", onMaterial);
            TestEnvironmentSetupTool.SetField(mapLight, "offMaterial", offMaterial);
            return lamp;
        }

        /// <summary>발광 머티리얼만으로는 주변을 비추지 못하므로 안쪽에 은은한 빛을 더한다.</summary>
        public static GameObject CreateBareLight(Transform parent, string lightName, Vector3 position, Color color, float intensity, float range)
        {
            var lightHolder = new GameObject(lightName);
            lightHolder.transform.SetParent(parent, false);
            lightHolder.transform.position = position;
            AddPointLight(lightHolder.transform, Vector3.zero, color, intensity, range);
            lightHolder.AddComponent<MapLight>();
            return lightHolder;
        }

        /// <summary>조명이 많아 그림자 비용이 크므로 그림자를 끄고, 사냥 시간에 꺼야 하므로 실시간으로 둔다.</summary>
        private static void AddPointLight(Transform parent, Vector3 localPosition, Color color, float intensity, float range)
        {
            var lightObject = new GameObject("PointLight");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = localPosition;
            Light pointLight = lightObject.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.color = color;
            pointLight.intensity = intensity;
            pointLight.range = range;
            pointLight.shadows = LightShadows.None;
            pointLight.lightmapBakeType = LightmapBakeType.Realtime;
        }

        #endregion
    }
}
