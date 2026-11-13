using System.Collections.Generic;
using GuardVsMannequin.Core;
using GuardVsMannequin.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Player
{
    /// <summary>맵마다 외형을 바꿀 수 있도록 캐릭터 외형을 붙이고 애니메이터를 준비한다.</summary>
    public class CharacterSkinView : MonoBehaviour
    {
        [Tooltip("맵에서 외형을 고를 때 사용할 역할.")]
        [SerializeField] private PlayerRole role = PlayerRole.Mannequin;

        [Tooltip("맵이 외형을 지정하지 않았을 때의 외형.")]
        [SerializeField] private CharacterSkin defaultSkin;

        [Tooltip("사냥 시간 외형. 맵이 사냥 외형을 지정하지 않았을 때 쓴다. 비우면 사냥 시간에도 외형을 바꾸지 않는다.")]
        [SerializeField] private CharacterSkin huntSkin;

        [Tooltip("외형 모델을 붙일 부모. (감정 표현이 이 트랜스폼을 움직인다)")]
        [SerializeField] private Transform visualRoot;

        [Tooltip("외형이 없을 때 쓰는 기본 도형 렌더러. 외형을 붙이면 숨긴다.")]
        [SerializeField] private Renderer[] fallbackRenderers;

        private readonly List<Renderer> skinRenderers = new();
        private Renderer[] bodyRenderers;
        private AnimatorParameterSet parameters;
        private LocomotionAnimatorDriver locomotion;
        private HandAimIK aimIK;
        private CharacterSkin activeSkin;
        private GameObject activeModel;
        private bool isApplied;

        /// <summary>외형이 바뀌면 달라지므로 쓸 때마다 읽는 지금 몸 렌더러.</summary>
        public Renderer[] BodyRenderers
        {
            get
            {
                EnsureApplied();
                return bodyRenderers;
            }
        }

        /// <summary>외형 없이 기본 도형을 쓰면 null.</summary>
        public CharacterSkin ActiveSkin
        {
            get
            {
                EnsureApplied();
                return activeSkin;
            }
        }

        /// <summary>서버가 맞는 순간에 판정하도록 외형마다 다른 공격 지연(초).</summary>
        public float AttackHitDelay => ActiveSkin != null ? ActiveSkin.attackHitDelay : 0f;

        /// <summary>외형이 바뀌면 달라지므로 쓸 때마다 읽는 애니메이터.</summary>
        public Animator Animator
        {
            get
            {
                EnsureApplied();
                return parameters?.Animator;
            }
        }

        /// <summary>
        /// 다른 컴포넌트가 Start에서 렌더러를 찾기 전에 외형이 붙어 있도록 Awake에서 적용한다.
        /// </summary>
        private void Awake()
        {
            EnsureApplied();
        }

        /// <summary>
        /// 이동 드라이버는 일반 클래스라 스스로 매 프레임 돌 수 없어서 여기서 대신 돌린다.
        /// </summary>
        private void Update()
        {
            locomotion?.Tick(Time.deltaTime);
        }

        /// <summary>맵마다 다른 괴물을 쓸 수 있도록 맵의 사냥 외형을 우선해 바꾼다.</summary>
        public void UseHuntSkin()
        {
            EnsureApplied();
            CharacterSkin mapHuntSkin = MapInfo.Current != null ? MapInfo.Current.GetHuntSkin(role) : null;
            CharacterSkin skin = mapHuntSkin != null ? mapHuntSkin : huntSkin;
            if (skin == null || skin.modelPrefab == null || skin == activeSkin)
                return;

            ApplySkin(skin);
        }

        /// <summary>
        /// 외형마다 지원하는 동작이 달라서, 호출하는 쪽이 대체 방식을 고를 수 있도록 있는지 묻는다.
        /// </summary>
        public bool HasParameter(string parameterName)
        {
            EnsureApplied();
            return parameters != null && parameters.Has(parameterName);
        }

        /// <summary>
        /// 애니메이션이 없는 외형도 있어서, 트리거가 없으면 경고 없이 false를 준다.
        /// </summary>
        public bool TryPlayTrigger(string triggerName)
        {
            EnsureApplied();
            return parameters != null && parameters.TrySetTrigger(triggerName);
        }

        /// <summary>
        /// 애니메이션이 없는 외형도 있어서, 파라미터가 없으면 경고 없이 false를 준다.
        /// </summary>
        public bool TrySetInteger(string parameterName, int value)
        {
            EnsureApplied();
            return parameters != null && parameters.TrySetInteger(parameterName, value);
        }

        /// <summary>
        /// 애니메이션이 없는 외형도 있어서, 파라미터가 없으면 경고 없이 false를 준다.
        /// </summary>
        public bool TrySetBool(string parameterName, bool value)
        {
            EnsureApplied();
            return parameters != null && parameters.TrySetBool(parameterName, value);
        }

        /// <summary>
        /// 외형 시스템 도입 전 프리팹과 호환하도록, 외형 컴포넌트가 없으면 지정한 렌더러를 대신 준다.
        /// </summary>
        public static Renderer[] ResolveBodyRenderers(Component owner, Renderer[] fallback)
        {
            CharacterSkinView skinView = owner.GetComponent<CharacterSkinView>();
            return skinView != null ? skinView.BodyRenderers : fallback ?? new Renderer[0];
        }

        /// <summary>조준 IK는 외형 모델에 붙어 바깥에서 찾기 어려워 여기서 방향 기준을 넘긴다.</summary>
        public void SetAimSource(Transform aimSource)
        {
            EnsureApplied();
            if (aimIK != null)
                aimIK.AimSource = aimSource;
        }

        /// <summary>외형마다 손 위치가 달라 총 쪽이 매 프레임 넘긴다.</summary>
        public void SetAimHandOffset(Vector3 handOffset)
        {
            if (aimIK != null)
                aimIK.HandOffset = handOffset;
        }

        /// <summary>양손 총이 IK 직전에 손 목표를 정할 수 있도록 연결한다.</summary>
        public void SetHandGoalSource(IHandIKGoalSource goalSource)
        {
            EnsureApplied();
            if (aimIK != null)
                aimIK.GoalSource = goalSource;
        }

        /// <summary>
        /// 다른 컴포넌트의 Awake가 먼저 불려도 렌더러를 조회할 수 있도록, 처음 조회될 때 한 번만 적용한다.
        /// </summary>
        private void EnsureApplied()
        {
            if (isApplied)
                return;
            isApplied = true;

            CharacterSkin skin = ChooseSkin();
            if (skin == null || skin.modelPrefab == null)
            {
                bodyRenderers = fallbackRenderers ?? new Renderer[0];
                return;
            }

            ApplySkin(skin);
        }

        /// <summary>
        /// 맵마다 분위기에 맞는 외형을 쓸 수 있도록 맵 지정 외형을 우선하고, 없으면 기본 외형을 쓴다.
        /// </summary>
        private CharacterSkin ChooseSkin()
        {
            CharacterSkin mapSkin = MapInfo.Current != null ? MapInfo.Current.GetSkin(role) : null;
            return mapSkin != null ? mapSkin : defaultSkin;
        }

        /// <summary>
        /// 변신 중이거나 1인칭이라 숨겨 둔 상태에서 외형이 바뀌어도 그대로 숨겨져 있도록, 이전 몸체의 표시·그림자 설정을 새 몸체에 옮긴다.
        /// </summary>
        private void ApplySkin(CharacterSkin skin)
        {
            Renderer previousRenderer = FirstRenderer(bodyRenderers);
            bool isVisible = previousRenderer == null || previousRenderer.enabled;
            ShadowCastingMode shadowMode = previousRenderer != null ? previousRenderer.shadowCastingMode : ShadowCastingMode.On;
            RemoveActiveModel();

            activeSkin = skin;
            Transform parent = visualRoot != null ? visualRoot : transform;
            activeModel = Instantiate(skin.modelPrefab, parent, false);
            activeModel.name = skin.modelPrefab.name;
            activeModel.transform.localRotation = Quaternion.Euler(0f, skin.yawOffset, 0f);

            activeModel.GetComponentsInChildren(true, skinRenderers);
            bodyRenderers = skinRenderers.ToArray();
            foreach (Renderer fallbackRenderer in fallbackRenderers ?? new Renderer[0])
            {
                if (fallbackRenderer != null)
                    fallbackRenderer.enabled = false;
            }

            // 첫 적용은 기본값(보임·그림자 켬)이므로 이전 상태를 옮길 것이 없다.
            if (previousRenderer != null)
            {
                foreach (Renderer body in bodyRenderers)
                {
                    body.enabled = isVisible;
                    body.shadowCastingMode = shadowMode;
                }
            }

            SetupAnimator(activeModel, skin);
        }

        /// <summary>
        /// 이전 모델의 애니메이터를 계속 쓰지 않도록 관련 참조를 비우고 모델을 지운다.
        /// </summary>
        private void RemoveActiveModel()
        {
            parameters = null;
            locomotion = null;
            aimIK = null;
            if (activeModel == null)
                return;

            // Destroy는 프레임 끝에 처리되므로, 같은 프레임의 렌더러 조회에 걸리지 않게 먼저 끈다.
            activeModel.SetActive(false);
            Destroy(activeModel);
            activeModel = null;
        }

        /// <summary>파괴된 렌더러가 목록 앞에 있을 수 있어 살아 있는 것을 고른다.</summary>
        private static Renderer FirstRenderer(Renderer[] renderers)
        {
            if (renderers == null)
                return null;
            foreach (Renderer candidate in renderers)
            {
                if (candidate != null)
                    return candidate;
            }
            return null;
        }

        /// <summary>
        /// 이동은 캐릭터 이동 코드가 맡으므로 루트 모션을 끄고, 외형 설정에 따라 조준 IK·제자리 고정을 붙인다.
        /// </summary>
        private void SetupAnimator(GameObject model, CharacterSkin skin)
        {
            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator == null)
                return;

            if (skin.animatorController != null)
                animator.runtimeAnimatorController = skin.animatorController;
            animator.applyRootMotion = false;

            // 에셋 팩 클립 이벤트는 제거된 스크립트를 부르므로 기본으로 끈다.
            animator.fireEvents = skin.fireAnimationEvents;

            parameters = new AnimatorParameterSet(animator);
            locomotion = new LocomotionAnimatorDriver(parameters, transform);

            if (skin.useAimIK && animator.isHuman)
                aimIK = animator.gameObject.AddComponent<HandAimIK>();

            // Generic 모델의 이동 클립은 뼈가 앞으로 나가므로 제자리로 고정한다.
            if (skin.keepRootBoneInPlace && !animator.isHuman)
                animator.gameObject.AddComponent<RootBoneInPlaceLock>();
        }
    }
}
