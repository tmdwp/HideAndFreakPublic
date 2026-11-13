using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>크기·자세가 바뀌는 외형도 정확히 맞도록 뼈를 따라 움직이는 피격 판정 캡슐을 만든다.</summary>
    [RequireComponent(typeof(CharacterSkinView))]
    public class SkinHitboxes : MonoBehaviour, IHitShapeOverride
    {
        /// <summary>Project Settings > Tags and Layers에 같은 이름의 레이어가 있어야 한다.</summary>
        public const string LayerName = "Hitbox";

        // 외형 비율로 캡슐 굵기를 맞추는 기준 골반~머리 거리.
        private const float HumanHipsToHead = 0.63f;

        // 사람 기준 반지름(m)
        private const float TorsoRadius = 0.16f;
        private const float HeadRadius = 0.12f;
        private const float HeadLength = 0.2f;
        private const float UpperArmRadius = 0.075f;
        private const float LowerArmRadius = 0.065f;
        private const float HandRadius = 0.06f;
        private const float HandLength = 0.2f;
        private const float UpperLegRadius = 0.09f;
        private const float LowerLegRadius = 0.075f;

        // 캡슐이 정확히 따라가도록 잇는 부모·자식 뼈와 반지름.
        private static readonly (HumanBodyBones from, HumanBodyBones to, float radius)[] Segments =
        {
            (HumanBodyBones.Hips, HumanBodyBones.Spine, TorsoRadius),
            (HumanBodyBones.Spine, HumanBodyBones.Chest, TorsoRadius),
            (HumanBodyBones.Chest, HumanBodyBones.UpperChest, TorsoRadius),
            (HumanBodyBones.UpperChest, HumanBodyBones.Neck, TorsoRadius),
            (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, UpperArmRadius),
            (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, LowerArmRadius),
            (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, UpperArmRadius),
            (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, LowerArmRadius),
            (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, UpperLegRadius),
            (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, LowerLegRadius),
            (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, UpperLegRadius),
            (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, LowerLegRadius),
        };

        // 자식 뼈가 없는 끝 부위의 뼈·방향·길이·반지름.
        private static readonly (HumanBodyBones bone, HumanBodyBones parent, float length, float radius)[] Tips =
        {
            (HumanBodyBones.Head, HumanBodyBones.Neck, HeadLength, HeadRadius),
            (HumanBodyBones.LeftHand, HumanBodyBones.LeftLowerArm, HandLength, HandRadius),
            (HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm, HandLength, HandRadius),
        };

        // -2: 아직 찾지 않음, -1: 레이어 없음
        private static int hitboxLayer = -2;

        [Tooltip("판정 캡슐이 켜져 있을 때 총이 맞히지 않을 이동 캡슐. 비우면 같은 오브젝트의 CapsuleCollider.")]
        [SerializeField] private Collider movementCapsule;

        private readonly List<CapsuleCollider> hitboxes = new();
        private CharacterSkinView skinView;
        private Animator builtForAnimator;
        private bool hitboxesEnabled;

        /// <summary>판정 캡슐이 없으면 이동 캡슐을 건너뛰면 안 되므로, 실제로 있고 켜졌을 때만 참이다.</summary>
        public bool IsActive => hitboxesEnabled && hitboxes.Count > 0;

        /// <summary>
        /// 다른 트리거까지 맞으면 안 되므로, 이 레이어의 트리거만 판정 캡슐로 본다.
        /// </summary>
        public static bool IsHitbox(Collider collider)
        {
            return collider != null && collider.isTrigger && collider.gameObject.layer == HitboxLayer && HitboxLayer >= 0;
        }

        /// <summary>이동 캡슐은 몸보다 커서 빈 곳을 쏴도 맞으므로 판정 캡슐이 켜져 있으면 건너뛴다.</summary>
        public bool ReplacesCollider(Collider collider)
        {
            return IsActive && movementCapsule == collider;
        }

        /// <summary>판정 캡슐이 이동·물리에 영향을 주지 않도록 처음 쓸 때 모든 레이어와의 충돌을 끈다.</summary>
        private static int HitboxLayer
        {
            get
            {
                if (hitboxLayer != -2)
                    return hitboxLayer;
                hitboxLayer = LayerMask.NameToLayer(LayerName);
                if (hitboxLayer < 0)
                {
                    Debug.LogWarning($"[SkinHitboxes] '{LayerName}' 레이어가 없어 판정 캡슐을 만들지 않습니다. (Project Settings > Tags and Layers)");
                    return hitboxLayer;
                }
                for (int layer = 0; layer < 32; layer++)
                    Physics.IgnoreLayerCollision(hitboxLayer, layer, true);
                return hitboxLayer;
            }
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
        /// 외형마다 키가 달라서 굵기를 사람 기준 골반~머리 거리와의 비로 맞춰 다시 만든다.
        /// </summary>
        private void Rebuild(Animator animator)
        {
            Clear();
            builtForAnimator = animator;
            if (animator == null || HitboxLayer < 0)
                return;

            Transform hips = SkinBones.Find(animator, HumanBodyBones.Hips);
            Transform head = SkinBones.Find(animator, HumanBodyBones.Head);
            float sizeScale = hips != null && head != null ? Vector3.Distance(hips.position, head.position) / HumanHipsToHead : 1f;

            foreach ((HumanBodyBones from, HumanBodyBones to, float radius) in Segments)
            {
                Transform startBone = SkinBones.Find(animator, from);
                Transform endBone = SkinBones.Find(animator, to);
                if (startBone != null && endBone != null)
                    CreateCapsule(startBone, startBone.position, endBone.position, radius * sizeScale);
            }
            foreach ((HumanBodyBones bone, HumanBodyBones parent, float length, float radius) in Tips)
            {
                Transform tipBone = SkinBones.Find(animator, bone);
                Transform parentBone = SkinBones.Find(animator, parent);
                if (tipBone == null || parentBone == null)
                    continue;
                Vector3 direction = (tipBone.position - parentBone.position).normalized;
                CreateCapsule(tipBone, tipBone.position, tipBone.position + direction * length * sizeScale, radius * sizeScale);
            }

            hitboxesEnabled = true;
        }

        /// <summary>
        /// 뼈를 따라 움직이도록 뼈의 자식으로 만들고, 뼈의 크기 배율이 캡슐 크기를 바꾸지 않도록 나눠서 월드 크기를 맞춘다.
        /// </summary>
        private void CreateCapsule(Transform bone, Vector3 from, Vector3 to, float radius)
        {
            Vector3 axis = to - from;
            float length = axis.magnitude;
            if (length < 0.01f)
                return;

            var hitboxObject = new GameObject("Hitbox_" + bone.name) { layer = HitboxLayer };
            Transform hitboxTransform = hitboxObject.transform;
            hitboxTransform.SetParent(bone, false);
            hitboxTransform.SetPositionAndRotation((from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, axis / length));

            float boneScale = Mathf.Max(1e-4f, hitboxTransform.lossyScale.x);
            CapsuleCollider capsule = hitboxObject.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.direction = 1;
            capsule.radius = radius / boneScale;
            capsule.height = (length + radius * 2f) / boneScale;
            hitboxes.Add(capsule);
        }

        /// <summary>
        /// 다시 만들 때 이전 캡슐이 남아 두 번 맞지 않도록 모두 지운다.
        /// </summary>
        private void Clear()
        {
            foreach (CapsuleCollider hitbox in hitboxes)
            {
                if (hitbox != null)
                    Destroy(hitbox.gameObject);
            }
            hitboxes.Clear();
            builtForAnimator = null;
            hitboxesEnabled = false;
        }
    }
}
