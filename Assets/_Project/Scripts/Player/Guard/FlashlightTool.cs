using FishNet.Object;
using FishNet.Object.Synchronizing;
using GuardVsMannequin.Audio;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>
    /// 가드의 손전등. 사냥 시간에만 E키로 켜고 끌 수 있고, 배터리는 무제한이다.
    /// 켜짐 상태는 서버가 관리해 모든 플레이어에게 보이며, 사냥 시간이 끝나면 자동으로 꺼진다.
    /// </summary>
    public class FlashlightTool : NetworkBehaviour, ISwitchableTool
    {
        [Tooltip("카메라 피벗 아래에 둔 스포트라이트. 원격 가드는 PlayerViewSync로 상하 방향이 맞춰진다.")]
        [SerializeField] private Light spotLight;

        [Tooltip("본인 손전등의 그림자에서 뺄 가까운 거리(m). 1인칭에서 보이지 않는 본인 몸(머리·모자·팔·장갑)은 그림자만 그리므로,\n" +
                 "손전등 바로 앞의 몸 그림자가 빛을 막지 않도록 이 거리 안의 물체는 그림자를 만들지 않게 한다.")]
        [SerializeField] private float ownerShadowNearPlane = 0.8f;

        private readonly SyncVar<bool> isOn = new();
        private readonly ToolCooldown cooldown = new();

        private IPlayerInput playerInput;
        private NoiseEmitter noiseEmitter;

        public string DisplayName => Loc.Get("tool.flashlight");
        public float CooldownRemaining => cooldown.LocalRemaining;
        public bool IsReady => GameServices.CurrentStateOrWaiting == GameState.Hunting && CooldownRemaining <= 0f;

        /// <inheritdoc />
        public bool IsOn => isOn.Value;

        /// <summary>
        /// 구성 요소를 캐시하고, 켜짐 상태 변경을 조명에 반영하도록 연결한다.
        /// </summary>
        private void Awake()
        {
            playerInput = GetComponent<IPlayerInput>();
            noiseEmitter = GetComponent<NoiseEmitter>();
            // 범위·퍼짐 각도·밝기는 조정 파일(game_tuning.csv)을 따른다.
            spotLight.range = GameTuning.FlashlightRange;
            spotLight.intensity = GameTuning.FlashlightIntensity;
            spotLight.spotAngle = GameTuning.FlashlightAngle;
            spotLight.innerSpotAngle = Mathf.Min(spotLight.innerSpotAngle, spotLight.spotAngle);
            spotLight.enabled = false;
            isOn.OnChange += OnIsOnChanged;
        }

        /// <summary>
        /// 동기화 콜백을 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            isOn.OnChange -= OnIsOnChanged;
        }

        /// <summary>
        /// 소유자만 손전등 키 입력을 구독한다.
        /// 다른 가드의 손전등은 그림자를 끈다. (그림자 조명마다 그림자 맵을 따로 그려야 해서, 가드가 여럿이면 비용이 크다)
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner)
                spotLight.shadows = LightShadows.None;
            else
                spotLight.shadowNearPlane = Mathf.Max(spotLight.shadowNearPlane, ownerShadowNearPlane);
            if (IsOwner && playerInput != null)
                playerInput.FlashlightPressed += OnFlashlightPressed;
        }

        /// <summary>
        /// 입력 구독을 해제한다.
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsOwner && playerInput != null)
                playerInput.FlashlightPressed -= OnFlashlightPressed;
        }

        /// <summary>
        /// 서버에서 사냥 시간이 아니면 손전등을 끈다.
        /// </summary>
        private void Update()
        {
            if (this.IsServerReady() && isOn.Value && GameServices.CurrentStateOrWaiting != GameState.Hunting)
                isOn.Value = false;
        }

        /// <summary>
        /// 사냥 시간이고 쿨타임이 끝났으면 서버에 켜기/끄기를 요청한다.
        /// </summary>
        private void OnFlashlightPressed()
        {
            if (!IsReady)
                return;
            cooldown.StartLocal(GameTuning.FlashlightToggleCooldown);
            ToggleServerRpc();
        }

        /// <summary>
        /// 서버에서 사냥 시간·쿨타임을 확인하고 켜짐 상태를 뒤집는다.
        /// </summary>
        [ServerRpc]
        private void ToggleServerRpc()
        {
            if (GameServices.CurrentStateOrWaiting != GameState.Hunting)
                return;
            if (!cooldown.TryStartServer(GameTuning.FlashlightToggleCooldown))
                return;

            isOn.Value = !isOn.Value;
        }

        /// <summary>
        /// 켜짐 상태를 조명에 반영하고 스위치 소리를 낸다.
        /// </summary>
        private void OnIsOnChanged(bool prev, bool next, bool asServer)
        {
            // 호스트는 서버/클라이언트 콜백이 모두 오므로 클라이언트 쪽에서만 처리한다.
            if (asServer && IsClientInitialized)
                return;

            spotLight.enabled = next;
            if (noiseEmitter != null)
                noiseEmitter.PlayLocal(NoiseType.FlashlightClick);
        }
    }
}
