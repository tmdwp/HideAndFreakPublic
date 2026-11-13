using System.Collections.Generic;
using System.Linq;
using GuardVsMannequin.Core;
using GuardVsMannequin.Mods;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Map
{
    /// <summary>맵을 다시 만들지 않아도 모든 맵에 적용되도록, 불러온 맵의 투명 경계 벽 안쪽에 흰 안개 면을 실행 중에 붙인다.</summary>
    public static class BoundaryFog
    {
        // 맵 생성 도구(MapSceneBuilder.CreateBoundary)가 만드는 이름과 같아야 한다.
        private const string BoundaryName = "Boundary (Invisible)";
        private const string WallPrefix = "Wall_";
        private const string ShaderPath = "Shaders/BoundaryFog";
        // 한 장이면 납작한 벽처럼 보여서 바깥쪽으로 여러 겹을 둬 깊이감을 낸다.
        private const int LayerCount = 6;
        // 겹 사이가 끊겨 보이지 않도록 한 겹이 물체를 부드럽게 덮는 거리를 겹 간격보다 넉넉히 잡는다.
        private const float SoftDistancePerSpacing = 1.5f;

        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int SoftDistanceId = Shader.PropertyToID("_SoftDistance");

        private static Mesh quadMesh;
        private static Material fogMaterial;

        /// <summary>도메인 재로드를 끈 경우 정적 구독이 겹치지 않도록 플레이 모드에 들어갈 때마다 다시 구독한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            quadMesh = null;
            fogMaterial = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>룸·메뉴 씬과 검사에 걸린 모드 맵에는 붙이지 않는다.</summary>
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!ModSceneGuard.Approve(scene))
                return;

            GameObject[] roots = scene.GetRootGameObjects();
            if (!roots.Any(root => root.GetComponentInChildren<MapInfo>(true) != null))
                return;

            GameObject boundary = roots.FirstOrDefault(root => root.name == BoundaryName);
            if (boundary != null)
                Build(scene, boundary.transform);
        }

        /// <summary>맵을 내릴 때 같이 사라지도록 안개 묶음을 맵 씬으로 옮긴다.</summary>
        private static void Build(Scene scene, Transform boundary)
        {
            List<BoxCollider> walls = boundary.GetComponentsInChildren<BoxCollider>(true)
                .Where(box => box.name.StartsWith(WallPrefix))
                .ToList();
            Material material = GetMaterial();
            if (walls.Count == 0 || material == null)
                return;

            Bounds area = walls[0].bounds;
            foreach (BoxCollider wall in walls)
                area.Encapsulate(wall.bounds);

            var fogRoot = new GameObject("Boundary Fog");
            SceneManager.MoveGameObjectToScene(fogRoot, scene);
            float layerSpacing = GetLayerSpacing();
            foreach (BoxCollider wall in walls)
                CreateWallLayers(fogRoot.transform, wall.bounds, area.center, layerSpacing, material);
        }

        /// <summary>경계 벽은 축에 맞춘 얇은 상자라서, 얇은 축의 맵 쪽 면부터 바깥으로 겹을 쌓는다.</summary>
        private static void CreateWallLayers(Transform parent, Bounds wallBounds, Vector3 areaCenter, float layerSpacing, Material material)
        {
            bool isThinOnX = wallBounds.size.x < wallBounds.size.z;
            Vector3 inward = isThinOnX
                ? new Vector3(Mathf.Sign(areaCenter.x - wallBounds.center.x), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(areaCenter.z - wallBounds.center.z));
            Vector3 innerFace = wallBounds.center + Vector3.Scale(inward, wallBounds.extents);
            float wallLength = isThinOnX ? wallBounds.size.z : wallBounds.size.x;
            Quaternion facing = Quaternion.LookRotation(inward);

            for (int layer = 0; layer < LayerCount; layer++)
            {
                float outwardOffset = layerSpacing * layer;
                var fogLayer = new GameObject($"FogLayer_{layer}");
                fogLayer.transform.SetParent(parent, false);
                fogLayer.transform.SetPositionAndRotation(innerFace - inward * outwardOffset, facing);
                // 바깥 겹은 모서리에서 틈이 보이지 않도록 밀려난 만큼 양옆을 늘린다.
                fogLayer.transform.localScale = new Vector3(wallLength + outwardOffset * 2f, wallBounds.size.y, 1f);

                fogLayer.AddComponent<MeshFilter>().sharedMesh = GetQuadMesh();
                MeshRenderer fogRenderer = fogLayer.AddComponent<MeshRenderer>();
                fogRenderer.sharedMaterial = material;
                fogRenderer.shadowCastingMode = ShadowCastingMode.Off;
                fogRenderer.receiveShadows = false;
                fogRenderer.lightProbeUsage = LightProbeUsage.Off;
                fogRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        /// <summary>겹을 모두 겹친 불투명도가 설정값이 되도록 한 겹의 불투명도를 거꾸로 구한다.</summary>
        private static Material GetMaterial()
        {
            if (fogMaterial != null)
                return fogMaterial;

            var shader = Resources.Load<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogWarning($"[BoundaryFog] Resources/{ShaderPath} 셰이더를 찾지 못해 경계 안개를 만들지 않습니다.");
                return null;
            }

            fogMaterial = new Material(shader) { name = "M_BoundaryFog (Runtime)" };
            float layerOpacity = 1f - Mathf.Pow(1f - GameTuning.BoundaryFogOpacity, 1f / LayerCount);
            fogMaterial.SetFloat(OpacityId, layerOpacity);
            fogMaterial.SetFloat(SoftDistanceId, Mathf.Max(1f, GetLayerSpacing() * SoftDistancePerSpacing));
            return fogMaterial;
        }

        /// <summary>겹 배치와 부드럽게 덮는 거리가 같은 간격을 써야 해서 한곳에서 구한다.</summary>
        private static float GetLayerSpacing()
        {
            return GameTuning.BoundaryFogDepth / Mathf.Max(1, LayerCount - 1);
        }

        /// <summary>충돌체가 생기면 레이·겹침 검사에 걸리므로 기본 Quad 대신 메시만 직접 만든다.</summary>
        private static Mesh GetQuadMesh()
        {
            if (quadMesh != null)
                return quadMesh;

            quadMesh = new Mesh { name = "BoundaryFogQuad" };
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
