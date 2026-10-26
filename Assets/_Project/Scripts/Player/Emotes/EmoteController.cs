using FishNet.Object;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Player.Emotes
{
    /// <summary>
    /// 감정 표현을 네트워크로 동기화한다.
    /// 마네킹이 1~6번 키를 누르면 서버가 검증한 뒤 <see cref="ServerPlay"/>로 모든 클라이언트에 재생한다.
    /// </summary>
    public class EmoteController : NetworkBehaviour
    {
        private IEmoteView[] emoteViews;
        private IPlayerInput playerInput;
        private IEmoteBlocker emoteBlocker;
        private float serverEmoteEndTime;

        /// <summary>
        /// 뷰, 입력, 차단 조건 구현을 캐시한다.
        /// </summary>
        private void Awake()
        {
            // 컴포넌트 순서가 우선순위다. (애니메이터 → 절차적 움직임)
            emoteViews = GetComponents<IEmoteView>();
            playerInput = GetComponent<IPlayerInput>();
            emoteBlocker = GetComponent<IEmoteBlocker>();
        }

        /// <summary>
        /// 소유자만 감정 표현 입력을 구독한다.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner && playerInput != null)
                playerInput.EmotePressed += OnEmotePressed;
        }

        /// <summary>
        /// 입력 구독을 해제한다.
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsOwner && playerInput != null)
                playerInput.EmotePressed -= OnEmotePressed;
        }

        /// <summary>
        /// 차단 조건(변신 중 등)이 아니면 서버에 재생을 요청한다.
        /// </summary>
        private void OnEmotePressed(int emoteId)
        {
            if (emoteBlocker != null && emoteBlocker.IsEmoteBlocked)
                return;

            RequestEmoteServerRpc(emoteId);
        }

        /// <summary>
        /// 서버에서 요청을 검증하고 재생한다.
        /// </summary>
        [ServerRpc]
        private void RequestEmoteServerRpc(int emoteId)
        {
            if (emoteBlocker != null && emoteBlocker.IsEmoteBlocked)
                return;

            ServerPlay(emoteId);
        }

        /// <summary>
        /// 모든 클라이언트에서 감정 표현을 재생한다. 재생 중이면 무시한다. (서버 전용)
        /// </summary>
        /// <returns>재생을 시작했는지 여부.</returns>
        [Server]
        public bool ServerPlay(int emoteId)
        {
            if (emoteId < 1 || emoteId > GameConstants.EmoteCount || Time.time < serverEmoteEndTime)
                return false;

            serverEmoteEndTime = Time.time + GameTuning.EmoteDuration;
            PlayObserversRpc(emoteId);
            return true;
        }

        /// <summary>
        /// 각 클라이언트에서 뷰를 순서대로 시도해 처음 성공한 것 하나만 재생한다.
        /// </summary>
        [ObserversRpc]
        private void PlayObserversRpc(int emoteId)
        {
            foreach (IEmoteView view in emoteViews)
            {
                if (view.TryPlay(emoteId))
                    return;
            }
        }
    }

    /// <summary>
    /// 감정 표현을 막는 조건. (마네킹: 변신 중)
    /// </summary>
    public interface IEmoteBlocker
    {
        /// <summary>지금 감정 표현이 막혀 있는지 여부.</summary>
        bool IsEmoteBlocked { get; }
    }
}
