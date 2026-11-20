using FishNet.Object;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Player
{
    /// <summary>내 아바타에서만 카메라·입력·물리가 돌도록 소유 여부에 따라 켜고 끈다.</summary>
    [RequireComponent(typeof(PlayerIdentity))]
    public class PlayerOwnershipSetup : NetworkBehaviour
    {
        [SerializeField] private Camera playerCamera;

        [Tooltip("로컬 소유자에게만 활성화할 컴포넌트. (입력, 이동 드라이버 등)")]
        [SerializeField] private Behaviour[] ownerOnlyBehaviours;

        [Tooltip("소유자 본인 화면에서는 그림자만 남길 렌더러. (1인칭 몸체 등)")]
        [SerializeField] private Renderer[] hiddenForOwner;

        [Tooltip("1인칭 역할이면 켠다. 외형(CharacterSkinView) 렌더러도 소유자 화면에서 그림자만 남긴다.")]
        [SerializeField] private bool hideSkinForOwner;

        private PlayerIdentity identity;
        private ICharacterMotor motor;

        /// <summary>
        /// 이동 어셈블리가 빠져 있으면 아바타가 조용히 움직이지 않으므로, 구성 요소를 찾으면서 없을 때 바로 알린다.
        /// </summary>
        private void Awake()
        {
            identity = GetComponent<PlayerIdentity>();
            motor = GetComponent<ICharacterMotor>();
            if (motor == null)
                Debug.LogError($"[PlayerOwnershipSetup] '{name}'에 캐릭터 이동 구현(ICharacterMotor)이 없습니다. 이동 어셈블리(GuardVsMannequin.Movement.ECM2)가 컴파일되었는지 확인하세요.", this);
        }

        /// <summary>
        /// 소유 여부는 클라이언트에서 스폰된 뒤에야 확정되므로 이 시점에 구성한다.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();

            if (IsOwner)
                SetupAsOwner();
            else
                SetupAsRemote();
        }

        /// <summary>
        /// 카메라·HUD가 관전으로 넘어갈 수 있도록 내 아바타가 사라지면 알린다.
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();

            if (IsOwner)
                LocalPlayerEvents.RaiseDespawned(identity);
        }

        /// <summary>
        /// 카메라·입력을 켜고 보간을 건 뒤에 알려야 구독자가 완성된 상태를 받으므로, 등록은 맨 마지막에 한다.
        /// </summary>
        private void SetupAsOwner()
        {
            playerCamera.gameObject.SetActive(true);
            // 프리팹을 고치지 않아도 내 시야각 설정이 적용되도록 내 카메라에만 붙인다.
            Cameras.ViewFieldOfView.Attach(playerCamera);
            motor?.SetupAsOwner(playerCamera);
            SetBehavioursEnabled(true);
            // 프리팹을 고치지 않아도 모든 역할이 사물을 밀 수 있도록, 충돌을 직접 겪는 소유자 아바타에만 붙인다.
            if (motor != null && !TryGetComponent(out PropPhysics.PropPushReporter _))
                gameObject.AddComponent<PropPhysics.PropPushReporter>();

            // 1인칭 역할은 자기 몸이 카메라를 가리지 않게 외형 렌더러도 그림자만 남긴다.
            Renderer[] targets = hideSkinForOwner ? CharacterSkinView.ResolveBodyRenderers(this, hiddenForOwner) : hiddenForOwner;
            foreach (Renderer target in targets)
                target.shadowCastingMode = ShadowCastingMode.ShadowsOnly;

            LocalPlayerEvents.RaiseSpawned(identity);
        }

        /// <summary>
        /// 남의 아바타가 내 입력에 반응하거나 스스로 시뮬레이션하면 NetworkTransform 위치와 어긋나므로 모두 끈다.
        /// </summary>
        private void SetupAsRemote()
        {
            playerCamera.gameObject.SetActive(false);
            SetBehavioursEnabled(false);
            motor?.SetupAsRemote();
        }

        /// <summary>
        /// 소유자 전용 목록은 인스펙터에서 정하므로, 종류를 몰라도 한 번에 켜고 끈다.
        /// </summary>
        private void SetBehavioursEnabled(bool enabledState)
        {
            foreach (Behaviour behaviour in ownerOnlyBehaviours)
                behaviour.enabled = enabledState;
        }
    }
}
