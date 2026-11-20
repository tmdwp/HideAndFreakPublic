using FishNet.Object;
using FishNet.Object.Synchronizing;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>
    /// 사냥 시간 구울이 Shift를 누르고 있는 동안 쭈그려 움직이지 않고 소리를 죽이는 은신 상태.
    /// 은신하는 동안 달리기와 같은 스태미나를 쓰고, 바닥나면 저절로 풀린다.
    /// </summary>
    [RequireComponent(typeof(MannequinTransformer), typeof(PlayerHealth))]
    public class GhoulStealth : NetworkBehaviour, IStealthState, IMoveSpeedModifier, ISprintBlocker, IStaminaDrain
    {
        [Tooltip("쭈그린 동작(Crouch 파라미터)이 없는 외형에서 대신 몸을 낮출 높이(m, 음수).")]
        [SerializeField] private float fallbackCrouchHeight = -0.45f;

        private readonly SyncVar<bool> isStealthedSynced = new();

        // 소유자 스태미나는 조작할 수 있어, 서버가 은신 시간만으로 따로 세어 상한을 지킨다.
        private readonly StaminaModel serverStamina = new();

        private IPlayerInput playerInput;
        private IStaminaSource staminaSource;
        private IStaminaDepleter staminaDepleter;
        private MannequinTransformer transformer;
        private PlayerHealth health;
        private CharacterSkinView skinView;
        private bool isStealthedLocal;
        private bool isReleaseRequired;
        private bool isVisualCrouched;

        /// <summary>
        /// 소유자는 응답을 기다리면 멈추는 순간이 늦어서 입력을 바로 반영한 값을 쓴다.
        /// 이동·소리 코드가 네트워크 초기화 전에도 매 프레임 읽으므로, 그때는 은신하지 않은 것으로 본다.
        /// </summary>
        public bool IsStealthed => this.IsNetworkReady() && (IsOwner ? isStealthedLocal : isStealthedSynced.Value);

        /// <inheritdoc />
        public bool ServerIsStealthed => isStealthedSynced.Value;

        /// <inheritdoc />
        public float SpeedMultiplier => IsStealthed ? 0f : 1f;

        /// <inheritdoc />
        public bool IsSprintBlocked => CanStealth;

        /// <inheritdoc />
        public float StaminaDrainPerSecond => IsStealthed ? StealthDrainPerSecond : 0f;

        /// <summary>조정 파일 값을 바꾸면 바로 반영되도록 매번 계산한다.</summary>
        private static float StealthDrainPerSecond => GhoulRules.GetStealthDrainPerSecond(GameTuning.GhoulStealthStaminaSeconds);

        /// <summary>스태미나 구현이 없는 아바타는 은신 시간을 제한하지 않는다.</summary>
        private bool HasOwnerStamina => staminaSource == null || !staminaSource.IsExhausted;

        /// <summary>사물 모습은 숨는 수단이 따로 있고, 탐색 단계 마네킹에는 은신이 없어서 사냥 시간 구울 모습일 때만 허용한다.</summary>
        private bool CanStealth => GameServices.CurrentStateOrWaiting == GameState.Hunting && !transformer.IsDisguised && health.IsAlive;

        /// <summary>매 프레임 GetComponent를 부르지 않도록 미리 찾아 둔다.</summary>
        private void Awake()
        {
            playerInput = GetComponent<IPlayerInput>();
            staminaSource = GetComponent<IStaminaSource>();
            staminaDepleter = GetComponent<IStaminaDepleter>();
            transformer = GetComponent<MannequinTransformer>();
            health = GetComponent<PlayerHealth>();
            skinView = GetComponent<CharacterSkinView>();
        }

        /// <summary>입력은 소유자만, 조건 확인은 서버가, 자세 표시는 모든 피어가 한다.</summary>
        private void Update()
        {
            if (!this.IsNetworkReady())
                return;

            if (IsOwner)
                UpdateOwnerInput();
            if (IsServerInitialized)
                UpdateServerLimit();

            UpdateCrouchVisual(IsStealthed);
        }

        /// <summary>
        /// 도약 뒤나 스태미나가 바닥난 뒤 Shift를 계속 누르고 있어도 바로 다시 숨지 않도록, 한 번 떼야 다시 은신할 수 있게 한다.
        /// </summary>
        private void UpdateOwnerInput()
        {
            bool isShiftHeld = playerInput != null && playerInput.IsSprintHeld;
            if (!isShiftHeld)
                isReleaseRequired = false;
            if (isStealthedLocal && !HasOwnerStamina)
                isReleaseRequired = true;

            bool wantsStealth = isShiftHeld && !isReleaseRequired && CanStealth && HasOwnerStamina;
            if (wantsStealth == isStealthedLocal)
                return;

            isStealthedLocal = wantsStealth;
            SetStealthServerRpc(wantsStealth);
        }

        /// <summary>
        /// 소유자가 풀지 않아도 단계·변신·생존 조건이 깨지거나 서버가 센 스태미나가 바닥나면 서버가 은신을 끝낸다.
        /// 서버는 은신이 소유자보다 늦게 시작되므로 정상 클라이언트보다 먼저 끊지 않는다.
        /// </summary>
        private void UpdateServerLimit()
        {
            serverStamina.Tick(isStealthedSynced.Value, StealthDrainPerSecond, Time.deltaTime);
            if (isStealthedSynced.Value && (!CanStealth || serverStamina.IsExhausted))
                isStealthedSynced.Value = false;
        }

        /// <summary>클라이언트는 조작할 수 있어 서버가 단계·변신·생존·스태미나를 다시 확인한다.</summary>
        [ServerRpc]
        private void SetStealthServerRpc(bool wantsStealth)
        {
            isStealthedSynced.Value = wantsStealth && CanStealth && !serverStamina.IsExhausted;
        }

        /// <summary>
        /// 도약 한 번에 스태미나를 모두 쓰게 해, 연달아 도약하거나 곧바로 다시 숨지 못하게 한다.
        /// 스태미나가 바닥나면 은신을 유지할 수 없으므로 은신도 함께 풀린다.
        /// </summary>
        public void OwnerConsumeForLunge()
        {
            staminaDepleter?.DepleteStamina();
            isStealthedLocal = false;
            isReleaseRequired = true;
        }

        /// <summary>서버가 센 은신 스태미나도 함께 바닥내, 조작된 클라이언트가 바로 다시 은신하지 못하게 한다.</summary>
        [Server]
        public void ServerConsumeForLunge()
        {
            serverStamina.Deplete();
            isStealthedSynced.Value = false;
        }

        /// <summary>
        /// 외형이 사냥 시간에 바뀌어 새 애니메이터가 생기므로 매 프레임 넣고,
        /// 쭈그린 동작이 없는 외형은 몸을 낮춰 은신이 보이게 한다.
        /// </summary>
        private void UpdateCrouchVisual(bool isCrouched)
        {
            if (skinView == null)
                return;

            bool hasCrouchAnimation = skinView.TrySetBool(CharacterAnimatorParameters.Crouch, isCrouched);
            bool shouldLower = isCrouched && !hasCrouchAnimation;
            if (shouldLower == isVisualCrouched)
                return;

            isVisualCrouched = shouldLower;
            skinView.SetModelHeightOffset(shouldLower ? fallbackCrouchHeight : 0f);
        }
    }
}
