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
    /// <summary>불러올 때마다 안개 면을 만들지 않고 씬 뷰에서도 보이도록, 맵 씬의 경계 벽 안쪽에 안개 겹을 미리 둔다.</summary>
    public static class BoundaryFogBaker
    {
        private const string AssetFolder = "Assets/_Project/Art/Baked/BoundaryFog";
        private const string ShaderPath = "Assets/_Project/Resources/Shaders/BoundaryFog.shader";

        /// <summary>
        /// 맵 생성 도구가 경계를 만든 직후 부르도록 경계를 직접 받는다. 다시 실행해도 겹치지 않게 같은 씬의 이전 안개를 지운다.
        /// </summary>
        public static int BakeFor(Transform boundary)
        {
            Scene scene = boundary.gameObject.scene;
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.GetComponent<BoundaryFogLayers>() != null)
                    Object.DestroyImmediate(rootObject);
            }

            BoundaryFogLayers fog = BoundaryFogLayers.Create(boundary, GetQuadMesh(), GetMaterial());
            if (fog == null)
                return 0;
            SceneManager.MoveGameObjectToScene(fog.gameObject, scene);
            return fog.GetComponentsInChildren<MeshRenderer>(true).Length;
        }

        /// <summary>이미 만든 맵을 다시 생성하지 않고, 씬을 하나씩 열어 안개만 넣고 저장한다.</summary>
        public static void BakeAllMaps(List<string> report)
        {
            string returnScenePath = SceneManager.GetActiveScene().path;
            foreach (string scenePath in TargetScenes())
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                GameObject boundary = scene.GetRootGameObjects().FirstOrDefault(rootObject => rootObject.name == BoundaryFogLayers.BoundaryName);
                if (boundary == null)
                {
                    report.Add($"[{scene.name}] 경계가 없어 건너뜀");
                    continue;
                }

                int layerCount = BakeFor(boundary.transform);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                report.Add($"[{scene.name}] 경계 안개 {layerCount}장");
            }

            if (!string.IsNullOrEmpty(returnScenePath) && File.Exists(returnScenePath))
                EditorSceneManager.OpenScene(returnScenePath, OpenSceneMode.Single);
        }

        /// <summary>테스트 씬도 같은 경계를 쓰므로 함께 넣고, 파일이 없는 맵은 뺀다.</summary>
        private static IEnumerable<string> TargetScenes()
        {
            return MapCatalog.AllMaps.Select(map => map.ScenePath)
                .Append(TestEnvironmentSetupTool.ScenePath)
                .Where(File.Exists)
                .Distinct();
        }

        /// <summary>씬마다 메시를 새로 넣으면 씬 파일이 커지므로 하나의 에셋을 같이 쓴다.</summary>
        private static Mesh GetQuadMesh()
        {
            string path = $"{AssetFolder}/BoundaryFogQuad.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
                return mesh;

            Directory.CreateDirectory(AssetFolder);
            mesh = BoundaryFogLayers.CreateQuadMesh();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>불투명도는 실행 중에 렌더러별로 넣지만, 씬 뷰에서도 비슷하게 보이도록 지금 조정값을 머티리얼에 적어 둔다.</summary>
        private static Material GetMaterial()
        {
            string path = $"{AssetFolder}/M_BoundaryFog.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
                if (shader == null)
                {
                    Debug.LogWarning($"[BoundaryFogBaker] {ShaderPath} 셰이더를 찾지 못해 경계 안개를 넣지 않습니다.");
                    return null;
                }
                Directory.CreateDirectory(AssetFolder);
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            BoundaryFogLayers.ApplyDefaults(material);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
