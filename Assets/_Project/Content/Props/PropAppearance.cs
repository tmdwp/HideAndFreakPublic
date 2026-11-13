using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Props
{
    /// <summary>사물마다 외형 에셋을 만들지 않도록 맵에 놓인 물체의 메시·머티리얼을 사물 기준 공간으로 복사해 둔다.</summary>
    public sealed class PropAppearance
    {
        /// <summary>사물 기준 공간의 위치·회전·크기를 가진 외형 조각 하나.</summary>
        public readonly struct Part
        {
            public readonly Mesh Mesh;
            public readonly Material[] Materials;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 Scale;

            /// <summary>변신 중 원본 사물이 옮겨져도 영향받지 않도록 배치를 값으로 복사해 둔다.</summary>
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

        /// <summary>비어 있으면 변신할 수 없다.</summary>
        public IReadOnlyList<Part> Parts => parts;

        /// <summary>변신 배치와 충돌체 크기 계산에 쓰는 사물 기준 공간의 경계.</summary>
        public Bounds Bounds { get; }

        /// <summary>크기 제한 판정에 쓰는 크기(m).</summary>
        public float LargestSize => Mathf.Max(Bounds.size.x, Bounds.size.y, Bounds.size.z);

        /// <summary>조각과 경계가 어긋난 외형이 만들어지지 않도록 생성은 <see cref="Capture"/>로만 하게 막는다.</summary>
        private PropAppearance(List<Part> parts, Bounds bounds)
        {
            this.parts = parts;
            Bounds = bounds;
        }

        /// <summary>맵에 놓인 모습을 그대로 복사하되, 정적 배칭된 메시는 원래 모양을 얻을 수 없어 경고한다.</summary>
        public static PropAppearance Capture(TransformableProp prop)
        {
            List<MeshRenderer> renderers = CollectRenderers(prop);

            // 좌우 방향만 뺀 기준 공간: 사물 기준점 + 월드 Y축 회전
            Matrix4x4 toLocal = Matrix4x4.TRS(prop.transform.position, YawOf(prop.transform.rotation), Vector3.one).inverse;

            var parts = new List<Part>(renderers.Count);
            Bounds bounds = default;
            foreach (MeshRenderer meshRenderer in renderers)
            {
                Mesh mesh = meshRenderer.GetComponent<MeshFilter>().sharedMesh;
                if (meshRenderer.isPartOfStaticBatch)
                    Debug.LogWarning($"[PropAppearance] '{meshRenderer.name}'이 정적 배칭되어 외형을 정확히 복사할 수 없습니다. 맵 최적화를 다시 실행하세요.", meshRenderer);

                Matrix4x4 partToLocal = toLocal * meshRenderer.transform.localToWorldMatrix;
                parts.Add(new Part(mesh, meshRenderer.sharedMaterials, partToLocal.GetPosition(), partToLocal.rotation, partToLocal.lossyScale));
                Encapsulate(ref bounds, parts.Count == 1, mesh.bounds, partToLocal);
            }
            return new PropAppearance(parts, parts.Count > 0 ? bounds : new Bounds(Vector3.up * 0.5f, Vector3.one));
        }

        /// <summary>같은 모양이 겹쳐 그려지지 않도록 하위 LOD·다른 변신 사물의 자식·꺼진 렌더러는 뺀다.</summary>
        private static List<MeshRenderer> CollectRenderers(TransformableProp prop)
        {
            var lowerLods = new HashSet<Renderer>();
            foreach (LODGroup group in prop.GetComponentsInChildren<LODGroup>())
            {
                LOD[] lods = group.GetLODs();
                for (int i = 1; i < lods.Length; i++)
                    lowerLods.UnionWith(lods[i].renderers);
            }

            var renderers = new List<MeshRenderer>();
            foreach (MeshRenderer meshRenderer in prop.GetComponentsInChildren<MeshRenderer>())
            {
                if (!meshRenderer.enabled || lowerLods.Contains(meshRenderer))
                    continue;
                if (meshRenderer.GetComponentInParent<TransformableProp>() != prop)
                    continue;
                MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                    renderers.Add(meshRenderer);
            }
            return renderers;
        }

        /// <summary>섞여 옮겨진 사물도 서버가 변신 거리를 검증하도록 지금 위치의 월드 경계로 바꾼다.</summary>
        /// <param name="localBounds">사물 기준 공간의 경계.</param>
        /// <param name="position">사물 기준점의 지금 월드 위치.</param>
        /// <param name="rotation">사물의 지금 월드 회전(좌우 방향만 씀).</param>
        public static Bounds ToWorld(Bounds localBounds, Vector3 position, Quaternion rotation)
        {
            Matrix4x4 toWorld = Matrix4x4.TRS(position, YawOf(rotation), Vector3.one);
            Bounds worldBounds = default;
            Encapsulate(ref worldBounds, true, localBounds, toWorld);
            return worldBounds;
        }

        /// <summary>회전된 상자는 중심·크기만 바꾸면 틀리므로 꼭짓점 8개를 모두 변환해 감싼다.</summary>
        private static void Encapsulate(ref Bounds bounds, bool isFirst, Bounds meshBounds, Matrix4x4 matrix)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = meshBounds.center + Vector3.Scale(meshBounds.extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 transformedCorner = matrix.MultiplyPoint3x4(corner);
                if (isFirst && i == 0)
                    bounds = new Bounds(transformedCorner, Vector3.zero);
                else
                    bounds.Encapsulate(transformedCorner);
            }
        }

        /// <summary>세워 둔 사물이 변신 후 눕지 않도록 기울기는 두고 좌우 방향만 뽑는다.</summary>
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
