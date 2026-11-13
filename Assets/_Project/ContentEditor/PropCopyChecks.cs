using GuardVsMannequin.Core;
using GuardVsMannequin.Props;
using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>게임과 모드 SDK가 같은 기준으로 변신 사물이 그대로 복사될 수 있는지 검사한다.</summary>
    public static class PropCopyChecks
    {
        /// <summary>사물이 많은 맵에서 Console이 넘치지 않도록 가장 중요한 문제 하나만 알린다.</summary>
        public static bool TryFindProblem(TransformableProp prop, out string message, out bool isError)
        {
            isError = false;
            if (prop.IsWall)
            {
                message = "예전에 벽으로 등록된 사물입니다. 벽은 변신 대상이 아니라 게임에서 무시됩니다. 등록을 해제하세요.";
                return true;
            }

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
            if (appearance.LargestSize > GameTuning.DisguiseMaxSize)
            {
                message = $"너무 커서({appearance.LargestSize:F1}m > {GameTuning.DisguiseMaxSize}m) 변신할 수 없습니다. 등록을 해제하세요.";
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

        /// <summary>외형 복사는 MeshRenderer만 다루므로 다른 표시 요소가 있으면 변신 모습이 달라짐을 알린다.</summary>
        private static bool HasUncopiedParts(TransformableProp prop)
        {
            foreach (Component part in prop.GetComponentsInChildren<Component>(true))
            {
                bool isUncopied = part is ParticleSystem || part is Light || part is SkinnedMeshRenderer;
                if (isUncopied && part.GetComponentInParent<TransformableProp>(true) == prop)
                    return true;
            }
            return false;
        }

        /// <summary>하나라도 볼록하지 않으면 튕겨나가지 않으므로 트리거를 뺀 모든 충돌체를 확인한다.</summary>
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
        /// 실행 중에는 이미 배칭된 결과만 보여 원인을 알 수 없으므로, 에디터에서 정적 배칭 플래그를 미리 찾는다.
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
