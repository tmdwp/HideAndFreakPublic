using System;
using System.Collections.Generic;
using System.Linq;
using GuardVsMannequin.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Map
{
    /// <summary>
    /// 경계 안개 겹을 맵 씬에 미리 둘 수 있도록 겹 배치를 기억하고, 빌드 뒤 바꿀 수 있는 조정값(불투명도·깊이)은 불러올 때 다시 적용한다.
    /// </summary>
    public class BoundaryFogLayers : MonoBehaviour
    {
        public const string RootName = "Boundary Fog";
        // 맵 생성 도구(MapSceneBuilder.CreateBoundary)가 만드는 이름과 같아야 한다.
        public const string BoundaryName = "Boundary (Invisible)";
        private const string WallPrefix = "Wall_";

        // 한 장이면 납작한 벽처럼 보여서 바깥쪽으로 여러 겹을 둬 깊이감을 낸다.
        private const int LayerCount = 6;
        // 겹 사이가 끊겨 보이지 않도록 한 겹이 물체를 부드럽게 덮는 거리를 겹 간격보다 넉넉히 잡는다.
        private const float SoftDistancePerSpacing = 1.5f;

        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int SoftDistanceId = Shader.PropertyToID("_SoftDistance");
        private static readonly int FarFadeStartId = Shader.PropertyToID("_FarFadeStart");
        private static readonly int FarFadeEndId = Shader.PropertyToID("_FarFadeEnd");

        /// <summary>조정값이 바뀌어도 겹을 다시 놓을 수 있도록 벽 하나의 안쪽 면 정보를 기억한다.</summary>
        [Serializable]
        private struct FogWall
        {
            public Vector3 innerFace;
            public Vector3 inward;
            public float wallLength;
            public float height;
            public MeshRenderer[] layers;
        }

        [SerializeField] private List<FogWall> walls = new();

        /// <summary>
        /// 미리 둔 안개도 빌드 뒤 조정 파일 값을 따르도록 불러올 때 한 번 다시 맞춘다.
        /// </summary>
        private void Awake()
        {
            ApplyTuning();
        }

        /// <summary>
        /// 공유 머티리얼을 바꾸면 에디터에서 에셋까지 바뀌므로 렌더러별 속성으로 불투명도를 넣는다.
        /// </summary>
        public void ApplyTuning()
        {
            float layerSpacing = GetLayerSpacing();
            var properties = new MaterialPropertyBlock();
            properties.SetFloat(OpacityId, GetLayerOpacity());
            properties.SetFloat(SoftDistanceId, GetSoftDistance(layerSpacing));
            properties.SetFloat(FarFadeStartId, GameTuning.BoundaryFogFarFadeStart);
            properties.SetFloat(FarFadeEndId, GameTuning.BoundaryFogFarFadeEnd);

            foreach (FogWall wall in walls)
            {
                for (int layer = 0; layer < wall.layers.Length; layer++)
                {
                    MeshRenderer fogRenderer = wall.layers[layer];
                    if (fogRenderer == null)
                        continue;
                    PlaceLayer(fogRenderer.transform, wall, layer, layerSpacing);
                    fogRenderer.SetPropertyBlock(properties);
                }
            }
        }

        /// <summary>
        /// 실행 중 생성(모드 맵·예전 맵)과 에디터 미리 배치가 같은 결과가 되도록 겹 만들기를 한곳에 둔다.
        /// 새 오브젝트는 지금 활성 씬에 생기므로, 다른 씬에 둬야 하면 부르는 쪽이 옮긴다.
        /// </summary>
        public static BoundaryFogLayers Create(Transform boundary, Mesh quadMesh, Material material)
        {
            List<(Vector3 center, Vector3 size)> wallBoxes = boundary.GetComponentsInChildren<BoxCollider>(true)
                .Where(box => box.name.StartsWith(WallPrefix))
                .Select(WorldBox)
                .ToList();
            if (wallBoxes.Count == 0 || quadMesh == null || material == null)
                return null;

            var area = new Bounds(wallBoxes[0].center, wallBoxes[0].size);
            foreach ((Vector3 center, Vector3 size) in wallBoxes)
                area.Encapsulate(new Bounds(center, size));

            var fogRoot = new GameObject(RootName);
            BoundaryFogLayers fog = fogRoot.AddComponent<BoundaryFogLayers>();
            foreach ((Vector3 center, Vector3 size) in wallBoxes)
                fog.walls.Add(CreateWall(fogRoot.transform, center, size, area.center, quadMesh, material));
            fog.ApplyTuning();
            return fog;
        }

        /// <summary>
        /// 막 만든 충돌체는 에디터에서 bounds가 아직 계산되지 않을 수 있어, 경계 벽(회전 없는 상자)의 월드 크기를 직접 구한다.
        /// </summary>
        private static (Vector3 center, Vector3 size) WorldBox(BoxCollider box)
        {
            Transform boxTransform = box.transform;
            Vector3 lossyScale = boxTransform.lossyScale;
            Vector3 size = Vector3.Scale(box.size, new Vector3(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z)));
            return (boxTransform.TransformPoint(box.center), size);
        }

        /// <summary>경계 벽은 축에 맞춘 얇은 상자라서, 얇은 축의 맵 쪽 면부터 바깥으로 겹을 쌓는다.</summary>
        private static FogWall CreateWall(Transform parent, Vector3 wallCenter, Vector3 wallSize, Vector3 areaCenter, Mesh quadMesh, Material material)
        {
            bool isThinOnX = wallSize.x < wallSize.z;
            Vector3 inward = isThinOnX
                ? new Vector3(Mathf.Sign(areaCenter.x - wallCenter.x), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(areaCenter.z - wallCenter.z));

            var wall = new FogWall
            {
                innerFace = wallCenter + Vector3.Scale(inward, wallSize * 0.5f),
                inward = inward,
                wallLength = isThinOnX ? wallSize.z : wallSize.x,
                height = wallSize.y,
                layers = new MeshRenderer[LayerCount]
            };

            for (int layer = 0; layer < LayerCount; layer++)
            {
                var fogLayer = new GameObject($"FogLayer_{layer}");
                fogLayer.transform.SetParent(parent, false);
                fogLayer.AddComponent<MeshFilter>().sharedMesh = quadMesh;
                MeshRenderer fogRenderer = fogLayer.AddComponent<MeshRenderer>();
                fogRenderer.sharedMaterial = material;
                fogRenderer.shadowCastingMode = ShadowCastingMode.Off;
                fogRenderer.receiveShadows = false;
                fogRenderer.lightProbeUsage = LightProbeUsage.Off;
                fogRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                wall.layers[layer] = fogRenderer;
            }
            return wall;
        }

        /// <summary>바깥 겹은 모서리에서 틈이 보이지 않도록 밀려난 만큼 양옆을 늘린다.</summary>
        private static void PlaceLayer(Transform layerTransform, FogWall wall, int layer, float layerSpacing)
        {
            float outwardOffset = layerSpacing * layer;
            layerTransform.SetPositionAndRotation(wall.innerFace - wall.inward * outwardOffset, Quaternion.LookRotation(wall.inward));
            layerTransform.localScale = new Vector3(wall.wallLength + outwardOffset * 2f, wall.height, 1f);
        }

        /// <summary>미리 만든 머티리얼이 씬 뷰에서도 게임과 비슷하게 보이도록 기본값을 넣는다.</summary>
        public static void ApplyDefaults(Material material)
        {
            material.SetFloat(OpacityId, GetLayerOpacity());
            material.SetFloat(SoftDistanceId, GetSoftDistance(GetLayerSpacing()));
            material.SetFloat(FarFadeStartId, GameTuning.BoundaryFogFarFadeStart);
            material.SetFloat(FarFadeEndId, GameTuning.BoundaryFogFarFadeEnd);
        }

        /// <summary>겹을 모두 겹친 불투명도가 설정값이 되도록 한 겹의 불투명도를 거꾸로 구한다.</summary>
        private static float GetLayerOpacity()
        {
            return 1f - Mathf.Pow(1f - GameTuning.BoundaryFogOpacity, 1f / LayerCount);
        }

        /// <summary>겹 배치와 부드럽게 덮는 거리가 같은 간격을 써야 해서 한곳에서 구한다.</summary>
        private static float GetLayerSpacing()
        {
            return GameTuning.BoundaryFogDepth / Mathf.Max(1, LayerCount - 1);
        }

        /// <summary>겹 간격에 맞춰야 겹 사이가 끊겨 보이지 않는다.</summary>
        private static float GetSoftDistance(float layerSpacing)
        {
            return Mathf.Max(1f, layerSpacing * SoftDistancePerSpacing);
        }

        /// <summary>충돌체가 생기면 레이·겹침 검사에 걸리므로 기본 Quad 대신 메시만 직접 만든다.</summary>
        public static Mesh CreateQuadMesh()
        {
            var quadMesh = new Mesh { name = "BoundaryFogQuad" };
            quadMesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
            };
            // 셰이더가 위쪽 끝을 흐리게 하려고 세로 위치를 uv.y로 읽는다.
            quadMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            quadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            quadMesh.RecalculateBounds();
            return quadMesh;
        }
    }
}
