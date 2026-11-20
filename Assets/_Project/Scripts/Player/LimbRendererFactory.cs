using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Player
{
    /// <summary>실행 중 생성과 에디터 미리 굽기가 같은 1인칭 팔다리 렌더러를 만들도록 설정을 한곳에 둔다.</summary>
    public static class LimbRendererFactory
    {
        public const string LimbObjectName = "FirstPersonLimbs";

        /// <summary>애니메이션을 그대로 따라가도록 원본과 같은 뼈를 쓰고, 그림자는 원본이 그리므로 끈다.</summary>
        public static SkinnedMeshRenderer Create(SkinnedMeshRenderer source, Mesh limbMesh)
        {
            var limbObject = new GameObject(LimbObjectName) { layer = source.gameObject.layer };
            Transform limbTransform = limbObject.transform;
            limbTransform.SetParent(source.transform.parent, false);
            limbTransform.SetLocalPositionAndRotation(source.transform.localPosition, source.transform.localRotation);
            limbTransform.localScale = source.transform.localScale;

            SkinnedMeshRenderer limbRenderer = limbObject.AddComponent<SkinnedMeshRenderer>();
            limbRenderer.sharedMesh = limbMesh;
            limbRenderer.bones = source.bones;
            limbRenderer.rootBone = source.rootBone;
            limbRenderer.sharedMaterials = source.sharedMaterials;
            limbRenderer.shadowCastingMode = ShadowCastingMode.Off;
            limbRenderer.receiveShadows = source.receiveShadows;
            // 카메라 바로 앞에 있어 경계 계산이 어긋나면 잘려 보일 수 있으므로 화면 밖 판정 없이 매번 갱신한다.
            limbRenderer.updateWhenOffscreen = true;
            return limbRenderer;
        }
    }
}
