using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 외형의 뼈를 따라 움직이는 피격 판정용 캡슐(몸통·머리·팔다리)을 만든다. (마네킹 — 사냥 시간 구울처럼 크기·자세가 바뀌는 외형)
    /// - 판정 캡슐은 트리거이고 "Hitbox" 레이어에 둔다. 이동(ECM2는 Default만 충돌)·다른 광선 검사(트리거 무시)·물리에 영향이 없다.
    /// - 총 판정(<see cref="AimRaycaster.TryRaycastHittable"/>)만 이 캡슐을 맞히고, 판정 캡슐이 켜져 있는 동안 이동 캡슐은 맞히지 않는다.
    /// - 원본 몸이 숨겨지면(변신) 판정 캡슐을 끈다. 그때는 지금처럼 사물 크기로 바뀐 이동 캡슐이 판정을 맡는다.
    /// - 외형이 바뀌면(사냥 외형) 새 뼈대로 다시 만든다.
    /// 모든 피어에서 같은 애니메이션으로 움직이므로 서버(호스트)의 판정도 화면과 맞는다.
    /// </summary>
    [RequireComponent(typeof(CharacterSkinView))]
    public class SkinHitboxes : MonoBehaviour
    {
        /// <summary>판정 캡슐을 두는 레이어 이름. (Project Settings > Tags and Layers)</summary>
        public const string LayerName = "Hitbox";

        // 사람 기준(키 1.8m) 골반~머리 거리. 외형의 실제 거리와의 비로 캡슐 굵기를 맞춘다.
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

        // 이어지는 두 뼈와 반지름. (부모 뼈 → 자식 뼈. 자식 뼈의 시작점은 부모 뼈 기준으로 움직이지 않으므로 캡슐이 정확히 따라간다)
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

        // 자식 뼈가 없는 끝 부위: (뼈, 방향을 정할 부모 뼈, 길이, 반지름) — 부모→뼈 방향으로 길이만큼 늘인다.
        private static readonly (HumanBodyBones bone, HumanBodyBones parent, float length, float radius)[] Tips =
        {
            (HumanBodyBones.Head, HumanBodyBones.Neck, HeadLength, HeadRadius),
            (HumanBodyBones.LeftHand, HumanBodyBones.LeftLowerArm, HandLength, HandRadius),
            (HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm, HandLength, HandRadius),
        };

        private static int hitboxLayer = -2;

        [Tooltip("판정 캡슐이 켜져 있을 때 총이 맞히지 않을 이동 캡슐. 비우면 같은 오브젝트의 CapsuleCollider.")]
        [SerializeField] private Collider movementCapsule;

        private readonly List<CapsuleCollider> hitboxes = new();
        private CharacterSkinView skinView;
        private Animator builtFor;
        private bool hitboxesEnabled;

        /// <summary>판정 캡슐이 있고 켜져 있는지 여부.</summary>
        public bool IsActive => hitboxesEnabled && hitboxes.Count > 0;

        /// <summary>
        /// 총 판정에서 이 충돌체를 판정 캡슐로 볼지 여부.
        /// </summary>
        public static bool IsHitbox(Collider collider)
        {
            return collider != null && collider.isTrigger && collider.gameObject.layer == HitboxLayer && HitboxLayer >= 0;
        }

        /// <summary>
        /// 판정 캡슐이 대신하고 있어 총 판정에서 건너뛸 이동 캡슐인지 여부.
        /// </summary>
        public static bool IsReplacedCapsule(Collider collider)
        {
            return collider != null && collider.TryGetComponent(out SkinHitboxes owner) && owner.IsActive && owner.movementCapsule == collider;
        }

        /// <summary>
        /// "Hitbox" 레이어 번호. 처음 쓸 때 다른 모든 레이어와의 물리 충돌을 끈다. 레이어가 없으면 -1.
        /// </summary>
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
        /// 외형과 이동 캡슐을 캐시한다.
        /// </summary>
        private void Awake()
        {
            skinView = GetComponent<CharacterSkinView>();
            if (movementCapsule == null)
                movementCapsule = GetComponent<CapsuleCollider>();
        }

        /// <summary>
        /// 만든 판정 캡슐을 지운다.
        /// </summary>
        private void OnDestroy()
        {
            Clear();
        }

        /// <summary>
        /// 외형이 바뀌면 다시 만들고, 원본 몸이 보이는지(변신하지 않았는지)에 맞춰 켜고 끈다.
        /// (애니메이션 뒤에 실행되도록 LateUpdate)
        /// </summary>
        private void LateUpdate()
        {
            Animator animator = skinView.Animator;
            if (animator != builtFor)
                Rebuild(animator);

            bool visible = IsBodyVisible();
            if (visible == hitboxesEnabled)
                return;
            hitboxesEnabled = visible;
            foreach (CapsuleCollider hitbox in hitboxes)
                hitbox.enabled = visible;
        }

        /// <summary>
        /// 원본 몸 렌더러가 하나라도 켜져 있으면 true. (변신하면 몸 렌더러가 꺼진다)
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
        /// 지금 외형의 뼈대로 판정 캡슐을 다시 만든다. 굵기는 외형 크기(골반~머리 거리)에 비례한다.
        /// </summary>
        private void Rebuild(Animator animator)
        {
            Clear();
            builtFor = animator;
            if (animator == null || HitboxLayer < 0)
                return;

            Transform hips = SkinBones.Find(animator, HumanBodyBones.Hips);
            Transform head = SkinBones.Find(animator, HumanBodyBones.Head);
            float scale = hips != null && head != null ? Vector3.Distance(hips.position, head.position) / HumanHipsToHead : 1f;

            foreach ((HumanBodyBones from, HumanBodyBones to, float radius) in Segments)
            {
                Transform start = SkinBones.Find(animator, from);
                Transform end = SkinBones.Find(animator, to);
                if (start != null && end != null)
                    CreateCapsule(start, start.position, end.position, radius * scale);
            }
            foreach ((HumanBodyBones bone, HumanBodyBones parent, float length, float radius) in Tips)
            {
                Transform tip = SkinBones.Find(animator, bone);
                Transform before = SkinBones.Find(animator, parent);
                if (tip == null || before == null)
                    continue;
                Vector3 direction = (tip.position - before.position).normalized;
                CreateCapsule(tip, tip.position, tip.position + direction * length * scale, radius * scale);
            }

            hitboxesEnabled = true;
        }

        /// <summary>
        /// 두 점(월드)을 잇는 트리거 캡슐을 뼈의 자식으로 만든다. 뼈의 크기 배율을 나눠 월드 크기를 맞춘다.
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
        /// 만든 판정 캡슐을 지운다.
        /// </summary>
        private void Clear()
        {
            foreach (CapsuleCollider hitbox in hitboxes)
            {
                if (hitbox != null)
                    Destroy(hitbox.gameObject);
            }
            hitboxes.Clear();
            builtFor = null;
            hitboxesEnabled = false;
        }
    }
}
