using System.Linq;
using GuardVsMannequin.Mods;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Map
{
    /// <summary>
    /// 안개를 미리 두지 않은 맵(모드 맵·예전 맵)에도 적용되도록, 불러온 맵의 투명 경계 벽 안쪽에 흰 안개 면을 실행 중에 붙인다.
    /// 미리 둔 안개(BoundaryFogLayers)가 있는 맵은 건드리지 않는다.
    /// </summary>
    public static class BoundaryFog
    {
        private const string ShaderPath = "Shaders/BoundaryFog";

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

        /// <summary>룸·메뉴 씬, 검사에 걸린 모드 맵, 이미 안개를 둔 맵에는 붙이지 않는다.</summary>
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!ModSceneGuard.Approve(scene))
                return;

            GameObject[] roots = scene.GetRootGameObjects();
            if (!roots.Any(root => root.GetComponentInChildren<MapInfo>(true) != null))
                return;
            if (roots.Any(root => root.GetComponentInChildren<BoundaryFogLayers>(true) != null))
                return;

            GameObject boundary = roots.FirstOrDefault(root => root.name == BoundaryFogLayers.BoundaryName);
            if (boundary == null)
                return;

            quadMesh ??= BoundaryFogLayers.CreateQuadMesh();
            BoundaryFogLayers fog = BoundaryFogLayers.Create(boundary.transform, quadMesh, GetMaterial());
            // 맵을 내릴 때 같이 사라지도록 안개 묶음을 맵 씬으로 옮긴다.
            if (fog != null)
                SceneManager.MoveGameObjectToScene(fog.gameObject, scene);
        }

        /// <summary>불투명도는 렌더러별 속성으로 넣으므로 머티리얼은 모든 맵이 하나를 같이 쓴다.</summary>
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
            return fogMaterial;
        }
    }
}
