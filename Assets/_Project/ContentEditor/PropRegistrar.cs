using GuardVsMannequin.Props;
using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>맵 오브젝트를 변신 사물로 등록하고 조준·외형 복사·물리에 필요한 충돌체와 플래그를 맞춘다.</summary>
    public static class PropRegistrar
    {
        /// <summary>메시가 없으면 변신할 모습이 없어 false를 돌려주고, 모든 변경은 Undo에 기록한다.</summary>
        /// <param name="target">등록할 오브젝트(여러 조각 물체는 부모).</param>
        /// <param name="absorbChildren">true면 자식에 따로 붙은 변신 사물 표시를 지워 하나의 물체로 합친다.</param>
        public static bool Register(GameObject target, bool absorbChildren)
        {
            MeshFilter[] meshFilters = target.GetComponentsInChildren<MeshFilter>(true);
            if (!HasAnyMesh(meshFilters))
                return false;

            if (absorbChildren)
            {
                foreach (TransformableProp childProp in target.GetComponentsInChildren<TransformableProp>(true))
                {
                    if (childProp.gameObject != target)
                        Undo.DestroyObjectImmediate(childProp);
                }
            }

            TransformableProp prop = target.GetComponent<TransformableProp>();
            if (prop == null)
                prop = Undo.AddComponent<TransformableProp>(target);
            Undo.RecordObject(prop, "변신 사물 등록");
            prop.SetWall(false);
            EditorUtility.SetDirty(prop);

            // 직접 고른 물체(부모 단위)는 모든 조각을 조준할 수 있게 하고, 자동 등록은 원래 충돌체만 쓴다.
            PrepareParts(prop, addChildColliders: absorbChildren);
            return true;
        }

        /// <summary>정적 배칭된 조각은 복사할 수 없고 충돌체 없는 조각은 조준할 수 없으므로 조각마다 고친다.</summary>
        /// <param name="addChildColliders">false면 자식 장식이 갑자기 막히지 않도록 사물 자신에만 충돌체를 붙인다.</param>
        public static void PrepareParts(TransformableProp prop, bool addChildColliders)
        {
            ClearStaticFlags(prop.gameObject, prop.IsWall);
            foreach (MeshFilter meshFilter in prop.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh == null || meshFilter.GetComponentInParent<TransformableProp>() != prop)
                    continue;
                bool isPropRoot = meshFilter.gameObject == prop.gameObject;
                if ((isPropRoot || addChildColliders) && meshFilter.GetComponent<Collider>() == null)
                    Undo.AddComponent<MeshCollider>(meshFilter.gameObject).sharedMesh = meshFilter.sharedMesh;
                ClearStaticFlags(meshFilter.gameObject, prop.IsWall);
            }

            // 총에 맞으면 튕겨나갈 수 있는 크기면 충돌체를 움직일 수 있는 모양(볼록)으로 바꾼다.
            PropColliderBaker.Bake(prop);
        }

        /// <summary>
        /// MeshFilter는 있어도 메시가 비어 있을 수 있어 실제 메시가 하나라도 있는지 본다.
        /// </summary>
        private static bool HasAnyMesh(MeshFilter[] meshFilters)
        {
            foreach (MeshFilter meshFilter in meshFilters)
            {
                if (meshFilter.sharedMesh != null)
                    return true;
            }
            return false;
        }

        /// <summary>합쳐진 메시는 복사할 수 없어 정적 배칭을 끄고, 옮겨진 뒤 엉뚱하게 가려지지 않도록 오클루전 플래그도 끈다.</summary>
        private static void ClearStaticFlags(GameObject target, bool isWall)
        {
            StaticEditorFlags flagsToRemove = StaticEditorFlags.BatchingStatic;
            if (!isWall)
                flagsToRemove |= StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic;

            StaticEditorFlags currentFlags = GameObjectUtility.GetStaticEditorFlags(target);
            if ((currentFlags & flagsToRemove) != 0)
                GameObjectUtility.SetStaticEditorFlags(target, currentFlags & ~flagsToRemove);
        }
    }
}
