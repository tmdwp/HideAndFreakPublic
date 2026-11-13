using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>맞은 것이 보이도록 체력이 줄면 외형의 Hit 트리거를 발동한다.</summary>
    [RequireComponent(typeof(PlayerHealth), typeof(CharacterSkinView))]
    public class HitReactionAnimator : MonoBehaviour
    {
        private PlayerHealth health;
        private CharacterSkinView skinView;

        /// <summary>
        /// 체력 쪽이 애니메이션을 몰라도 되도록 피격 이벤트를 여기서 듣는다.
        /// </summary>
        private void Awake()
        {
            health = GetComponent<PlayerHealth>();
            skinView = GetComponent<CharacterSkinView>();
            health.Damaged += OnDamaged;
        }

        /// <summary>
        /// 파괴된 뒤 이벤트가 이 객체를 부르지 않도록 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            if (health != null)
                health.Damaged -= OnDamaged;
        }

        /// <summary>
        /// 외형마다 Hit 트리거가 없을 수 있어서 있는 경우에만 재생한다.
        /// </summary>
        private void OnDamaged()
        {
            skinView.TryPlayTrigger(CharacterAnimatorParameters.Hit);
        }
    }
}
