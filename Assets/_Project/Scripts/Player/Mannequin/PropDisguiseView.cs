using System.Collections.Generic;
using GuardVsMannequin.Props;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>변신 상태에 맞춰 맵 사물에서 복사한 외형과 그 모양대로의 충돌체로 바꾼다.</summary>
    [RequireComponent(typeof(MannequinTransformer))]
    public class PropDisguiseView : MonoBehaviour, IPerspectiveListener, IHitShapeOverride
    {
        // 너무 작은 사물은 캡슐이 바닥을 뚫으므로 반지름 최소값을 둔다.
        private const float MinRadius = 0.15f;
        private const float MaxRadius = 1.0f;

        [Tooltip("변신 중 숨길 마네킹 본체 렌더러.")]
        [SerializeField] private Renderer[] bodyRenderers;

        private readonly List<MeshRenderer> partRenderers = new();
        private readonly List<MeshFilter> partFilters = new();

        private MannequinTransformer transformer;
        private ICharacterMotor motor;
        private Collider movementCapsule;
        private Transform disguiseRoot;
        private DisguiseShape shape;
        private Vector3 defaultLocalPosition;
        private float defaultRadius;
        private float defaultHeight;
        private bool disguiseShadowsOnly;

        /// <summary>
        /// 변신을 풀 때 원래 크기로 돌아가야 하므로, 변신 전에 기본 충돌체 크기를 저장해 두고 변신 이벤트를 듣는다.
        /// </summary>
        private void Awake()
        {
            transformer = GetComponent<MannequinTransformer>();
            motor = GetComponent<ICharacterMotor>();
            movementCapsule = GetComponent<CapsuleCollider>();
            if (motor != null)
            {
                defaultRadius = motor.CapsuleRadius;
                defaultHeight = motor.CapsuleHeight;
            }
            transformer.DisguiseChanged += ApplyDisguise;
        }

        /// <summary>
        /// 파괴된 뒤 변신 이벤트가 이 객체를 부르지 않도록 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            transformer.DisguiseChanged -= ApplyDisguise;
        }

        /// <summary>서버 히트스캔이 화면과 맞도록 모든 피어에서 외형과 충돌체를 함께 바꾼다.</summary>
        private void ApplyDisguise(int propIndex)
        {
            if (propIndex == TransformableProp.None)
            {
                RestoreBody();
                return;
            }
            if (!PropRegistry.TryGet(propIndex, out TransformableProp prop))
            {
                Debug.LogWarning($"[PropDisguiseView] 사물 번호 {propIndex}를 이 맵에서 찾을 수 없습니다. (맵 빌드가 다를 수 있음)", this);
                RestoreBody();
                return;
            }

            PropAppearance appearance = prop.Appearance;
            EnsureDisguiseRoot();
            ApplyParts(appearance.Parts);

            // 떠 있거나 박히지 않도록 사물 바닥을 캐릭터 발 위치에 맞춘다.
            Bounds bounds = appearance.Bounds;
            DisguiseBounds = bounds;
            defaultLocalPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            ResetPose();
            disguiseRoot.gameObject.SetActive(true);
            shape.Build(appearance.Parts, bounds);

            SetBodyVisible(false);

            // 캡슐이 사물 밖으로 튀어나와 빈 곳에 막히지 않도록 사물 안에 들어가는 크기로 줄이고, 나머지 모양은 상자가 맡는다.
            float radius = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z, bounds.size.y) * 0.5f, MinRadius, MaxRadius);
            motor?.SetCapsule(radius, Mathf.Max(bounds.size.y, radius * 2f));
            motor?.SetShapeBoxes(shape.ActiveBoxes);
        }

        /// <summary>사물 모양 상자가 있으면 그보다 크거나 작은 이동 캡슐로는 맞히지 않는다.</summary>
        public bool ReplacesCollider(Collider collider)
        {
            return collider == movementCapsule && IsShowing && shape.ActiveBoxes.Count > 0;
        }

        public bool IsShowing => disguiseRoot != null && disguiseRoot.gameObject.activeSelf;

        /// <summary>외형 기준 공간의 값이라 <see cref="WorldPose"/>로 월드에 놓인다.</summary>
        public Bounds DisguiseBounds { get; private set; }

        /// <summary>외형이 없을 때도 호출하는 쪽이 분기하지 않도록 아바타 자세를 대신 준다.</summary>
        public Pose WorldPose => disguiseRoot != null
            ? new Pose(disguiseRoot.position, disguiseRoot.rotation)
            : new Pose(transform.position, transform.rotation);

        /// <summary>
        /// 날아가는 동안에는 서버 물리 자세를 그대로 따라가야 해서 월드 자세로 놓는다.
        /// </summary>
        public void SetWorldPose(Pose pose)
        {
            if (disguiseRoot != null)
                disguiseRoot.SetPositionAndRotation(pose.position, pose.rotation);
        }

        /// <summary>
        /// 멈춘 뒤 아바타가 움직여도 넘어진 모습이 함께 따라가도록 아바타 기준 자세로 놓는다.
        /// </summary>
        public void SetLocalPose(Pose pose)
        {
            if (disguiseRoot != null)
                disguiseRoot.SetLocalPositionAndRotation(pose.position, pose.rotation);
        }

        /// <summary>
        /// 넘어졌던 사물이 다음 변신·착지 후에도 기울어진 채 남지 않도록 똑바로 선 기본 자세로 되돌린다.
        /// </summary>
        public void ResetPose()
        {
            if (disguiseRoot != null)
                disguiseRoot.SetLocalPositionAndRotation(defaultLocalPosition, Quaternion.identity);
        }

        /// <summary>
        /// 변신할 때마다 오브젝트를 만들고 지우지 않도록 조각 렌더러를 다시 쓰고, 모자라면 만들고 남으면 끈다.
        /// </summary>
        private void ApplyParts(IReadOnlyList<PropAppearance.Part> parts)
        {
            while (partRenderers.Count < parts.Count)
                CreatePart();

            for (int i = 0; i < partRenderers.Count; i++)
            {
                bool isUsed = i < parts.Count;
                partRenderers[i].gameObject.SetActive(isUsed);
                if (!isUsed)
                    continue;

                PropAppearance.Part part = parts[i];
                partFilters[i].sharedMesh = part.Mesh;
                partRenderers[i].sharedMaterials = part.Materials;
                Transform partTransform = partRenderers[i].transform;
                partTransform.SetLocalPositionAndRotation(part.Position, part.Rotation);
                partTransform.localScale = part.Scale;
            }
        }

        /// <summary>
        /// 변신을 풀면 사물 크기 캡슐로 남아 끼이지 않도록 본체와 기본 충돌체로 함께 되돌린다.
        /// </summary>
        private void RestoreBody()
        {
            if (disguiseRoot != null)
                disguiseRoot.gameObject.SetActive(false);
            shape?.Clear();

            SetBodyVisible(true);
            motor?.SetCapsule(defaultRadius, defaultHeight);
            motor?.SetShapeBoxes(System.Array.Empty<BoxCollider>());
        }

        /// <summary>
        /// 변신하지 않는 마네킹에게는 필요 없으므로 처음 변신할 때 한 번만 만든다.
        /// </summary>
        private void EnsureDisguiseRoot()
        {
            if (disguiseRoot != null)
                return;

            disguiseRoot = new GameObject("Disguise") { layer = gameObject.layer }.transform;
            disguiseRoot.SetParent(transform, false);
            shape = new DisguiseShape(disguiseRoot);
        }

        /// <summary>
        /// 새로 만든 조각도 지금 그림자 설정을 따르도록 만들 때 그림자 모드를 맞춘다.
        /// </summary>
        private void CreatePart()
        {
            var partObject = new GameObject($"Part_{partRenderers.Count}");
            partObject.transform.SetParent(disguiseRoot, false);
            partFilters.Add(partObject.AddComponent<MeshFilter>());
            MeshRenderer partRenderer = partObject.AddComponent<MeshRenderer>();
            partRenderer.shadowCastingMode = ShadowMode;
            partRenderers.Add(partRenderer);
        }

        /// <summary>
        /// 테스트 방 시점 전환에서 1인칭일 때 변신한 사물이 시야를 가리지 않도록 그림자만 남긴다.
        /// </summary>
        public void OnPerspectiveChanged(bool firstPerson)
        {
            SetDisguiseShadowsOnly(firstPerson);
        }

        /// <summary>
        /// 사냥 시간 1인칭·관전에서 사물이 화면을 가리지 않게 하되, 바닥 그림자는 남아야 해서 끄지 않고 그림자만 그린다.
        /// </summary>
        public void SetDisguiseShadowsOnly(bool shadowsOnly)
        {
            disguiseShadowsOnly = shadowsOnly;
            foreach (MeshRenderer partRenderer in partRenderers)
                partRenderer.shadowCastingMode = ShadowMode;
        }

        /// <summary>새 조각과 기존 조각이 같은 설정을 쓰도록 한곳에서 정한다.</summary>
        private ShadowCastingMode ShadowMode => disguiseShadowsOnly ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;

        /// <summary>외형으로 몸이 바뀌었을 수 있어 지금 몸 렌더러를 찾아 켜고 끈다.</summary>
        private void SetBodyVisible(bool visible)
        {
            foreach (Renderer bodyRenderer in CharacterSkinView.ResolveBodyRenderers(this, bodyRenderers))
                bodyRenderer.enabled = visible;
        }
    }
}
