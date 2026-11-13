using UnityEngine;

namespace GuardVsMannequin.Player.Emotes
{
    /// <summary>외형 애니메이터의 감정 표현 트리거로 감정 표현을 재생한다.</summary>
    [RequireComponent(typeof(CharacterSkinView))]
    public class AnimatorEmoteView : MonoBehaviour, IEmoteView
    {
        private CharacterSkinView skinView;

        /// <summary>
        /// 재생마다 GetComponent를 부르지 않도록 미리 찾아 둔다.
        /// </summary>
        private void Awake()
        {
            skinView = GetComponent<CharacterSkinView>();
        }

        /// <summary>
        /// 외형마다 컨트롤러 구성이 달라서 번호별 트리거 방식과 번호+공통 트리거 방식을 차례로 시도한다.
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
