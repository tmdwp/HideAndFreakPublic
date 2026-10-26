using FishNet.Object;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 아바타가 로컬 소유자인지에 따라 카메라, 입력, 물리 시뮬레이션을 켜고 끈다.
    /// 이동은 소유자 권한(NetworkTransform)으로 동기화하고, 원격 아바타는 시뮬레이션하지 않는다.
    /// </summary>
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
        /// 같은 오브젝트의 PlayerIdentity와 캐릭터 이동 구현을 캐시한다.
        /// </summary>
        private void Awake()
        {
            identity = GetComponent<PlayerIdentity>();
            motor = GetComponent<ICharacterMotor>();
            if (motor == null)
                Debug.LogError($"[PlayerOwnershipSetup] '{name}'에 캐릭터 이동 구현(ICharacterMotor)이 없습니다. 이동 어셈블리(GuardVsMannequin.Movement.ECM2)가 컴파일되었는지 확인하세요.", this);
        }

        /// <summary>
        /// 클라이언트에서 스폰되면 소유 여부에 맞게 구성한다.
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
        /// 로컬 소유자 아바타가 제거되면 알린다.
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();

            if (IsOwner)
                LocalPlayerEvents.RaiseDespawned(identity);
        }

        /// <summary>
        /// 카메라와 입력을 켜고, 캐릭터 이동 구현에 카메라를 연결(보간 포함)한 뒤 로컬 플레이어로 등록한다.
        /// </summary>
        private void SetupAsOwner()
        {
            playerCamera.gameObject.SetActive(true);
            motor?.SetupAsOwner(playerCamera);
            SetBehavioursEnabled(true);

            // 1인칭 역할은 외형(CharacterSkinView) 렌더러도 그림자만 남긴다.
            Renderer[] targets = hideSkinForOwner ? CharacterSkinView.ResolveBodyRenderers(this, hiddenForOwner) : hiddenForOwner;
            foreach (Renderer target in targets)
                target.shadowCastingMode = ShadowCastingMode.ShadowsOnly;

            LocalPlayerEvents.RaiseSpawned(identity);
        }

        /// <summary>
        /// 원격 아바타는 카메라와 입력을 끄고, NetworkTransform이 위치를 적용하도록 시뮬레이션을 멈춘다.
        /// </summary>
        private void SetupAsRemote()
        {
            playerCamera.gameObject.SetActive(false);
            SetBehavioursEnabled(false);
            motor?.SetupAsRemote();
        }

        /// <summary>
        /// 소유자 전용 컴포넌트를 일괄로 켜고 끈다.
        /// </summary>
        private void SetBehavioursEnabled(bool enabledState)
        {
            foreach (Behaviour behaviour in ownerOnlyBehaviours)
                behaviour.enabled = enabledState;
        }
    }
}
