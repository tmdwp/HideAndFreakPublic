using System;
using System.Collections.Generic;
using GuardVsMannequin.Core;

namespace GuardVsMannequin.Network
{
    /// <summary>네트워크 없이 시험하도록 방에 들어온 사람의 키·추방·재접속 자리를 값으로 기억한다.</summary>
    public sealed class RoomMembership
    {
        private readonly Dictionary<int, string> keyByClient = new();
        private readonly Dictionary<string, int> clientByKey = new();
        private readonly HashSet<string> banned = new();
        private readonly HashSet<string> leaving = new();
        private readonly Dictionary<string, RejoinSlot> slots = new();

        /// <summary>대기 중인 사람도 생존으로 세므로, 자리가 늘거나 줄면 생존 인원을 다시 세도록 알린다.</summary>
        public event Action PendingChanged;

        /// <summary>연결 번호는 접속마다 바뀌므로 플레이어 키와 묶어 두고, 다시 들어오면 "스스로 나감" 표시를 지운다.</summary>
        public void Bind(int clientId, string key)
        {
            if (keyByClient.TryGetValue(clientId, out string previousKey))
                clientByKey.Remove(previousKey);
            keyByClient[clientId] = key;
            clientByKey[key] = clientId;
            leaving.Remove(key);
        }

        /// <summary>
        /// 끊긴 직후에도 대기 자리를 만들 수 있도록 끊긴 연결도 번호가 다시 쓰이기 전까지 찾을 수 있다.
        /// </summary>
        public bool TryGetKey(int clientId, out string key)
        {
            return keyByClient.TryGetValue(clientId, out key);
        }

        /// <summary>
        /// 같은 사람이 새로 접속했을 때 남아 있는 이전 연결을 찾아 끊으려고 쓴다.
        /// </summary>
        public bool TryGetClient(string key, out int clientId)
        {
            return clientByKey.TryGetValue(key, out clientId);
        }

        /// <summary>
        /// 내보낸 사람이 재접속으로 돌아오지 못하도록 방이 닫힐 때까지 막고 대기 자리도 없앤다.
        /// </summary>
        public void Ban(string key)
        {
            banned.Add(key);
            RemoveSlot(key);
        }

        public bool IsBanned(string key)
        {
            return banned.Contains(key);
        }

        /// <summary>
        /// 스스로 나간 사람에게 대기 자리를 주면 판이 기다리기만 하므로, 끊겨도 자리를 만들지 않게 표시한다.
        /// </summary>
        public void MarkLeaving(string key)
        {
            leaving.Add(key);
        }

        /// <summary>
        /// 스스로 나갔거나 추방된 사람은 돌아올 이유가 없으므로 대기 자리를 주지 않는다.
        /// </summary>
        public bool CanHoldSlot(string key)
        {
            return !banned.Contains(key) && !leaving.Contains(key);
        }

        /// <summary>
        /// 생존 인원에 바로 반영되도록 더한 뒤 알린다.
        /// </summary>
        public void AddSlot(RejoinSlot slot)
        {
            slots[slot.key] = slot;
            PendingChanged?.Invoke();
        }

        /// <summary>
        /// 맵을 불러오다 다시 끊긴 사람에게 새 자리를 만들지 않도록 이미 있는지 확인한다.
        /// </summary>
        public bool HasSlot(string key)
        {
            return slots.ContainsKey(key);
        }

        /// <summary>
        /// 느린 PC가 맵을 불러오는 도중에 자리를 잃지 않도록, 다시 접속한 연결이 자리를 차지하면 불러오기 기한으로 판단하게 한다.
        /// </summary>
        /// <returns>대기 자리가 있었는지 여부.</returns>
        public bool TryClaim(string key, int clientId, float now)
        {
            if (!slots.TryGetValue(key, out RejoinSlot slot))
                return false;

            slot.claimedByClientId = clientId;
            slot.claimTime = now;
            return true;
        }

        /// <summary>끊겼다 붙었다를 반복해 유예 시간을 늘릴 수 없도록 처음 끊긴 시각은 그대로 두고 차지만 푼다.</summary>
        public void ReleaseClaim(string key)
        {
            if (slots.TryGetValue(key, out RejoinSlot slot))
                slot.claimedByClientId = -1;
        }

        /// <summary>
        /// 다른 연결이 남의 자리를 꺼내 가지 않도록, 그 자리를 차지한 연결일 때만 꺼내고 목록에서 지운다.
        /// </summary>
        public bool TryTakeClaimed(int clientId, out RejoinSlot slot)
        {
            slot = null;
            if (!keyByClient.TryGetValue(clientId, out string key)
                || !slots.TryGetValue(key, out RejoinSlot foundSlot)
                || foundSlot.claimedByClientId != clientId)
                return false;

            slot = foundSlot;
            RemoveSlot(key);
            return true;
        }

        /// <summary>
        /// 잠깐 끊긴 것만으로 판이 끝나지 않도록, 살아 있는 채로 돌아오길 기다리는 역할별 인원을 승패 판정에서 생존으로 센다.
        /// </summary>
        public int CountPendingAlive(PlayerRole role)
        {
            int count = 0;
            foreach (RejoinSlot slot in slots.Values)
            {
                if (slot.isAlive && slot.role == role)
                    count++;
            }
            return count;
        }

        /// <summary>돌아오지 않는 사람 때문에 판이 끝나지 않는 일이 없도록 기한이 지난 자리를 없앤다.</summary>
        /// <param name="now">현재 시각(초).</param>
        /// <param name="graceSeconds">끊긴 뒤 돌아올 수 있는 시간(초).</param>
        /// <param name="loadSeconds">다시 접속한 뒤 맵을 불러올 수 있는 시간(초).</param>
        /// <returns>호출한 쪽이 관련 기록도 지우도록 없앤 자리의 키.</returns>
        public List<string> RemoveExpired(float now, float graceSeconds, float loadSeconds)
        {
            var expiredKeys = new List<string>();
            foreach (RejoinSlot slot in slots.Values)
            {
                bool isExpired = slot.claimedByClientId >= 0
                    ? now - slot.claimTime >= loadSeconds
                    : now - slot.disconnectTime >= graceSeconds;
                if (isExpired)
                    expiredKeys.Add(slot.key);
            }

            foreach (string expiredKey in expiredKeys)
                slots.Remove(expiredKey);
            if (expiredKeys.Count > 0)
                PendingChanged?.Invoke();
            return expiredKeys;
        }

        /// <summary>판이 끝나면 돌아올 곳이 없으므로 대기 자리를 지우고 추방은 방이 닫힐 때까지 유지한다.</summary>
        public void ClearMatchState()
        {
            leaving.Clear();
            if (slots.Count == 0)
                return;
            slots.Clear();
            PendingChanged?.Invoke();
        }

        /// <summary>
        /// 실제로 지웠을 때만 생존 인원을 다시 세도록 알린다.
        /// </summary>
        private void RemoveSlot(string key)
        {
            if (slots.Remove(key))
                PendingChanged?.Invoke();
        }
    }
}
