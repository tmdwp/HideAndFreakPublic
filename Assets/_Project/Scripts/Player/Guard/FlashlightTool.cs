using FishNet.Object;
using FishNet.Object.Synchronizing;
using GuardVsMannequin.Audio;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>모두에게 보이도록 서버가 켜짐을 관리하는 사냥 시간 전용 가드 손전등.</summary>
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

        /// <summary>프리팹마다 맞추지 않도록 빛 수치는 조정 파일에서 넣는다.</summary>
        private void Awake()
        {
            playerInput = GetComponent<IPlayerInput>();
            noiseEmitter = GetComponent<NoiseEmitter>();
            spotLight.range = GameTuning.FlashlightRange;
            spotLight.intensity = GameTuning.FlashlightIntensity;
            spotLight.spotAngle = GameTuning.FlashlightAngle;
            spotLight.innerSpotAngle = Mathf.Min(spotLight.innerSpotAngle, spotLight.spotAngle);
            spotLight.enabled = false;
            isOn.OnChange += OnIsOnChanged;
        }

        /// <summary>
        /// 파괴된 뒤 동기화 콜백이 불리지 않도록 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            isOn.OnChange -= OnIsOnChanged;
        }

        /// <summary>그림자 비용을 줄이도록 다른 가드의 손전등은 그림자를 끄고, 입력은 소유자만 듣는다.</summary>
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
        /// 아바타가 사라진 뒤 입력 이벤트가 이 객체를 부르지 않도록 구독을 끊는다.
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsOwner && playerInput != null)
                playerInput.FlashlightPressed -= OnFlashlightPressed;
        }

        /// <summary>
        /// 사냥 시간이 끝나도 켜진 채 남으면 다음 탐색 단계에서 반칙이 되므로 서버가 끈다.
        /// </summary>
        private void Update()
        {
            if (this.IsServerReady() && isOn.Value && GameServices.CurrentStateOrWaiting != GameState.Hunting)
                isOn.Value = false;
        }

        /// <summary>
        /// 연타로 깜빡이지 않게 짧은 쿨타임을 소유자 화면에서 먼저 걸고 서버에 요청한다.
        /// </summary>
        private void OnFlashlightPressed()
        {
            if (!IsReady)
                return;
            cooldown.StartLocal(GameTuning.FlashlightToggleCooldown);
            ToggleServerRpc();
        }

        /// <summary>
        /// 클라이언트는 조작할 수 있으므로 서버가 사냥 시간·쿨타임을 다시 확인한 뒤에 뒤집는다.
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
        /// 호스트는 서버/클라이언트 콜백이 모두 오므로, 소리가 두 번 나지 않게 클라이언트 쪽에서만 처리한다.
        /// </summary>
        private void OnIsOnChanged(bool previousValue, bool nextValue, bool asServer)
        {
            if (asServer && IsClientInitialized)
                return;

            spotLight.enabled = nextValue;
            if (noiseEmitter != null)
                noiseEmitter.PlayLocal(NoiseType.FlashlightClick);
        }
    }
}
