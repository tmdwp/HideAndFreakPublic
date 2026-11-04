using UnityEngine;

namespace GuardVsMannequin.Player.Emotes
{
    /// <summary>
    /// 외형 애니메이터로 감정 표현을 재생한다. (파라미터 규칙: <see cref="CharacterAnimatorParameters"/>)
    /// 1. 번호별 트리거(Emote1 ~ Emote6)가 있으면 그것을 발동한다.
    /// 2. 없으면 Emote 트리거 + EmoteId 정수가 있을 때 번호를 넣고 발동한다.
    /// 3. 둘 다 없으면 false를 반환한다. (감정 표현 애니메이션이 없는 외형은 아무 동작도 하지 않음)
    /// </summary>
    [RequireComponent(typeof(CharacterSkinView))]
    public class AnimatorEmoteView : MonoBehaviour, IEmoteView
    {
        private CharacterSkinView skinView;

        /// <summary>
        /// 외형 컴포넌트를 캐시한다.
        /// </summary>
        private void Awake()
        {
            skinView = GetComponent<CharacterSkinView>();
        }

        /// <summary>
        /// 애니메이터에 감정 표현이 있으면 재생한다.
        /// </summary>
        public bool TryPlay(int emoteId)
        {
            if (skinView.TryPlayTrigger(CharacterAnimatorParameters.EmoteTrigger(emoteId)))
                return true;

            if (skinView.HasParameter(CharacterAnimatorParameters.Emote) && skinView.TrySetInteger(CharacterAnimatorParameters.EmoteId, emoteId))
                return skinView.TryPlayTrigger(CharacterAnimatorParameters.Emote);

            return false;
        }
    }
}
