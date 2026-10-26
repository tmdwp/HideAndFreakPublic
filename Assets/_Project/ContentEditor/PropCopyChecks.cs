using GuardVsMannequin.Core;
using GuardVsMannequin.Props;
using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 변신 사물이 게임에서 그대로 복사될 수 있는지 검사한다. (게임의 맵 검사와 모드 SDK의 맵 검사 <see cref="ModMapValidator"/>가 함께 쓴다)
    /// 오류: 조준하거나 변신할 수 없음 / 경고: 변신은 되지만 원래 모습과 달라 보이거나 변신이 막힘.
    /// </summary>
    public static class PropCopyChecks
    {
        /// <summary>
        /// 가장 중요한 문제 하나를 찾는다. 문제가 없으면 false.
        /// </summary>
        public static bool TryFindProblem(TransformableProp prop, out string message, out bool isError)
        {
            isError = true;
            if (prop.GetComponentInChildren<Collider>(true) == null)
            {
                message = "충돌체가 없어 조준할 수 없습니다. '충돌체 추가'를 실행하세요.";
                return true;
            }

            PropAppearance appearance = PropAppearance.Capture(prop);
            if (appearance.Parts.Count == 0)
            {
                message = "복사할 메시가 없습니다.";
                return true;
            }

            isError = false;
            if (!prop.IsWall && appearance.LargestSize > GameTuning.DisguiseMaxSize)
            {
                message = $"너무 커서({appearance.LargestSize:F1}m > {GameTuning.DisguiseMaxSize}m) 변신할 수 없습니다. 벽으로 지정하거나 등록을 해제하세요.";
                return true;
            }
            if (HasUncopiedParts(prop))
            {
                message = "파티클·조명·스킨 메시는 복사되지 않아 변신 모습이 원래와 달라 보입니다. (변신 대상에서 빼는 것을 권장)";
                return true;
            }
            if (HasBatchedPart(prop))
            {
                message = "정적 배칭이 켜진 조각이 있어 게임에서 모습이 깨집니다. 맵 최적화를 다시 실행하거나 다시 등록하세요.";
                return true;
            }
            if (PropPhysicsRules.IsCandidate(prop) && !HasSimulatableColliders(prop))
            {
                message = "볼록하지 않은 충돌체가 있어 총에 맞아도 튕겨나가지 않습니다. 맵 최적화를 다시 실행하거나 다시 등록하세요.";
                return true;
            }

            message = null;
            return false;
        }

        /// <summary>
        /// 이 사물에 속한 자식 중 메시가 아닌 표시 요소(파티클·조명·스킨 메시)가 있는지 확인한다.
        /// </summary>
        private static bool HasUncopiedParts(TransformableProp prop)
        {
            foreach (Component part in prop.GetComponentsInChildren<Component>(true))
            {
                bool uncopied = part is ParticleSystem || part is Light || part is SkinnedMeshRenderer;
                if (uncopied && part.GetComponentInParent<TransformableProp>(true) == prop)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 이 사물의 충돌체(트리거 제외)가 모두 움직이는 물체에 쓸 수 있는 모양인지 확인한다.
        /// </summary>
        private static bool HasSimulatableColliders(TransformableProp prop)
        {
            foreach (Collider collider in prop.GetComponentsInChildren<Collider>(true))
            {
                if (!collider.isTrigger && !PropPhysicsRules.IsSimulatable(collider))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 이 사물의 메시 조각 중 정적 배칭 플래그가 켜진 것이 있는지 확인한다.
        /// </summary>
        private static bool HasBatchedPart(TransformableProp prop)
        {
            foreach (MeshRenderer part in prop.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (part.GetComponentInParent<TransformableProp>(true) != prop)
                    continue;
                if ((GameObjectUtility.GetStaticEditorFlags(part.gameObject) & StaticEditorFlags.BatchingStatic) != 0)
                    return true;
            }
            return false;
        }
    }
}
