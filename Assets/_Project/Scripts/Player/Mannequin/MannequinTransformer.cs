using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using GuardVsMannequin.Core;
using GuardVsMannequin.Localization;
using GuardVsMannequin.Map;
using GuardVsMannequin.Player.Emotes;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>변신 상태를 사물 번호 하나로 동기화하며 마네킹의 변신과 해제를 관리한다.</summary>
    public class MannequinTransformer : NetworkBehaviour, ICooldownGauge, IEmoteBlocker
    {
        // 클라이언트와 서버가 재는 거리가 조금 달라 두는 서버 거리 검증 여유(m).
        private const float ServerRangeTolerance = 1.5f;

        private readonly SyncVar<int> currentPropIndex = new(TransformableProp.None);
        private readonly ToolCooldown cooldown = new();

        private IPlayerInput playerInput;
        private IActionLock actionLock;
        private PropTargetFinder targetFinder;

        /// <summary>외형·충돌체·넉백이 변신을 따라가도록 피어마다 한 번씩 알린다.</summary>
        public event Action<int> DisguiseChanged;

        /// <summary>재접속 때 같은 사물로 되돌리도록 노출하는 변신한 사물 번호.</summary>
        public int CurrentPropIndex => currentPropIndex.Value;

        public bool IsDisguised => currentPropIndex.Value != TransformableProp.None;

        /// <summary>사물 모습으로 감정 표현을 하면 바로 들키므로 변신 중에는 막는다.</summary>
        public bool IsEmoteBlocked => IsDisguised;

        public string DisplayName => Loc.Get("tool.disguise");
        public float CooldownRemaining => cooldown.LocalRemaining;
        public bool IsReady => CooldownRemaining <= 0f && CanTransformIn(GameServices.CurrentStateOrWaiting);

        /// <inheritdoc />
        public float CooldownProgress => cooldown.LocalProgress;

        /// <summary>
        /// 외곽선 표시와 같은 판정을 쓰도록 사물 찾기 객체를 여기서 만들고, 변신 동기화를 이벤트로 연결한다.
        /// </summary>
        private void Awake()
        {
            playerInput = GetComponent<IPlayerInput>();
            actionLock = GetComponent<IActionLock>();
            targetFinder = new PropTargetFinder(transform, GetComponent<IAimProvider>(), GameTuning.TransformRange);
            currentPropIndex.OnChange += OnPropIndexChanged;
        }

        /// <summary>
        /// 파괴된 뒤 동기화 콜백이 불리지 않도록 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            currentPropIndex.OnChange -= OnPropIndexChanged;
        }

        /// <summary>
        /// 남의 아바타가 내 우클릭에 반응하지 않도록 소유자만 입력을 듣는다.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner && playerInput != null)
                playerInput.SecondaryPressed += OnSecondaryPressed;
        }

        /// <summary>
        /// 아바타가 사라진 뒤 입력 이벤트가 이 객체를 부르지 않도록 구독을 끊는다.
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsOwner && playerInput != null)
                playerInput.SecondaryPressed -= OnSecondaryPressed;
        }

        /// <summary>한 키로 변신과 해제를 함께 하므로 지금 상태에 따라 요청을 고르고, 해제 쿨타임은 먼저 시작한다.</summary>
        private void OnSecondaryPressed()
        {
            if (IsLocked)
                return;

            if (IsDisguised)
            {
                cooldown.StartLocal(GameTuning.UntransformCooldown);
                UntransformServerRpc();
                return;
            }

            if (!IsReady)
                return;

            if (TryGetAimedProp(out TransformableProp prop))
                TransformServerRpc(prop.Index);
        }

        /// <summary>
        /// 외곽선 표시(<see cref="PropHighlighter"/>)가 변신 요청과 같은 판정을 쓰도록 사물 찾기를 밖에 연다.
        /// </summary>
        public bool TryGetAimedProp(out TransformableProp prop)
        {
            return targetFinder.TryFind(out prop);
        }

        /// <summary>
        /// 클라이언트가 아무 번호나 보낼 수 있으므로 변신 가능 여부·사거리·단계·쿨타임을 서버에서 다시 확인한다.
        /// </summary>
        [ServerRpc]
        private void TransformServerRpc(int propIndex)
        {
            if (IsLocked || IsDisguised || !PropRegistry.TryGet(propIndex, out TransformableProp prop) || !prop.CanDisguiseAs)
                return;
            if (!IsWithinRange(prop))
                return;
            if (!CanTransformIn(GameServices.CurrentStateOrWaiting))
                return;
            if (!cooldown.IsServerReady)
                return;

            currentPropIndex.Value = propIndex;
        }

        /// <summary>먼 사물 번호를 보내는 조작을 막도록 몸에서 사물의 지금 경계까지 거리를 잰다.</summary>
        private bool IsWithinRange(TransformableProp prop)
        {
            float rangeLimit = GameTuning.TransformRange + ServerRangeTolerance;
            return prop.CurrentWorldBounds.SqrDistance(transform.position) <= rangeLimit * rangeLimit;
        }

        /// <summary>
        /// 해제·재변신을 연타해 위치를 숨기지 못하도록 해제 쿨타임을 서버에서도 건다.
        /// </summary>
        [ServerRpc]
        private void UntransformServerRpc()
        {
            if (IsLocked || !IsDisguised)
                return;

            currentPropIndex.Value = TransformableProp.None;
            cooldown.StartServer(GameTuning.UntransformCooldown);
        }

        /// <summary>재접속한 사람이 같은 모습으로 돌아오도록 스폰 직전에 부르므로 [Server]를 붙이지 않는다.</summary>
        public void ServerRestoreDisguise(int propIndex)
        {
            currentPropIndex.Value = propIndex;
        }

        /// <summary>사냥 시간에는 구울로 바뀌어야 하므로 진입할 때 쿨타임 없이 해제한다.</summary>
        [Server]
        public void ForceUntransform()
        {
            currentPropIndex.Value = TransformableProp.None;
        }

        /// <summary>날아가는 중에 변신을 바꾸면 외형과 대리 물체가 어긋나므로 잠근다.</summary>
        private bool IsLocked => actionLock != null && actionLock.IsActionLocked;

        /// <summary>
        /// 카운트다운 중에 미리 변신해 두면 시작 전에 숨는 셈이므로 탐색·사냥 시간에만 허용한다.
        /// </summary>
        private static bool CanTransformIn(GameState state)
        {
            return state == GameState.Playing || state == GameState.Hunting;
        }

        /// <summary>
        /// 호스트는 서버·클라이언트 콜백이 둘 다 오므로, 외형이 두 번 바뀌지 않게 클라이언트 쪽에서만 알린다.
        /// </summary>
        private void OnPropIndexChanged(int previousIndex, int nextIndex, bool asServer)
        {
            if (asServer && IsClientInitialized)
                return;

            DisguiseChanged?.Invoke(nextIndex);
        }
    }
}
