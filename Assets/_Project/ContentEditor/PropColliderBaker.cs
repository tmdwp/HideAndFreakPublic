using GuardVsMannequin.Props;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 튕겨나갈 수 있는 변신 사물(<see cref="PropPhysicsRules.IsCandidate"/>)의 충돌체를 움직일 수 있는 모양으로 준비한다.
    /// 움직이는 물체에는 볼록하지 않은 MeshCollider를 쓸 수 없으므로 볼록(convex)으로 바꾼다.
    /// 실행 중에 바꾸면 메시를 다시 계산해야 하고, 읽기가 허용되지 않은 메시(에셋 팩 모델 대부분)는 바꿀 수도 없어 에디터에서 미리 한다.
    /// (맵 최적화, 변신 사물 등록, 모드 SDK의 맵 도구가 함께 쓴다)
    /// </summary>
    public static class PropColliderBaker
    {
        /// <summary>
        /// 씬의 모든 변신 사물을 준비한다. 바꾼 충돌체 수를 반환한다.
        /// </summary>
        public static int BakeScene(Scene scene)
        {
            int changed = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (TransformableProp prop in root.GetComponentsInChildren<TransformableProp>(true))
                    changed += Bake(prop);
            }
            return changed;
        }

        /// <summary>
        /// 사물 하나를 준비한다. 물리 대상이 아니면 아무것도 하지 않는다. 바꾼 충돌체 수를 반환한다.
        /// </summary>
        public static int Bake(TransformableProp prop)
        {
            if (!PropPhysicsRules.IsCandidate(prop))
                return 0;

            int changed = 0;
            foreach (MeshCollider meshCollider in prop.GetComponentsInChildren<MeshCollider>(true))
            {
                if (meshCollider.convex || meshCollider.isTrigger || meshCollider.sharedMesh == null)
                    continue;

                Undo.RecordObject(meshCollider, "변신 사물 충돌체 볼록화");
                meshCollider.convex = true;
                EditorUtility.SetDirty(meshCollider);
                changed++;
            }
            return changed;
        }
    }
}
