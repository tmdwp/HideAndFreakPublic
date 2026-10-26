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
    /// <summary>
    /// 마네킹의 변신 상태를 관리한다.
    /// 우클릭으로 조준한 사물로 변신하거나 변신을 해제하며, 상태는 맵 사물 번호(<see cref="TransformableProp.Index"/>) 하나로 동기화된다.
    /// 맡은 일은 입력 → 서버 검증 → 동기화뿐이고, 나머지는 나눠 맡긴다:
    /// 조준한 사물 찾기 <see cref="PropTargetFinder"/>, 번호로 사물 찾기 <see cref="PropRegistry"/>,
    /// 쿨타임 <see cref="ToolCooldown"/>, 외형 복사·적용 <see cref="PropDisguiseView"/>.
    /// </summary>
    public class MannequinTransformer : NetworkBehaviour, ITool, IEmoteBlocker
    {
        // 서버 거리 검증 여유(m). 클라이언트 조준 판정은 몸-명중 지점 거리, 서버는 몸-사물 경계 거리라 조금 다르다.
        private const float ServerRangeTolerance = 1.5f;

        private readonly SyncVar<int> currentPropIndex = new(TransformableProp.None);
        private readonly ToolCooldown cooldown = new();

        private IPlayerInput playerInput;
        private IActionLock actionLock;
        private PropTargetFinder targetFinder;

        /// <summary>변신 상태가 바뀌었을 때. 인자는 새 사물 번호(해제 시 <see cref="TransformableProp.None"/>). 각 피어에서 한 번씩 호출된다.</summary>
        public event Action<int> DisguiseChanged;

        /// <summary>현재 변신한 사물 번호. 변신하지 않았으면 <see cref="TransformableProp.None"/>.</summary>
        public int CurrentPropIndex => currentPropIndex.Value;

        /// <summary>변신 중인지 여부.</summary>
        public bool IsDisguised => currentPropIndex.Value != TransformableProp.None;

        /// <summary>변신 중에는 감정 표현을 할 수 없다.</summary>
        public bool IsEmoteBlocked => IsDisguised;

        public string DisplayName => Loc.Get("tool.disguise");
        public float CooldownRemaining => cooldown.LocalRemaining;
        public bool IsReady => CooldownRemaining <= 0f && CanTransformIn(GameServices.CurrentStateOrWaiting);

        /// <summary>
        /// 입력·사물 찾기 구현을 만들고 동기화 콜백을 연결한다.
        /// </summary>
        private void Awake()
        {
            playerInput = GetComponent<IPlayerInput>();
            actionLock = GetComponent<IActionLock>();
            targetFinder = new PropTargetFinder(transform, GetComponent<IAimProvider>(), GameTuning.TransformRange);
            currentPropIndex.OnChange += OnPropIndexChanged;
        }

        /// <summary>
        /// 동기화 콜백을 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            currentPropIndex.OnChange -= OnPropIndexChanged;
        }

        /// <summary>
        /// 소유자만 우클릭 입력을 구독한다.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner && playerInput != null)
                playerInput.SecondaryPressed += OnSecondaryPressed;
        }

        /// <summary>
        /// 입력 구독을 해제한다.
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsOwner && playerInput != null)
                playerInput.SecondaryPressed -= OnSecondaryPressed;
        }

        /// <summary>
        /// 변신 중이면 해제를, 아니면 조준한 사물로 변신을 요청한다.
        /// </summary>
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
        /// 지금 조준하고 있는 변신 가능 사물을 찾는다. (변신 사거리 안, 번호가 있고 너무 크지 않은 것만)
        /// 외곽선 표시(<see cref="PropHighlighter"/>)도 같은 판정을 쓴다.
        /// </summary>
        public bool TryGetAimedProp(out TransformableProp prop)
        {
            return targetFinder.TryFind(out prop);
        }

        /// <summary>
        /// 서버에서 변신 요청을 검증하고 적용한다. (변신 가능한 사물인지, 사거리 안인지, 단계·쿨타임)
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

        /// <summary>
        /// 몸에서 사물 경계까지의 거리가 변신 사거리(+여유) 안인지 확인한다. (먼 사물 번호를 보내는 조작 방지)
        /// 경계는 사물의 지금 자리 기준이다. (판마다 섞기로 옮겨진 사물도 맞게 잰다)
        /// </summary>
        private bool IsWithinRange(TransformableProp prop)
        {
            float limit = GameTuning.TransformRange + ServerRangeTolerance;
            return prop.CurrentWorldBounds.SqrDistance(transform.position) <= limit * limit;
        }

        /// <summary>
        /// 서버에서 변신을 해제하고 해제 쿨타임을 시작한다.
        /// </summary>
        [ServerRpc]
        private void UntransformServerRpc()
        {
            if (IsLocked || !IsDisguised)
                return;

            currentPropIndex.Value = TransformableProp.None;
            cooldown.StartServer(GameTuning.UntransformCooldown);
        }

        /// <summary>
        /// 쿨타임 없이 강제로 변신을 해제한다. 사냥 시간 진입 시 사용하며, 이후 다시 변신할 수 있다.
        /// </summary>
        [Server]
        public void ForceUntransform()
        {
            currentPropIndex.Value = TransformableProp.None;
        }

        /// <summary>날아가는 중이라 변신·해제를 할 수 없는지. (<see cref="IActionLock"/>)</summary>
        private bool IsLocked => actionLock != null && actionLock.IsActionLocked;

        /// <summary>
        /// 변신이 가능한 상태인지 확인한다. (탐색/사냥 시간만, 판 시작 준비 중에는 불가)
        /// </summary>
        private static bool CanTransformIn(GameState state)
        {
            return state == GameState.Playing || state == GameState.Hunting;
        }

        /// <summary>
        /// 사물 번호 변경을 피어마다 한 번만 알린다. (호스트는 클라이언트 콜백에서만 처리)
        /// </summary>
        private void OnPropIndexChanged(int prev, int next, bool asServer)
        {
            if (asServer && IsClientInitialized)
                return;

            DisguiseChanged?.Invoke(next);
        }
    }
}
