using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using GuardVsMannequin.Core;
using GuardVsMannequin.Network;

namespace GuardVsMannequin.Player
{
    /// <summary>승패를 판정하도록 살아 있는 아바타를 추적해 역할별 생존 인원과 참가 인원을 동기화한다.</summary>
    public class PlayerRegistry : NetworkBehaviour
    {
        private readonly SyncVar<int> aliveGuardCount = new();
        private readonly SyncVar<int> aliveMannequinCount = new();
        private readonly SyncVar<int> matchMannequinCount = new();

        private readonly HashSet<PlayerIdentity> alivePlayers = new();
        private RoomMembership membership;

        /// <summary>잠깐 끊긴 것만으로 판이 끝나지 않도록 돌아올 사람까지 센 가드 생존 수.</summary>
        public int AliveGuardCount => aliveGuardCount.Value;

        /// <summary>잠깐 끊긴 것만으로 판이 끝나지 않도록 돌아올 사람까지 센 마네킹 생존 수.</summary>
        public int AliveMannequinCount => aliveMannequinCount.Value;

        /// <summary>목표 수를 인원에 맞추는 데 쓰므로 사망·접속 종료한 사람도 센다.</summary>
        public int MatchMannequinCount => matchMannequinCount.Value;

        /// <summary>서버 전용.</summary>
        public IReadOnlyCollection<PlayerIdentity> AlivePlayers => alivePlayers;

        /// <summary>
        /// 생존 인원은 서버만 계산하므로 서버에서만 스폰·제거와 유예 인원 변화를 듣는다.
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();
            PlayerIdentity.ServerSpawned += OnAvatarSpawned;
            PlayerIdentity.ServerDespawned += OnAvatarDespawned;
            membership = SessionServices.Session?.Membership;
            if (membership != null)
                membership.PendingChanged += Recount;
        }

        /// <summary>
        /// 정적 이벤트라 해제하지 않으면 다음 씬에서도 불리므로 끊고, 지난 판 아바타 목록도 비운다.
        /// </summary>
        public override void OnStopServer()
        {
            base.OnStopServer();
            PlayerIdentity.ServerSpawned -= OnAvatarSpawned;
            PlayerIdentity.ServerDespawned -= OnAvatarDespawned;
            if (membership != null)
                membership.PendingChanged -= Recount;
            membership = null;
            alivePlayers.Clear();
        }

        /// <summary>판 시작 신호가 따로 없어 살아 있는 아바타가 없을 때의 첫 스폰으로 새 판을 알아챈다.</summary>
        private void OnAvatarSpawned(PlayerIdentity identity)
        {
            if (alivePlayers.Count == 0 && !identity.IsRejoin)
                matchMannequinCount.Value = 0;

            if (!alivePlayers.Add(identity))
                return;

            if (identity.Role == PlayerRole.Mannequin && !identity.IsRejoin)
                matchMannequinCount.Value++;
            Recount();
        }

        /// <summary>목록에 없던 아바타면 다시 셀 필요가 없으므로, 실제로 뺐을 때만 다시 센다.</summary>
        private void OnAvatarDespawned(PlayerIdentity identity)
        {
            if (alivePlayers.Remove(identity))
                Recount();
        }

        /// <summary>
        /// 증감으로 관리하면 유예 인원과 겹칠 때 어긋나기 쉬워서, 바뀔 때마다 처음부터 다시 센다.
        /// </summary>
        private void Recount()
        {
            int guardCount = 0;
            int mannequinCount = 0;
            foreach (PlayerIdentity identity in alivePlayers)
            {
                if (identity.Role == PlayerRole.Guard)
                    guardCount++;
                else if (identity.Role == PlayerRole.Mannequin)
                    mannequinCount++;
            }

            aliveGuardCount.Value = guardCount + (membership?.CountPendingAlive(PlayerRole.Guard) ?? 0);
            aliveMannequinCount.Value = mannequinCount + (membership?.CountPendingAlive(PlayerRole.Mannequin) ?? 0);
        }
    }
}
