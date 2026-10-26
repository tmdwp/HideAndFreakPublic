using System.Collections.Generic;
using GuardVsMannequin.Props;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>
    /// 변신 상태에 맞춰 마네킹의 외형과 충돌체 크기를 바꾼다.
    /// 외형은 번호에 해당하는 맵 사물(<see cref="PropRegistry"/>)에서 그대로 복사한다(<see cref="PropAppearance"/>, 자식 조각 포함).
    /// 모든 피어에서 실행되어, 서버의 히트스캔도 변신한 크기로 판정된다.
    /// </summary>
    [RequireComponent(typeof(MannequinTransformer))]
    public class PropDisguiseView : MonoBehaviour, IPerspectiveListener
    {
        private const float MinRadius = 0.15f;
        private const float MaxRadius = 1.0f;

        [Tooltip("변신 중 숨길 마네킹 본체 렌더러.")]
        [SerializeField] private Renderer[] bodyRenderers;

        private readonly List<MeshRenderer> partRenderers = new();
        private readonly List<MeshFilter> partFilters = new();

        private MannequinTransformer transformer;
        private ICharacterMotor motor;
        private Transform disguiseRoot;
        private Vector3 defaultLocalPosition;
        private float defaultRadius;
        private float defaultHeight;
        private bool disguiseShadowsOnly;

        /// <summary>
        /// 기본 충돌체 크기를 저장하고 변신 이벤트를 구독한다.
        /// </summary>
        private void Awake()
        {
            transformer = GetComponent<MannequinTransformer>();
            motor = GetComponent<ICharacterMotor>();
            if (motor != null)
            {
                defaultRadius = motor.CapsuleRadius;
                defaultHeight = motor.CapsuleHeight;
            }
            transformer.DisguiseChanged += ApplyDisguise;
        }

        /// <summary>
        /// 이벤트 구독을 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            transformer.DisguiseChanged -= ApplyDisguise;
        }

        /// <summary>
        /// 사물 번호에 맞게 외형과 충돌체를 적용한다. 번호가 없거나 사물을 찾지 못하면 원래 모습으로 되돌린다.
        /// </summary>
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

            // 사물의 바닥이 캐릭터 발 위치(피벗)에 오도록 배치한다. 좌우 방향은 마네킹이 바라보는 방향을 따른다.
            Bounds bounds = appearance.Bounds;
            DisguiseBounds = bounds;
            defaultLocalPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            ResetPose();
            disguiseRoot.gameObject.SetActive(true);

            SetBodyVisible(false);

            float radius = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f, MinRadius, MaxRadius);
            motor?.SetCapsule(radius, Mathf.Max(bounds.size.y, radius * 2f));
        }

        /// <summary>변신 외형이 보이는 중인지.</summary>
        public bool IsShowing => disguiseRoot != null && disguiseRoot.gameObject.activeSelf;

        /// <summary>지금 외형의 경계. (외형 기준 공간 — <see cref="WorldPose"/>로 월드에 놓인다)</summary>
        public Bounds DisguiseBounds { get; private set; }

        /// <summary>외형 기준 공간의 월드 자세. (외형이 없으면 아바타 자세)</summary>
        public Pose WorldPose => disguiseRoot != null
            ? new Pose(disguiseRoot.position, disguiseRoot.rotation)
            : new Pose(transform.position, transform.rotation);

        /// <summary>
        /// 외형을 월드 자세에 그대로 놓는다. (날아가는 동안 서버 물리 자세를 따라감)
        /// </summary>
        public void SetWorldPose(Pose pose)
        {
            if (disguiseRoot != null)
                disguiseRoot.SetPositionAndRotation(pose.position, pose.rotation);
        }

        /// <summary>
        /// 외형을 아바타 기준 자세에 놓는다. (멈춘 뒤 넘어진 모습 유지)
        /// </summary>
        public void SetLocalPose(Pose pose)
        {
            if (disguiseRoot != null)
                disguiseRoot.SetLocalPositionAndRotation(pose.position, pose.rotation);
        }

        /// <summary>
        /// 외형을 기본 자세(사물 바닥이 발 위치, 똑바로 선 모습)로 되돌린다.
        /// </summary>
        public void ResetPose()
        {
            if (disguiseRoot != null)
                disguiseRoot.SetLocalPositionAndRotation(defaultLocalPosition, Quaternion.identity);
        }

        /// <summary>
        /// 외형 조각 수만큼 렌더러를 맞추고 메시·머티리얼·위치를 복사한다. 남는 조각은 끈다.
        /// </summary>
        private void ApplyParts(IReadOnlyList<PropAppearance.Part> parts)
        {
            while (partRenderers.Count < parts.Count)
                CreatePart();

            for (int i = 0; i < partRenderers.Count; i++)
            {
                bool used = i < parts.Count;
                partRenderers[i].gameObject.SetActive(used);
                if (!used)
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
        /// 변신 외형을 숨기고 본체와 기본 충돌체로 되돌린다.
        /// </summary>
        private void RestoreBody()
        {
            if (disguiseRoot != null)
                disguiseRoot.gameObject.SetActive(false);

            SetBodyVisible(true);
            motor?.SetCapsule(defaultRadius, defaultHeight);
        }

        /// <summary>
        /// 변신 외형을 담을 자식 오브젝트를 처음 한 번만 만든다.
        /// </summary>
        private void EnsureDisguiseRoot()
        {
            if (disguiseRoot != null)
                return;

            disguiseRoot = new GameObject("Disguise").transform;
            disguiseRoot.SetParent(transform, false);
        }

        /// <summary>
        /// 외형 조각 하나(메시 필터 + 렌더러)를 만든다.
        /// </summary>
        private void CreatePart()
        {
            var part = new GameObject($"Part_{partRenderers.Count}");
            part.transform.SetParent(disguiseRoot, false);
            partFilters.Add(part.AddComponent<MeshFilter>());
            MeshRenderer partRenderer = part.AddComponent<MeshRenderer>();
            partRenderer.shadowCastingMode = ShadowMode;
            partRenderers.Add(partRenderer);
        }

        /// <summary>
        /// 소유자 시점이 바뀌면 1인칭일 때 변신한 사물이 시야를 가리지 않게 그림자만 남긴다. (테스트 방 시점 전환)
        /// </summary>
        public void OnPerspectiveChanged(bool firstPerson)
        {
            SetDisguiseShadowsOnly(firstPerson);
        }

        /// <summary>
        /// 변신 외형을 그림자만 보이게 할지 정한다. 사냥 시간 1인칭에서 본인 화면에 사물이 가리지 않게 한다.
        /// </summary>
        public void SetDisguiseShadowsOnly(bool shadowsOnly)
        {
            disguiseShadowsOnly = shadowsOnly;
            foreach (MeshRenderer partRenderer in partRenderers)
                partRenderer.shadowCastingMode = ShadowMode;
        }

        /// <summary>지금 적용할 변신 외형의 그림자 모드.</summary>
        private ShadowCastingMode ShadowMode => disguiseShadowsOnly ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;

        /// <summary>
        /// 마네킹 본체 렌더러를 켜고 끈다.
        /// </summary>
        private void SetBodyVisible(bool visible)
        {
            // 외형(CharacterSkinView)이 있으면 외형 렌더러를 숨긴다.
            foreach (Renderer bodyRenderer in CharacterSkinView.ResolveBodyRenderers(this, bodyRenderers))
                bodyRenderer.enabled = visible;
        }
    }
}
