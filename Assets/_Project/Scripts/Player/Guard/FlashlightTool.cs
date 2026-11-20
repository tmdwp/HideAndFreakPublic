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

        [Tooltip("다른 사람 화면에 보이는 빛줄기·렌즈 빛 머티리얼. 비우면 실행 중에 같은 설정으로 만든다.")]
        [SerializeField] private Material beamMaterial;

        private readonly SyncVar<bool> isOn = new();
        private FlashlightBeamView beamView;

        // 외형이 정한 손전등 위치를 따르고, 외형이 지점을 안 주면 프리팹 위치로 되돌리려고 기억한다.
        private CharacterSkinView skinView;
        private CharacterSkin appliedSkin;
        private Transform mountPoint;
        private Vector3 defaultLocalPosition;
        private Color defaultColor;
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

            skinView = GetComponent<CharacterSkinView>();
            defaultLocalPosition = spotLight.transform.localPosition;
            defaultColor = spotLight.color;
            // 빛줄기가 만들어질 때 색을 읽으므로 그 전에 외형 색을 넣는다.
            ApplySkinLook();
        }

        /// <summary>
        /// 위치는 애니메이션이 끝난 뒤의 지점을 따라가고, 방향은 시선을 따르는 부모 회전을 그대로 둔다.
        /// 외형이 바뀌면(맵 외형 적용 등) 지점과 색을 다시 찾는다.
        /// </summary>
        private void LateUpdate()
        {
            if (skinView != null && skinView.ActiveSkin != appliedSkin)
                ApplySkinLook();
            if (mountPoint != null)
                spotLight.transform.position = mountPoint.position;
        }

        /// <summary>
        /// 외형마다 손전등 위치·색을 다르게 쓸 수 있도록 지금 외형에서 읽고, 지점이 없으면 프리팹 기본 위치를 쓴다.
        /// 밝기·범위·각도는 외형으로 바꾸면 유불리가 생겨 건드리지 않는다.
        /// </summary>
        private void ApplySkinLook()
        {
            appliedSkin = skinView != null ? skinView.ActiveSkin : null;
            mountPoint = appliedSkin != null ? skinView.FindModelPoint(FlashlightLook.MountPointName) : null;
            if (mountPoint == null)
                spotLight.transform.localPosition = defaultLocalPosition;
            spotLight.color = appliedSkin != null && appliedSkin.flashlight != null
                ? appliedSkin.flashlight.GetNormalizedColor()
                : defaultColor;
        }

        /// <summary>
        /// 파괴된 뒤 동기화 콜백이 불리지 않도록 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            isOn.OnChange -= OnIsOnChanged;
        }

        /// <summary>
        /// 그림자 비용을 줄이도록 다른 가드의 손전등은 그림자를 끄고, 어둠 속에서도 손전등을 든 가드를 찾을 수 있게 빛줄기를 붙인다. 입력은 소유자만 듣는다.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner)
            {
                spotLight.shadows = LightShadows.None;
                beamView = FlashlightBeamView.Create(spotLight, beamMaterial,
                    GameTuning.FlashlightBeamLength, GameTuning.FlashlightBeamAlpha, GameTuning.FlashlightLensGlowSize);
                // 이미 켜진 채 들어온 경우(늦게 접속·재접속)에도 보이도록 지금 상태를 맞춘다.
                beamView?.SetVisible(isOn.Value);
            }
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
            beamView?.SetVisible(nextValue);
            if (noiseEmitter != null)
                noiseEmitter.PlayLocal(NoiseType.FlashlightClick);
        }
    }
}
