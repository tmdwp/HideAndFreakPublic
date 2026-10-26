using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 소품을 벽에 붙일 때 기준이 되는 벽 방향.
    /// </summary>
    internal enum MapSide { North, South, East, West }

    /// <summary>
    /// 직접 설계하는 맵 빌더(지하 연구소·지하철역 등)가 함께 쓰는 배치 도우미.
    /// - 소품 놓기(크기 단위 보정 + 변신 사물 후보 표시), 벽에 붙이기, 받침 위에 올리기
    /// - Environment 아래 구역 묶음 만들기
    /// 실제 에셋 배치·머티리얼 교체는 <see cref="MapPiecePlacer"/>가 맡는다.
    /// </summary>
    internal class MapLayoutTools
    {
        /// <summary>벽 중심선에서 소품까지 더 띄울 틈. (벽 두께 절반 0.1m 바깥)</summary>
        public const float WallGap = 0.12f;

        public MapPiecePlacer placer;
        public Transform environment;

        /// <summary>크기 단위가 달라 높이를 맞춘 에셋 기록. (보고서용)</summary>
        public readonly List<string> sizeFixes = new List<string>();

        /// <summary>MapKit의 길쭉한 모델(벽·난간)이 X축을 따라 놓이도록 더할 회전.</summary>
        public float lengthYaw;

        /// <summary>Environment 아래 이름으로 묶음을 찾거나 만든다.</summary>
        public Transform Group(string name)
        {
            Transform group = environment.Find(name);
            if (group == null)
            {
                group = new GameObject(name).transform;
                group.SetParent(environment, false);
            }
            return group;
        }

        /// <summary>
        /// 소품을 놓는다. 높이가 예상(typicalHeight)과 2.5배 넘게 다르면 단위 문제로 보고 예상 높이로 맞춘다.
        /// transformable이면 변신 사물 후보로 표시한다.
        /// </summary>
        public GameObject Prop(Transform group, string path, float x, float z, float yaw, float typicalHeight,
            float baseY = 0f, bool transformable = true)
        {
            var floorCenter = new Vector3(x, baseY, z);
            GameObject placed = placer.Place(path, group, floorCenter, yaw);
            if (placed == null)
                return null;

            float height = MapPiecePlacer.GetBounds(placed).size.y;
            if (height > typicalHeight * 2.5f || height < typicalHeight / 2.5f)
            {
                Object.DestroyImmediate(placed);
                placed = placer.Place(path, group, floorCenter, yaw, fitHeight: typicalHeight);
                sizeFixes.Add($"{Path.GetFileNameWithoutExtension(path)}: 높이 {height:F2}m → {typicalHeight:F2}m로 맞춤");
            }
            return transformable ? placer.AsProp(placed) : placed;
        }

        /// <summary>
        /// 벽에 붙여 놓는다. along은 벽을 따라가는 좌표(북·남 벽은 x, 동·서 벽은 z), line은 벽 중심선 좌표.
        /// yaw를 주지 않으면 앞면(+Z)이 방 안을 보도록 돌린다.
        /// </summary>
        public GameObject Against(Transform group, string path, MapSide side, float line, float along, float typicalHeight,
            float? yaw = null, bool transformable = true, float baseY = 0f)
        {
            bool northSouth = side == MapSide.North || side == MapSide.South;
            float x = northSouth ? along : line;
            float z = northSouth ? line : along;
            float facing = yaw ?? side switch
            {
                MapSide.North => 180f,
                MapSide.South => 0f,
                MapSide.East => -90f,
                _ => 90f
            };

            GameObject placed = Prop(group, path, x, z, facing, typicalHeight, baseY, transformable);
            if (placed == null)
                return null;

            Bounds bounds = MapPiecePlacer.GetBounds(placed);
            float face = 0.1f + WallGap;
            Vector3 shift = side switch
            {
                MapSide.North => new Vector3(0f, 0f, line - face - bounds.max.z),
                MapSide.South => new Vector3(0f, 0f, line + face - bounds.min.z),
                MapSide.East => new Vector3(line - face - bounds.max.x, 0f, 0f),
                _ => new Vector3(line + face - bounds.min.x, 0f, 0f)
            };
            placed.transform.position += shift;
            return placed;
        }

        /// <summary>
        /// 받침(책상 등) 위에 작은 소품을 올리고 변신 사물 후보로 표시한다.
        /// </summary>
        public GameObject Decorate(GameObject support, string path, Vector2 offset, float yaw, float typicalHeight)
        {
            if (support == null)
                return null;
            Bounds top = MapPiecePlacer.GetBounds(support);
            return Prop(support.transform.parent, path, top.center.x + offset.x, top.center.z + offset.y, yaw, typicalHeight, baseY: top.max.y);
        }

        /// <summary>
        /// MapKit 벽 모델이 X축을 따라 길게 놓이려면 더할 회전(0 또는 90도). 임포트 축이 달라도 벽이 맞게 놓이도록 잰다.
        /// </summary>
        public static float MeasureLengthYaw(string path)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
                return 0f;
            GameObject probe = Object.Instantiate(asset);
            try
            {
                Vector3 size = MapPiecePlacer.GetBounds(probe).size;
                return size.z > size.x ? 90f : 0f;
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        /// <summary>
        /// 오브젝트의 모든 렌더러·슬롯을 한 머티리얼로 칠한다.
        /// </summary>
        public static void Paint(GameObject target, Material material)
        {
            if (target == null || material == null)
                return;
            foreach (Renderer part in target.GetComponentsInChildren<Renderer>(true))
                part.sharedMaterials = Enumerable.Repeat(material, part.sharedMaterials.Length).ToArray();
        }

        /// <summary>
        /// 크기·위치를 월드 기준으로 정한 상자를 만든다. collider가 false면 충돌체를 지우고 그림자도 끈다. (장식용)
        /// </summary>
        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material material, bool collider = true)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = center;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider)
            {
                Object.DestroyImmediate(box.GetComponent<Collider>());
                box.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return box;
        }

        /// <summary>
        /// 부모 아래 빈 오브젝트를 만든다.
        /// </summary>
        public static Transform CreateChild(Transform parent, string name)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }
    }

    /// <summary>
    /// 맵 빌더용 URP Lit 머티리얼을 폴더에 만들거나 불러온다. 다시 실행하면 색·값만 갱신한다.
    /// </summary>
    internal sealed class MapMaterialFactory
    {
        private readonly string folder;

        /// <param name="folder">머티리얼을 둘 폴더("Assets/..."). 없으면 상위 폴더까지 만든다.</param>
        public MapMaterialFactory(string folder)
        {
            this.folder = folder;
            EnsureFolder(folder);
        }

        /// <summary>
        /// 에셋 폴더가 없으면 상위 폴더부터 차례로 만든다.
        /// </summary>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>불투명 URP Lit 머티리얼.</summary>
        public Material Opaque(string name, Color color, float metallic, float smoothness)
        {
            Material material = GetOrCreate(name);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>발광 URP Lit 머티리얼.</summary>
        public Material Emissive(string name, Color color, Color emission)
        {
            Material material = Opaque(name, color, 0f, 0.6f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            // None이면 URP 머티리얼 검증이 발광 없음으로 보고 _EMISSION 키워드를 지운다. (발광 색만 남고 빛나지 않음)
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>반투명 URP Lit 머티리얼. (알파 블렌딩, 깊이 기록 안 함)</summary>
        public Material Transparent(string name, Color color)
        {
            Material material = Opaque(name, color, 0f, 0.92f);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>경로에 머티리얼이 있으면 불러오고, 없으면 URP Lit으로 만든다.</summary>
        private Material GetOrCreate(string name)
        {
            string path = $"{folder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
