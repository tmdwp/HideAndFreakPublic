using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>모든 클라이언트에서 상호작용 동작이 보이도록 동기화된 상태를 애니메이터에 옮긴다.</summary>
    [RequireComponent(typeof(CharacterSkinView))]
    public class InteractAnimationView : MonoBehaviour
    {
        private CharacterSkinView skinView;
        private IInteractionState interactionState;
        private bool lastInteracting;

        /// <summary>
        /// 매 프레임 조회하지 않도록 외형과 상호작용 상태를 미리 찾아 둔다.
        /// </summary>
        private void Awake()
        {
            skinView = GetComponent<CharacterSkinView>();
            interactionState = GetComponent<IInteractionState>();
        }

        /// <summary>
        /// 같은 값을 매 프레임 넣을 필요가 없으므로 상태가 바뀐 프레임에만 애니메이터 값을 바꾼다.
        /// </summary>
        private void Update()
        {
            if (interactionState == null)
                return;

            bool isInteracting = interactionState.IsInteracting;
            if (isInteracting == lastInteracting)
                return;

            lastInteracting = isInteracting;
            skinView.TrySetBool(CharacterAnimatorParameters.Interact, isInteracting);
        }
    }
}
