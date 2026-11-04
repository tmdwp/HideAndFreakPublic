using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 1인칭 소유자 화면에서 몸 모델 중 팔다리만 보여준다. (몸통·머리는 카메라를 가리므로 뺀다)
    /// - 가드: 로컬 플레이어가 되면 바로 켠다.
    /// - 마네킹: 사냥 시간에 1인칭이 될 때 <see cref="Mannequin.MannequinHuntingMode"/>가 켠다. (구울이 휘두르는 팔이 보이도록)
    /// 원본 몸은 소유자 구성(<see cref="PlayerOwnershipSetup"/>)대로 그림자만 그리고,
    /// 팔다리 삼각형만 남긴 복제 메시(<see cref="LimbMeshFilter"/>)를 같은 뼈에 붙여 그림자 없이 그린다.
    /// 그래서 화면에는 팔다리만 보이고, 바닥 그림자는 온몸 그대로 남는다. (본인 화면 전용 — 다른 플레이어에게는 온몸이 보인다)
    /// 원본 몸이 숨겨지면(변신 등) 복제도 함께 숨긴다. 팔다리 뼈를 찾지 못하거나 모델의 Read/Write가 꺼져 있으면 아무것도 더하지 않는다.
    /// </summary>
    public class FirstPersonBodyView : MonoBehaviour, IPerspectiveListener
    {
        private const string LimbObjectName = "FirstPersonLimbs";

        [Tooltip("로컬 플레이어가 되자마자 1인칭 팔다리를 보일지 여부. 가드는 켜고, 마네킹은 끈다(사냥 시간 1인칭에서 켜짐).")]
        [SerializeField] private bool showOnSpawn = true;

        private readonly List<GameObject> limbObjects = new();
        private readonly List<SkinnedMeshRenderer> limbSources = new();
        private readonly List<SkinnedMeshRenderer> limbRenderers = new();
        private readonly List<Mesh> limbMeshes = new();
        private CharacterSkinView skinView;
        private Animator builtFor;
        private bool isActive;

        /// <summary>
        /// 외형을 캐시하고 로컬 플레이어 등록을 구독한다.
        /// </summary>
        private void Awake()
        {
            skinView = GetComponent<CharacterSkinView>();
            LocalPlayerEvents.Spawned += OnLocalPlayerSpawned;
        }

        /// <summary>
        /// 이미 로컬 플레이어로 등록된 뒤에 켜졌으면 바로 적용한다.
        /// </summary>
        private void Start()
        {
            PlayerIdentity current = LocalPlayerEvents.Current;
            if (showOnSpawn && current != null && current.gameObject == gameObject)
                SetActive(true);
        }

        /// <summary>
        /// 1인칭 팔다리를 보인다. (마네킹: 사냥 시간 1인칭 전환 때)
        /// </summary>
        public void Show()
        {
            SetActive(true);
        }

        /// <summary>
        /// 구독을 해제하고 만든 메시·오브젝트를 지운다.
        /// </summary>
        private void OnDestroy()
        {
            LocalPlayerEvents.Spawned -= OnLocalPlayerSpawned;
            ClearLimbs();
        }

        /// <summary>
        /// 이 아바타가 로컬 플레이어로 등록되면(소유자 구성 직후) 1인칭 팔다리 표시를 시작한다.
        /// </summary>
        private void OnLocalPlayerSpawned(PlayerIdentity identity)
        {
            if (showOnSpawn && identity != null && identity.gameObject == gameObject)
                SetActive(true);
        }

        /// <summary>
        /// 테스트 방 시점 전환: 1인칭이면 팔다리를 보이고, 3인칭이면(온몸이 보이므로) 숨긴다.
        /// </summary>
        public void OnPerspectiveChanged(bool firstPerson)
        {
            SetActive(firstPerson);
        }

        /// <summary>
        /// 외형이 바뀌면(뼈가 달라짐) 팔다리 복제를 다시 만들고, 원본 몸이 숨겨졌는지(변신 등)에 맞춰 복제도 보이거나 숨긴다.
        /// </summary>
        private void LateUpdate()
        {
            if (!isActive)
                return;
            if (CurrentAnimator != builtFor)
                Rebuild();

            for (int i = 0; i < limbRenderers.Count; i++)
            {
                SkinnedMeshRenderer source = limbSources[i];
                limbRenderers[i].enabled = source != null && source.enabled && source.gameObject.activeInHierarchy;
            }
        }

        /// <summary>
        /// 팔다리 표시를 켜고 끈다. 처음 켤 때 복제를 만든다.
        /// </summary>
        private void SetActive(bool active)
        {
            isActive = active;
            if (active && CurrentAnimator != builtFor)
                Rebuild();
            foreach (GameObject limbObject in limbObjects)
                limbObject.SetActive(active);
        }

        /// <summary>지금 외형의 애니메이터. 없으면 null.</summary>
        private Animator CurrentAnimator => skinView != null ? skinView.Animator : null;

        /// <summary>
        /// 지금 외형의 몸 렌더러마다 팔다리만 남긴 복제 렌더러를 만든다.
        /// </summary>
        private void Rebuild()
        {
            ClearLimbs();
            builtFor = CurrentAnimator;
            HashSet<Transform> limbBones = LimbMeshFilter.CollectLimbBones(builtFor);
            if (limbBones.Count == 0)
                return;

            bool hasUnreadable = false;
            foreach (Renderer body in CharacterSkinView.ResolveBodyRenderers(this, null))
            {
                if (!(body is SkinnedMeshRenderer skinned) || !skinned.gameObject.activeInHierarchy || !skinned.enabled)
                    continue;
                if (skinned.sharedMesh != null && !skinned.sharedMesh.isReadable)
                {
                    hasUnreadable = true;
                    continue;
                }

                Mesh limbMesh = LimbMeshFilter.CreateLimbOnlyMesh(skinned, limbBones);
                if (limbMesh != null)
                    CreateLimbRenderer(skinned, limbMesh);
            }

            if (hasUnreadable)
                Debug.LogWarning($"[FirstPersonBodyView] '{name}' 외형 모델의 Read/Write가 꺼져 있어 1인칭 팔다리를 만들지 못한 부분이 있습니다. 모델 가져오기 설정에서 Read/Write를 켜세요.", this);
        }

        /// <summary>
        /// 원본과 같은 자리·뼈·재질로 팔다리 메시를 그리는 렌더러를 만든다. (그림자는 원본이 그린다)
        /// </summary>
        private void CreateLimbRenderer(SkinnedMeshRenderer source, Mesh limbMesh)
        {
            var limbObject = new GameObject(LimbObjectName) { layer = source.gameObject.layer };
            Transform limbTransform = limbObject.transform;
            limbTransform.SetParent(source.transform.parent, false);
            limbTransform.SetLocalPositionAndRotation(source.transform.localPosition, source.transform.localRotation);
            limbTransform.localScale = source.transform.localScale;

            SkinnedMeshRenderer limbRenderer = limbObject.AddComponent<SkinnedMeshRenderer>();
            limbRenderer.sharedMesh = limbMesh;
            limbRenderer.bones = source.bones;
            limbRenderer.rootBone = source.rootBone;
            limbRenderer.sharedMaterials = source.sharedMaterials;
            limbRenderer.shadowCastingMode = ShadowCastingMode.Off;
            limbRenderer.receiveShadows = source.receiveShadows;
            // 카메라 바로 앞에 있어 경계 계산이 어긋나면 잘려 보일 수 있으므로 화면 밖 판정 없이 매번 갱신한다.
            limbRenderer.updateWhenOffscreen = true;

            limbObjects.Add(limbObject);
            limbSources.Add(source);
            limbRenderers.Add(limbRenderer);
            limbMeshes.Add(limbMesh);
        }

        /// <summary>
        /// 만든 복제 오브젝트와 메시를 지운다.
        /// </summary>
        private void ClearLimbs()
        {
            foreach (GameObject limbObject in limbObjects)
            {
                if (limbObject != null)
                    Destroy(limbObject);
            }
            foreach (Mesh limbMesh in limbMeshes)
            {
                if (limbMesh != null)
                    Destroy(limbMesh);
            }
            limbObjects.Clear();
            limbSources.Clear();
            limbRenderers.Clear();
            limbMeshes.Clear();
            builtFor = null;
        }
    }
}
