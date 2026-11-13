using GuardVsMannequin.Props;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>움직이는 물체에는 볼록하지 않은 MeshCollider를 쓸 수 없어 튕겨나갈 사물의 충돌체를 에디터에서 미리 볼록으로 바꾼다.</summary>
    public static class PropColliderBaker
    {
        /// <summary>맵 도구가 씬 단위로 한 번에 처리하도록 바꾼 충돌체 수를 돌려준다.</summary>
        public static int BakeScene(Scene scene)
        {
            int changedCount = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (TransformableProp prop in root.GetComponentsInChildren<TransformableProp>(true))
                    changedCount += Bake(prop);
            }
            return changedCount;
        }

        /// <summary>움직이지 않는 사물까지 볼록으로 바꾸면 충돌 모양이 뭉개지므로 물리 대상만 바꾼다.</summary>
        public static int Bake(TransformableProp prop)
        {
            if (!PropPhysicsRules.IsCandidate(prop))
                return 0;

            int changedCount = 0;
            foreach (MeshCollider meshCollider in prop.GetComponentsInChildren<MeshCollider>(true))
            {
                if (meshCollider.convex || meshCollider.isTrigger || meshCollider.sharedMesh == null)
                    continue;

                Undo.RecordObject(meshCollider, "변신 사물 충돌체 볼록화");
                meshCollider.convex = true;
                EditorUtility.SetDirty(meshCollider);
                changedCount++;
            }
            return changedCount;
        }
    }
}
