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

    /// <summary>직접 설계하는 맵 빌더들이 소품·벽·계단 배치 코드를 반복하지 않도록 모은 도우미.</summary>
    internal class MapLayoutTools
    {
        /// <summary>벽 중심선에서 소품까지 더 띄울 틈.</summary>
        public const float WallGap = 0.12f;

        public MapPiecePlacer placer;
        public Transform environment;

        /// <summary>보고서에 쓰려고 크기 단위가 달라 높이를 맞춘 에셋을 기록한다.</summary>
        public readonly List<string> sizeFixes = new List<string>();

        /// <summary>MapKit의 길쭉한 모델(벽·난간)이 X축을 따라 놓이도록 더할 회전.</summary>
        public float lengthYaw;

        /// <summary>
        /// 구역별로 묶어 두면 Hierarchy에서 찾기 쉽고 구역 단위로 지울 수 있으므로, 같은 이름이 있으면 재사용하고 없으면 만든다.
        /// </summary>
        public Transform Group(string groupName)
        {
            Transform group = environment.Find(groupName);
            if (group == null)
            {
                group = new GameObject(groupName).transform;
                group.SetParent(environment, false);
            }
            return group;
        }

        /// <summary>에셋 팩마다 단위가 달라 높이가 예상과 크게 다르면 예상 높이로 맞추고, 변신 대상은 후보로만 표시한다.</summary>
        public GameObject Prop(Transform group, string path, float x, float z, float yaw, float typicalHeight,
            float baseY = 0f, bool transformable = true)
        {
            var floorCenter = new Vector3(x, baseY, z);
            GameObject placed = placer.Place(path, group, floorCenter, yaw);
            if (placed == null)
                return null;

            float placedHeight = MapPiecePlacer.GetBounds(placed).size.y;
            if (placedHeight > typicalHeight * 2.5f || placedHeight < typicalHeight / 2.5f)
            {
                Object.DestroyImmediate(placed);
                placed = placer.Place(path, group, floorCenter, yaw, fitHeight: typicalHeight);
                sizeFixes.Add($"{Path.GetFileNameWithoutExtension(path)}: 높이 {placedHeight:F2}m → {typicalHeight:F2}m로 맞춤");
            }
            return transformable ? placer.AsProp(placed) : placed;
        }

        /// <summary>모델마다 깊이가 달라 벽에 박히거나 뜨지 않도록 놓은 뒤 경계를 재서 벽 면에 붙인다.</summary>
        public GameObject Against(Transform group, string path, MapSide side, float line, float along, float typicalHeight,
            float? yaw = null, bool transformable = true, float baseY = 0f)
        {
            bool isNorthSouth = side == MapSide.North || side == MapSide.South;
            float x = isNorthSouth ? along : line;
            float z = isNorthSouth ? line : along;
            float facingYaw = yaw ?? side switch
            {
                MapSide.North => 180f,
                MapSide.South => 0f,
                MapSide.East => -90f,
                _ => 90f
            };

            GameObject placed = Prop(group, path, x, z, facingYaw, typicalHeight, baseY, transformable);
            if (placed == null)
                return null;

            Bounds placedBounds = MapPiecePlacer.GetBounds(placed);
            float wallFaceOffset = 0.1f + WallGap;
            Vector3 shift = side switch
            {
                MapSide.North => new Vector3(0f, 0f, line - wallFaceOffset - placedBounds.max.z),
                MapSide.South => new Vector3(0f, 0f, line + wallFaceOffset - placedBounds.min.z),
                MapSide.East => new Vector3(line - wallFaceOffset - placedBounds.max.x, 0f, 0f),
                _ => new Vector3(line + wallFaceOffset - placedBounds.min.x, 0f, 0f)
            };
            placed.transform.position += shift;
            return placed;
        }

        /// <summary>높이를 손으로 맞추지 않도록 받침 윗면에 올리고, 숨기 좋은 작은 물건이라 변신 후보로 표시한다.</summary>
        public GameObject Decorate(GameObject support, string path, Vector2 offset, float yaw, float typicalHeight)
        {
            if (support == null)
                return null;
            Bounds supportBounds = MapPiecePlacer.GetBounds(support);
            return Prop(support.transform.parent, path, supportBounds.center.x + offset.x, supportBounds.center.z + offset.y, yaw, typicalHeight, baseY: supportBounds.max.y);
        }

        /// <summary>
        /// 임포트 설정에 따라 벽 모델의 긴 축이 X일 수도 Z일 수도 있어, 실제로 놓아 경계를 잰 뒤 X축을 따라 놓이도록 더할 회전(0 또는 90도)을 정한다.
        /// </summary>
        public static float MeasureLengthYaw(string path)
        {
            GameObject sourceAsset = MapPiecePlacer.LoadAsset(path, out _);
            if (sourceAsset == null)
                return 0f;
            GameObject probe = Object.Instantiate(sourceAsset);
            try
            {
                Vector3 probeSize = MapPiecePlacer.GetBounds(probe).size;
                return probeSize.z > probeSize.x ? 90f : 0f;
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        /// <summary>
        /// 테마 색 하나로 통일할 구조물이 여러 슬롯을 가져서, 슬롯 수를 유지한 채 모두 같은 머티리얼로 칠한다.
        /// </summary>
        public static void Paint(GameObject target, Material material)
        {
            if (target == null || material == null)
                return;
            foreach (Renderer part in target.GetComponentsInChildren<Renderer>(true))
                part.sharedMaterials = Enumerable.Repeat(material, part.sharedMaterials.Length).ToArray();
        }

        /// <summary>맵 좌표를 월드 기준으로 설계하므로 부모와 무관하게 월드 위치·크기로 놓는다.</summary>
        public static GameObject Box(Transform parent, string boxName, Vector3 center, Vector3 size, Material material, bool collider = true)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = boxName;
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
        /// 그룹 오브젝트를 만드는 코드가 반복되어 한 줄로 쓰려고 둔다.
        /// </summary>
        public static Transform CreateChild(Transform parent, string childName)
        {
            Transform child = new GameObject(childName).transform;
            child.SetParent(parent, false);
            return child;
        }

        /// <summary>소품의 화면·발광판은 맵마다 색이 달라 머티리얼 이름으로 찾아 바꿔 끼운다.</summary>
        public static void ReplaceMaterials(Transform root, IReadOnlyDictionary<string, Material> remap)
        {
            foreach (Renderer childRenderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = childRenderer.sharedMaterials;
                bool isChanged = false;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != null && remap.TryGetValue(slots[i].name, out Material replacement))
                    {
                        slots[i] = replacement;
                        isChanged = true;
                    }
                }
                if (isChanged)
                    childRenderer.sharedMaterials = slots;
            }
        }

        /// <summary>계단 모델은 높이가 맞지 않는 경우가 많고 디딤판만 놓으면 빠질 수 있어 단마다 바닥까지 채운 상자로 만든다.</summary>
        /// <param name="stepRise">목표 단 높이(실제 높이는 rise를 나누어 떨어지게 맞춤).</param>
        /// <param name="stepRun">단 길이(디딤판 깊이).</param>
        public static void Stairs(Transform parent, Material material, Vector3 bottom, Vector3 direction, float rise, float width,
            float stepRise = 0.25f, float stepRun = 0.35f)
        {
            int stepCount = Mathf.Max(1, Mathf.RoundToInt(rise / stepRise));
            float actualRise = rise / stepCount;
            bool isAlongX = Mathf.Abs(direction.x) > 0.5f;
            for (int i = 0; i < stepCount; i++)
            {
                float stepTopY = bottom.y + actualRise * (i + 1);
                Vector3 stepCenter = bottom + direction * (stepRun * (i + 0.5f));
                stepCenter.y = (bottom.y + stepTopY) / 2f;
                Vector3 stepSize = isAlongX ? new Vector3(stepRun, stepTopY - bottom.y, width) : new Vector3(width, stepTopY - bottom.y, stepRun);
                Box(parent, "Step", stepCenter, stepSize, material);
            }
        }

        /// <summary>난간 모델은 길이를 늘리면 늘어나 보이므로 손잡이와 아래 판 상자로 만든다.</summary>
        public static void RailingRun(Transform parent, Material material, bool alongX, float line, float from, float to, float floorY)
        {
            float runLength = to - from;
            float runMiddle = (from + to) / 2f;
            // 난간 축 기준 높이·두께를 월드 위치/크기로 바꾼다.
            Vector3 RailCenter(float centerY) => alongX ? new Vector3(runMiddle, centerY, line) : new Vector3(line, centerY, runMiddle);
            Vector3 RailSize(float railHeight, float railThickness) => alongX ? new Vector3(runLength, railHeight, railThickness) : new Vector3(railThickness, railHeight, runLength);
            Box(parent, "RailTop", RailCenter(floorY + 1f), RailSize(0.06f, 0.08f), material);
            Box(parent, "RailPanel", RailCenter(floorY + 0.5f), RailSize(0.9f, 0.04f), material);
        }

        /// <summary>벽 모델을 늘리면 텍스처가 늘어나므로 상자로 벽을 세우고, 오클루전에 유리하도록 4m 이하 조각으로 나눈다.</summary>
        /// <param name="glass">유리 창(gap.glass)에 쓸 머티리얼.</param>
        public static void WallRun(Transform parent, bool alongX, float line, float from, float to, float yBottom, float yTop,
            Material material, Material glass, float thickness, System.Action<GameObject> onSolidPiece, params MapWallGap[] gaps)
        {
            float wallHeight = yTop - yBottom;
            // 벽 축 기준 좌표를 월드 위치/크기로 바꾼다.
            Vector3 WallCenter(float alongPosition, float centerY) => alongX ? new Vector3(alongPosition, centerY, line) : new Vector3(line, centerY, alongPosition);
            Vector3 WallSize(float pieceLength, float pieceHeight, float pieceThickness) => alongX ? new Vector3(pieceLength, pieceHeight, pieceThickness) : new Vector3(pieceThickness, pieceHeight, pieceLength);

            // segmentStart~segmentEnd 구간을 최대 4m 조각으로 나눠 막힌 벽을 세운다.
            void BuildSolidSegment(float segmentStart, float segmentEnd)
            {
                int pieceCount = Mathf.Max(1, Mathf.CeilToInt((segmentEnd - segmentStart) / 4f - 0.01f));
                float pieceLength = (segmentEnd - segmentStart) / pieceCount;
                for (int i = 0; i < pieceCount; i++)
                {
                    float pieceStart = segmentStart + pieceLength * i;
                    GameObject piece = Box(parent, "Wall", WallCenter(pieceStart + pieceLength / 2f, yBottom + wallHeight / 2f), WallSize(pieceLength, wallHeight, thickness), material);
                    onSolidPiece?.Invoke(piece);
                }
            }

            float cursor = from;
            foreach (MapWallGap gap in gaps.OrderBy(wallGap => wallGap.center))
            {
                float gapStart = Mathf.Max(from, gap.center - gap.width / 2f);
                float gapEnd = Mathf.Min(to, gap.center + gap.width / 2f);
                if (gapStart - cursor > 0.01f)
                    BuildSolidSegment(cursor, gapStart);
                float gapLength = gapEnd - gapStart;
                float gapMiddle = (gapStart + gapEnd) / 2f;
                if (gap.bottom > 0f)
                    Box(parent, "Sill", WallCenter(gapMiddle, yBottom + gap.bottom / 2f), WallSize(gapLength, gap.bottom, thickness), material);
                float gapTop = gap.bottom + gap.height;
                if (gapTop < wallHeight - 0.01f)
                    Box(parent, "Lintel", WallCenter(gapMiddle, yBottom + (gapTop + wallHeight) / 2f), WallSize(gapLength, wallHeight - gapTop, thickness), material);
                if (gap.glass)
                {
                    GameObject glassPane = Box(parent, "Glass", WallCenter(gapMiddle, yBottom + gap.bottom + gap.height / 2f),
                        WallSize(gapLength, gap.height, 0.05f), glass);
                    glassPane.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                cursor = gapEnd;
            }
            if (to - cursor > 0.01f)
                BuildSolidSegment(cursor, to);
        }
    }

    /// <summary>벽에 낸 출입구·창.</summary>
    internal readonly struct MapWallGap
    {
        public readonly float center, width, bottom, height;
        public readonly bool glass;

        /// <summary>문·창·통로 외의 특수한 틈도 만들 수 있도록 모든 값을 받는 생성자를 열어 둔다.</summary>
        public MapWallGap(float center, float width, float bottom, float height, bool glass)
        {
            this.center = center;
            this.width = width;
            this.bottom = bottom;
            this.height = height;
            this.glass = glass;
        }

        /// <summary>문 크기가 맵마다 다르면 캐릭터가 끼이므로 기본값을 한곳에 둔다.</summary>
        public static MapWallGap Door(float center, float width = 1.4f) => new MapWallGap(center, width, 0f, 2.4f, false);

        /// <summary>창 높이가 맵마다 다르면 어색하므로 기본값을 한곳에 둔다.</summary>
        public static MapWallGap Window(float center, float width = 3f) => new MapWallGap(center, width, 1f, 1.2f, true);

        /// <summary>복도 연결처럼 문틀 없이 뚫린 통로는 높이가 제각각이라 지정한 높이로 만든다.</summary>
        public static MapWallGap Opening(float center, float width, float height) => new MapWallGap(center, width, 0f, height, false);
    }

    /// <summary>다시 실행해도 참조가 끊기지 않도록 머티리얼이 있으면 색·값만 갱신한다.</summary>
    internal sealed class MapMaterialFactory
    {
        private readonly string folder;

        /// <summary>
        /// 머티리얼 저장이 폴더가 없으면 실패하므로 생성할 때 폴더를 미리 만든다.
        /// </summary>
        /// <param name="folder">머티리얼을 둘 폴더(없으면 만듦).</param>
        public MapMaterialFactory(string folder)
        {
            this.folder = folder;
            EnsureFolder(folder);
        }

        /// <summary>
        /// AssetDatabase.CreateFolder는 한 단계씩만 만들 수 있어 상위 폴더부터 재귀로 만든다.
        /// </summary>
        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;
            string parentPath = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parentPath))
                EnsureFolder(parentPath);
            AssetDatabase.CreateFolder(parentPath, Path.GetFileName(folderPath));
        }

        /// <summary>빌더를 다시 실행하면 색 조정이 반영되도록, 이미 있는 머티리얼도 값을 새로 쓴다.</summary>
        public Material Opaque(string materialName, Color color, float metallic, float smoothness)
        {
            Material material = GetOrCreate(materialName);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>어두운 사냥 시간에도 화면·표지판이 보이도록 발광을 켠다.</summary>
        public Material Emissive(string materialName, Color color, Color emission)
        {
            Material material = Opaque(materialName, color, 0f, 0.6f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            // None이면 URP가 _EMISSION 키워드를 지워 빛나지 않는다.
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>속성 값만 바꾸면 블렌드·렌더 큐가 따라 바뀌지 않으므로 유리용 알파 블렌딩 설정을 직접 맞춘다.</summary>
        public Material Transparent(string materialName, Color color)
        {
            Material material = Opaque(materialName, color, 0f, 0.92f);
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

        /// <summary>GUID가 바뀌면 씬의 머티리얼 참조가 끊기므로 있으면 다시 만들지 않고 불러온다.</summary>
        private Material GetOrCreate(string materialName)
        {
            string materialPath = $"{folder}/{materialName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null)
                return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = materialName };
            AssetDatabase.CreateAsset(material, materialPath);
            return material;
        }
    }
}
