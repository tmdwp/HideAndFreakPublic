using GuardVsMannequin.Core;
using GuardVsMannequin.Props;
using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 맵 오브젝트를 변신 가능 사물로 등록한다. (자동 맵 생성, 맵 제작 도우미, 이전 도구가 함께 쓴다)
    /// 등록은 <see cref="TransformableProp"/>을 붙이는 것뿐이다. 외형은 게임 중에 이 오브젝트(자식 포함)에서 그대로 복사하고,
    /// 번호는 씬 저장·빌드 때 <see cref="PropIndexer"/>가 매긴다.
    /// - 사물: 그 모습 그대로 변신
    /// - 벽: 고정 크기 벽 조각(1.2×2×0.25m, 첫 번째 머티리얼)으로 변신
    /// 조준 판정을 위해 충돌체가 없는 조각에 MeshCollider를 붙이고, 메시를 복사할 수 있도록 정적 배칭에서 뺀다.
    /// 튕겨나갈 수 있는 크기의 사물은 충돌체를 볼록으로 바꾼다(<see cref="PropColliderBaker"/>).
    /// </summary>
    public static class PropRegistrar
    {
        // 이보다 정점이 많고 변신 가능한 크기면 "벽 모양 가구"(책장·옷장·문 등)로 보고 벽 조각 대신 그대로 복사한다.
        private const int FurnitureMinVertices = 100;

        /// <summary>
        /// 오브젝트(자식 포함)를 변신 사물로 등록한다. 메시가 하나도 없으면 false.
        /// </summary>
        /// <param name="target">등록할 오브젝트. 여러 조각으로 된 물체는 부모를 넘긴다.</param>
        /// <param name="isWall">고정 크기 벽 조각으로 변신할지.</param>
        /// <param name="absorbChildren">true면 자식에 따로 붙은 변신 사물 표시를 지워 하나의 물체로 합친다.</param>
        public static bool Register(GameObject target, bool isWall, bool absorbChildren)
        {
            MeshFilter[] meshes = target.GetComponentsInChildren<MeshFilter>(true);
            if (!HasAnyMesh(meshes))
                return false;

            if (absorbChildren)
            {
                foreach (TransformableProp child in target.GetComponentsInChildren<TransformableProp>(true))
                {
                    if (child.gameObject != target)
                        Undo.DestroyObjectImmediate(child);
                }
            }

            TransformableProp prop = target.GetComponent<TransformableProp>();
            if (prop == null)
                prop = Undo.AddComponent<TransformableProp>(target);
            Undo.RecordObject(prop, "변신 사물 등록");
            prop.SetWall(isWall);
            EditorUtility.SetDirty(prop);

            // 직접 고른 물체(부모 단위)는 모든 조각을 조준할 수 있게 하고, 자동 등록은 원래 충돌체만 쓴다.
            PrepareParts(prop, addChildColliders: absorbChildren);
            return true;
        }

        /// <summary>
        /// 사물의 조각을 정적 배칭에서 빼고, 충돌체가 없으면 붙인다. (다른 변신 사물에 속한 자식은 건드리지 않음)
        /// </summary>
        /// <param name="addChildColliders">false면 사물 오브젝트 자신에만 충돌체를 붙인다. (자식 장식이 갑자기 막히지 않도록)</param>
        public static void PrepareParts(TransformableProp prop, bool addChildColliders)
        {
            ClearStaticFlags(prop.gameObject, prop.IsWall);
            foreach (MeshFilter filter in prop.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponentInParent<TransformableProp>() != prop)
                    continue;
                bool isSelf = filter.gameObject == prop.gameObject;
                if ((isSelf || addChildColliders) && filter.GetComponent<Collider>() == null)
                    Undo.AddComponent<MeshCollider>(filter.gameObject).sharedMesh = filter.sharedMesh;
                ClearStaticFlags(filter.gameObject, prop.IsWall);
            }

            // 총에 맞으면 튕겨나갈 수 있는 크기면 충돌체를 움직일 수 있는 모양(볼록)으로 바꾼다.
            PropColliderBaker.Bake(prop);
        }

        /// <summary>
        /// 벽 조건(높고 얇은 물체)에 맞아도 정점이 많고 변신 가능한 크기면 가구로 본다. (벽 조각 대신 그대로 복사)
        /// </summary>
        public static bool LooksLikeFurniture(MeshRenderer target)
        {
            MeshFilter filter = target.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                return false;

            Vector3 size = target.bounds.size;
            float longest = Mathf.Max(size.x, size.y, size.z);
            return filter.sharedMesh.vertexCount > FurnitureMinVertices && longest <= GameTuning.DisguiseMaxSize;
        }

        /// <summary>
        /// 메시가 있는 조각이 하나라도 있는지 확인한다.
        /// </summary>
        private static bool HasAnyMesh(MeshFilter[] meshes)
        {
            foreach (MeshFilter filter in meshes)
            {
                if (filter.sharedMesh != null)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 오브젝트의 정적 배칭 플래그를 끈다. (배칭되면 실행 중 메시가 맵 전체의 합쳐진 메시로 바뀌어 복사할 수 없다)
        /// 판마다 자리를 옮기는 사물(벽 조각 제외)은 오클루전 정적 플래그도 끈다. (옮긴 뒤 엉뚱하게 가려지지 않도록)
        /// </summary>
        private static void ClearStaticFlags(GameObject target, bool isWall)
        {
            StaticEditorFlags remove = StaticEditorFlags.BatchingStatic;
            if (!isWall)
                remove |= StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic;

            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(target);
            if ((flags & remove) != 0)
                GameObjectUtility.SetStaticEditorFlags(target, flags & ~remove);
        }
    }
}
