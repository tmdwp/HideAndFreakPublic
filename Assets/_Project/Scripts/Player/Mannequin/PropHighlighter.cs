using FishNet.Object;
using GuardVsMannequin.Props;
using GuardVsMannequin.Rendering;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>마네킹 본인 화면에서 조준 중인 변신 가능 사물에 외곽선을 표시한다.</summary>
    [RequireComponent(typeof(MannequinTransformer))]
    public class PropHighlighter : NetworkBehaviour
    {
        private MannequinTransformer transformer;
        private IOutlineService outlines;
        private TransformableProp currentTarget;

        /// <summary>
        /// 매 프레임 조회하지 않도록 변신 컴포넌트와 외곽선 서비스를 미리 찾아 둔다.
        /// </summary>
        private void Awake()
        {
            transformer = GetComponent<MannequinTransformer>();
            outlines = OutlineRegistry.Instance;
        }

        /// <summary>
        /// 아바타가 사라진 뒤에도 사물에 외곽선이 남지 않도록 지운다.
        /// </summary>
        private void OnDestroy()
        {
            outlines?.Hide(this);
        }

        /// <summary>노리는 사물이 남에게 보이지 않도록 소유자 화면에서만 찾고 대상이 바뀔 때만 다시 건다.</summary>
        private void Update()
        {
            if (!IsOwner)
                return;

            TransformableProp target = null;
            if (!transformer.IsDisguised && transformer.TryGetAimedProp(out TransformableProp aimedProp))
                target = aimedProp;

            if (target != currentTarget)
                ShowOn(target);
        }

        /// <summary>외곽선은 하나만 있어야 하므로 이전 대상에서 새 대상으로 옮긴다.</summary>
        private void ShowOn(TransformableProp target)
        {
            currentTarget = target;
            if (target == null)
            {
                outlines.Hide(this);
                return;
            }
            outlines.Show(this, target.GetComponentsInChildren<Renderer>(), OutlineKind.Aim);
        }
    }
}
