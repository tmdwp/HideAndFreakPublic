using FishNet.Object;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Player.Emotes
{
    /// <summary>모두가 보도록 서버가 검증한 감정 표현을 모든 클라이언트에 재생한다.</summary>
    public class EmoteController : NetworkBehaviour
    {
        private IEmoteView[] emoteViews;
        private IPlayerInput playerInput;
        private IEmoteBlocker emoteBlocker;
        private float serverEmoteEndTime;

        /// <summary>재생마다 찾지 않도록 미리 찾아 두고, 컴포넌트 순서를 뷰 우선순위로 쓴다.</summary>
        private void Awake()
        {
            emoteViews = GetComponents<IEmoteView>();
            playerInput = GetComponent<IPlayerInput>();
            emoteBlocker = GetComponent<IEmoteBlocker>();
        }

        /// <summary>
        /// 남의 아바타가 내 키에 반응하지 않도록 소유자만 입력을 듣는다.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner && playerInput != null)
                playerInput.EmotePressed += OnEmotePressed;
        }

        /// <summary>
        /// 아바타가 사라진 뒤 입력 이벤트가 이 객체를 부르지 않도록 구독을 끊는다.
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsOwner && playerInput != null)
                playerInput.EmotePressed -= OnEmotePressed;
        }

        /// <summary>
        /// 막힌 상태에서 RPC를 보내 봐야 거부되므로 소유자에서 먼저 거른다.
        /// </summary>
        private void OnEmotePressed(int emoteId)
        {
            if (emoteBlocker != null && emoteBlocker.IsEmoteBlocked)
                return;

            RequestEmoteServerRpc(emoteId);
        }

        /// <summary>
        /// 클라이언트 확인은 조작할 수 있으므로 서버에서 차단 조건을 다시 본다.
        /// </summary>
        [ServerRpc]
        private void RequestEmoteServerRpc(int emoteId)
        {
            if (emoteBlocker != null && emoteBlocker.IsEmoteBlocked)
                return;

            ServerPlay(emoteId);
        }

        /// <summary>연타로 동작이 끊기거나 네트워크가 넘치지 않도록 재생 중에는 무시한다.</summary>
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
        /// 여러 뷰가 동시에 재생되면 겹쳐 보이므로 순서대로 시도해 처음 성공한 하나만 재생한다.
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

    /// <summary>감정 표현을 막는 조건.</summary>
    public interface IEmoteBlocker
    {
        bool IsEmoteBlocked { get; }
    }
}
