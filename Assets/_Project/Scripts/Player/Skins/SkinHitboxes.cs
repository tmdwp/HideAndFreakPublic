using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>크기·자세가 바뀌는 외형도 정확히 맞도록 뼈를 따라 움직이는 피격 판정 캡슐을 만든다.</summary>
    [RequireComponent(typeof(CharacterSkinView))]
    public class SkinHitboxes : MonoBehaviour, IHitShapeOverride
    {
        [Tooltip("판정 캡슐이 켜져 있을 때 총이 맞히지 않을 이동 캡슐. 비우면 같은 오브젝트의 CapsuleCollider.")]
        [SerializeField] private Collider movementCapsule;

        private readonly List<CapsuleCollider> hitboxes = new();
        private CharacterSkinView skinView;
        private Animator builtForAnimator;
        private bool hitboxesEnabled;
        // 미리 구운 캡슐은 모델 프리팹의 일부라 여기서 지우면 안 된다.
        private bool ownsHitboxes;

        /// <summary>판정 캡슐이 없으면 이동 캡슐을 건너뛰면 안 되므로, 실제로 있고 켜졌을 때만 참이다.</summary>
        public bool IsActive => hitboxesEnabled && hitboxes.Count > 0;

        /// <summary>
        /// 다른 트리거까지 맞으면 안 되므로, 이 레이어의 트리거만 판정 캡슐로 본다.
        /// </summary>
        public static bool IsHitbox(Collider collider)
        {
            int hitboxLayer = SkinHitboxFactory.HitboxLayer;
            return collider != null && collider.isTrigger && hitboxLayer >= 0 && collider.gameObject.layer == hitboxLayer;
        }

        /// <summary>이동 캡슐은 몸보다 커서 빈 곳을 쏴도 맞으므로 판정 캡슐이 켜져 있으면 건너뛴다.</summary>
        public bool ReplacesCollider(Collider collider)
        {
            return IsActive && movementCapsule == collider;
        }

        /// <summary>
        /// 이동 캡슐을 지정하지 않은 프리팹도 동작하도록 같은 오브젝트의 캡슐을 대신 쓴다.
        /// </summary>
        private void Awake()
        {
            skinView = GetComponent<CharacterSkinView>();
            if (movementCapsule == null)
                movementCapsule = GetComponent<CapsuleCollider>();
        }

        /// <summary>
        /// 캡슐은 뼈의 자식이라 외형 모델과 함께 지워지지만, 외형이 남는 경우에 대비해 직접 지운다.
        /// </summary>
        private void OnDestroy()
        {
            Clear();
        }

        /// <summary>애니메이션 뒤에 확인하도록 LateUpdate에서 외형 교체와 변신을 따라간다.</summary>
        private void LateUpdate()
        {
            Animator animator = skinView.Animator;
            if (animator != builtForAnimator)
                Rebuild(animator);

            bool isVisible = IsBodyVisible();
            if (isVisible == hitboxesEnabled)
                return;
            hitboxesEnabled = isVisible;
            foreach (CapsuleCollider hitbox in hitboxes)
                hitbox.enabled = isVisible;
        }

        /// <summary>
        /// 변신하면 몸 렌더러가 꺼지므로, 변신 여부를 따로 묻지 않고 렌더러가 하나라도 켜져 있는지로 판단한다.
        /// </summary>
        private bool IsBodyVisible()
        {
            foreach (Renderer body in skinView.BodyRenderers)
            {
                if (body != null && body.enabled && body.gameObject.activeInHierarchy)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 외형 프리팹에 미리 구운 캡슐이 있으면 그대로 쓰고, 없는 외형(모드 외형 등)만 실행 중에 만든다.
        /// </summary>
        private void Rebuild(Animator animator)
        {
            Clear();
            builtForAnimator = animator;
            int hitboxLayer = SkinHitboxFactory.HitboxLayer;
            if (animator == null || hitboxLayer < 0)
                return;

            BakedSkinParts bakedParts = skinView.BakedParts;
            if (bakedParts != null && bakedParts.Hitboxes.Count > 0)
            {
                foreach (CapsuleCollider bakedHitbox in bakedParts.Hitboxes)
                {
                    if (bakedHitbox != null)
                        hitboxes.Add(bakedHitbox);
                }
                ownsHitboxes = false;
            }
            else
            {
                hitboxes.AddRange(SkinHitboxFactory.Build(bone => SkinBones.Find(animator, bone), hitboxLayer));
                ownsHitboxes = true;
            }

            foreach (CapsuleCollider hitbox in hitboxes)
                hitbox.enabled = true;
            hitboxesEnabled = true;
        }

        /// <summary>
        /// 다시 만들 때 이전 캡슐이 남아 두 번 맞지 않도록 직접 만든 것만 지운다.
        /// </summary>
        private void Clear()
        {
            if (ownsHitboxes)
            {
                foreach (CapsuleCollider hitbox in hitboxes)
                {
                    if (hitbox != null)
                        Destroy(hitbox.gameObject);
                }
            }
            hitboxes.Clear();
            builtForAnimator = null;
            hitboxesEnabled = false;
            ownsHitboxes = false;
        }
    }
}
