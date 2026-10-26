using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Props
{
    /// <summary>
    /// 변신 사물의 외형을 맵에 놓인 물체에서 그대로 복사한 정보. (메시·머티리얼·실제 크기·기울기, 자식 조각 포함)
    /// 좌표는 "사물 기준점에서 좌우 방향(월드 Y축 회전)만 뺀" 공간이다. 변신하면 마네킹이 바라보는 방향으로 놓인다.
    /// 벽으로 지정된 사물은 원래 크기와 관계없이 고정 크기 벽 조각(그 벽의 첫 번째 머티리얼)이 된다.
    /// 외형은 사물 기준 공간이라 사물이 옮겨져도(판마다 섞기) 그대로 쓸 수 있으므로 사물마다 한 번만 만든다(<see cref="TransformableProp.Appearance"/>).
    /// </summary>
    public sealed class PropAppearance
    {
        /// <summary>벽으로 변신할 때의 고정 크기(가로 1.2 × 높이 2 × 두께 0.25m).</summary>
        public static readonly Vector3 WallPieceSize = new(1.2f, 2f, 0.25f);

        /// <summary>외형 조각 하나. (사물 기준 공간의 위치·회전·크기)</summary>
        public readonly struct Part
        {
            public readonly Mesh Mesh;
            public readonly Material[] Materials;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 Scale;

            public Part(Mesh mesh, Material[] materials, Vector3 position, Quaternion rotation, Vector3 scale)
            {
                Mesh = mesh;
                Materials = materials;
                Position = position;
                Rotation = rotation;
                Scale = scale;
            }
        }

        private readonly List<Part> parts;

        /// <summary>외형 조각 목록. 비어 있으면 변신할 수 없다.</summary>
        public IReadOnlyList<Part> Parts => parts;

        /// <summary>사물 기준 공간에서 모든 조각을 감싸는 경계. (변신 배치와 충돌체 크기 계산)</summary>
        public Bounds Bounds { get; }

        /// <summary>
        /// 외형을 복사한 순간의 월드 경계. 사물이 그 뒤에 옮겨지면(판마다 섞기) 맞지 않으므로,
        /// 거리 검증은 <see cref="TransformableProp.CurrentWorldBounds"/>를 쓴다. (벽 조각은 옮기지 않아 이 값을 그대로 쓴다)
        /// </summary>
        public Bounds WorldBounds { get; }

        /// <summary>경계의 가장 긴 변(m).</summary>
        public float LargestSize => Mathf.Max(Bounds.size.x, Bounds.size.y, Bounds.size.z);

        private PropAppearance(List<Part> parts, Bounds bounds, Bounds worldBounds)
        {
            this.parts = parts;
            Bounds = bounds;
            WorldBounds = worldBounds;
        }

        /// <summary>
        /// 사물의 지금 모습을 복사한다. 하위 LOD, 다른 변신 사물에 속한 자식, 꺼진 렌더러는 뺀다.
        /// </summary>
        public static PropAppearance Capture(TransformableProp prop)
        {
            List<MeshRenderer> renderers = CollectRenderers(prop);
            Bounds worldBounds = EncapsulateWorld(renderers, prop.transform.position);

            if (prop.IsWall)
                return CaptureWallPiece(renderers, worldBounds);

            // 좌우 방향만 뺀 기준 공간: 사물 기준점 + 월드 Y축 회전
            Matrix4x4 toLocal = Matrix4x4.TRS(prop.transform.position, YawOf(prop.transform.rotation), Vector3.one).inverse;

            var parts = new List<Part>(renderers.Count);
            Bounds bounds = default;
            foreach (MeshRenderer meshRenderer in renderers)
            {
                Mesh mesh = meshRenderer.GetComponent<MeshFilter>().sharedMesh;
                if (meshRenderer.isPartOfStaticBatch)
                    Debug.LogWarning($"[PropAppearance] '{meshRenderer.name}'이 정적 배칭되어 외형을 정확히 복사할 수 없습니다. 맵 최적화를 다시 실행하세요.", meshRenderer);

                Matrix4x4 local = toLocal * meshRenderer.transform.localToWorldMatrix;
                parts.Add(new Part(mesh, meshRenderer.sharedMaterials, local.GetPosition(), local.rotation, local.lossyScale));
                Encapsulate(ref bounds, parts.Count == 1, mesh.bounds, local);
            }
            return new PropAppearance(parts, parts.Count > 0 ? bounds : new Bounds(Vector3.up * 0.5f, Vector3.one), worldBounds);
        }

        /// <summary>
        /// 벽 조각 외형: 기본 큐브를 고정 크기로 키우고 벽의 첫 번째 머티리얼을 입힌다.
        /// </summary>
        private static PropAppearance CaptureWallPiece(List<MeshRenderer> renderers, Bounds worldBounds)
        {
            var parts = new List<Part>(1);
            if (renderers.Count > 0)
            {
                Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                Material material = renderers[0].sharedMaterial;
                parts.Add(new Part(cube, new[] { material }, Vector3.zero, Quaternion.identity, WallPieceSize));
            }
            return new PropAppearance(parts, new Bounds(Vector3.zero, WallPieceSize), worldBounds);
        }

        /// <summary>
        /// 외형이 될 메시 렌더러를 모은다. (켜진 것만, 하위 LOD와 다른 변신 사물의 자식 제외)
        /// </summary>
        private static List<MeshRenderer> CollectRenderers(TransformableProp prop)
        {
            var lowerLods = new HashSet<Renderer>();
            foreach (LODGroup group in prop.GetComponentsInChildren<LODGroup>())
            {
                LOD[] lods = group.GetLODs();
                for (int i = 1; i < lods.Length; i++)
                    lowerLods.UnionWith(lods[i].renderers);
            }

            var result = new List<MeshRenderer>();
            foreach (MeshRenderer meshRenderer in prop.GetComponentsInChildren<MeshRenderer>())
            {
                if (!meshRenderer.enabled || lowerLods.Contains(meshRenderer))
                    continue;
                if (meshRenderer.GetComponentInParent<TransformableProp>() != prop)
                    continue;
                MeshFilter filter = meshRenderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                    result.Add(meshRenderer);
            }
            return result;
        }

        /// <summary>
        /// 렌더러들의 월드 경계를 합친다. 없으면 기준점 위치의 빈 경계.
        /// </summary>
        private static Bounds EncapsulateWorld(List<MeshRenderer> renderers, Vector3 fallbackCenter)
        {
            if (renderers.Count == 0)
                return new Bounds(fallbackCenter, Vector3.zero);

            Bounds result = renderers[0].bounds;
            for (int i = 1; i < renderers.Count; i++)
                result.Encapsulate(renderers[i].bounds);
            return result;
        }

        /// <summary>
        /// 사물 기준 공간의 경계를 지금 사물 위치·좌우 방향의 월드 경계로 바꾼다. (꼭짓점 8개 변환, 할당 없음)
        /// </summary>
        /// <param name="localBounds">사물 기준 공간의 경계. (<see cref="Bounds"/>)</param>
        /// <param name="position">사물 기준점의 지금 월드 위치.</param>
        /// <param name="rotation">사물의 지금 월드 회전. 좌우 방향만 쓴다.</param>
        public static Bounds ToWorld(Bounds localBounds, Vector3 position, Quaternion rotation)
        {
            Matrix4x4 toWorld = Matrix4x4.TRS(position, YawOf(rotation), Vector3.one);
            Bounds result = default;
            Encapsulate(ref result, true, localBounds, toWorld);
            return result;
        }

        /// <summary>
        /// 메시 경계 상자의 꼭짓점 8개를 변환해 경계에 더한다.
        /// </summary>
        private static void Encapsulate(ref Bounds bounds, bool isFirst, Bounds meshBounds, Matrix4x4 matrix)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = meshBounds.center + Vector3.Scale(meshBounds.extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 point = matrix.MultiplyPoint3x4(corner);
                if (isFirst && i == 0)
                    bounds = new Bounds(point, Vector3.zero);
                else
                    bounds.Encapsulate(point);
            }
        }

        /// <summary>
        /// 회전에서 좌우 방향(월드 Y축 회전)만 뽑는다. rotation = yaw * tilt 로 보고,
        /// rotation⁻¹ = tilt⁻¹ * yaw⁻¹ 의 Y축 비틀림 성분이 yaw⁻¹이다.
        /// 모델 파일이 눕혀져 있고 배치에서 세운 사물(예: X -90°)도 변신 후 그대로 서 있게 된다.
        /// </summary>
        public static Quaternion YawOf(Quaternion rotation)
        {
            Quaternion inverse = Quaternion.Inverse(rotation);
            Vector2 twist = new(inverse.y, inverse.w);
            // Y축 성분이 없으면(수평축 180° 뒤집힘) 좌우 방향을 정할 수 없으므로 회전 없음으로 본다.
            if (twist.sqrMagnitude < 1e-6f)
                return Quaternion.identity;

            twist.Normalize();
            return Quaternion.Inverse(new Quaternion(0f, twist.x, 0f, twist.y));
        }
    }
}
