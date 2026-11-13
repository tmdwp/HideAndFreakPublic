using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Player
{
    /// <summary>몸통·머리가 카메라를 가리지 않도록 1인칭 소유자 화면에 팔다리만 보여 준다.</summary>
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
        private Animator builtForAnimator;
        private bool isActive;

        /// <summary>
        /// 소유자 구성보다 먼저 구독해야 로컬 플레이어 등록을 놓치지 않으므로 Awake에서 구독한다.
        /// </summary>
        private void Awake()
        {
            skinView = GetComponent<CharacterSkinView>();
            LocalPlayerEvents.Spawned += OnLocalPlayerSpawned;
        }

        /// <summary>
        /// 이 컴포넌트가 늦게 켜져 등록 이벤트를 이미 놓쳤을 수 있으므로 현재 로컬 플레이어를 한 번 더 확인한다.
        /// </summary>
        private void Start()
        {
            PlayerIdentity currentPlayer = LocalPlayerEvents.Current;
            if (showOnSpawn && currentPlayer != null && currentPlayer.gameObject == gameObject)
                SetActive(true);
        }

        /// <summary>
        /// 마네킹은 스폰 때가 아니라 사냥 시간 1인칭 전환 때 켜야 해서 밖에서 켤 수 있게 연다.
        /// </summary>
        public void Show()
        {
            SetActive(true);
        }

        /// <summary>
        /// 런타임에 만든 메시는 자동으로 해제되지 않으므로 직접 지운다.
        /// </summary>
        private void OnDestroy()
        {
            LocalPlayerEvents.Spawned -= OnLocalPlayerSpawned;
            ClearLimbs();
        }

        /// <summary>
        /// 다른 사람 화면에는 온몸이 보여야 하므로, 이 아바타가 내 것으로 등록될 때만 팔다리 표시를 시작한다.
        /// </summary>
        private void OnLocalPlayerSpawned(PlayerIdentity identity)
        {
            if (showOnSpawn && identity != null && identity.gameObject == gameObject)
                SetActive(true);
        }

        /// <summary>
        /// 3인칭에서는 온몸이 이미 보이므로 복제 팔다리가 겹쳐 그려지지 않게 숨긴다.
        /// </summary>
        public void OnPerspectiveChanged(bool firstPerson)
        {
            SetActive(firstPerson);
        }

        /// <summary>애니메이션이 뼈를 움직인 뒤 확인하도록 LateUpdate에서 외형 교체와 숨김을 따라간다.</summary>
        private void LateUpdate()
        {
            if (!isActive)
                return;
            if (CurrentAnimator != builtForAnimator)
                Rebuild();

            for (int i = 0; i < limbRenderers.Count; i++)
            {
                SkinnedMeshRenderer source = limbSources[i];
                limbRenderers[i].enabled = source != null && source.enabled && source.gameObject.activeInHierarchy;
            }
        }

        /// <summary>
        /// 쓰지 않는 아바타에서 메시를 만들지 않도록 처음 켤 때 복제를 만든다.
        /// </summary>
        private void SetActive(bool active)
        {
            isActive = active;
            if (active && CurrentAnimator != builtForAnimator)
                Rebuild();
            foreach (GameObject limbObject in limbObjects)
                limbObject.SetActive(active);
        }

        /// <summary>외형 교체를 알아내려고 기억하는 애니메이터.</summary>
        private Animator CurrentAnimator => skinView != null ? skinView.Animator : null;

        /// <summary>외형마다 렌더러 구성이 달라 지금 외형의 렌더러마다 팔다리 복제를 새로 만든다.</summary>
        private void Rebuild()
        {
            ClearLimbs();
            builtForAnimator = CurrentAnimator;
            HashSet<Transform> limbBones = LimbMeshFilter.CollectLimbBones(builtForAnimator);
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

        /// <summary>애니메이션을 그대로 따라가도록 원본과 같은 뼈를 쓰고, 그림자는 원본이 그리므로 끈다.</summary>
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
        /// 다시 만들 때 이전 복제가 겹쳐 남지 않고 메시가 새지 않도록 모두 지운다.
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
            builtForAnimator = null;
        }
    }
}
