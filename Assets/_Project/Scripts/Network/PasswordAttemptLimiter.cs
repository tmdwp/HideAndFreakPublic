using System.Collections.Generic;

namespace GuardVsMannequin.Network
{
    /// <summary>
    /// 방 암호를 반복해서 틀리는 접속자를 잠시 막는다. 공개방에서 암호를 무차별로 대입하는 것을 어렵게 한다.
    /// 값만 판정하고 네트워크는 다루지 않는다. 시각은 호출하는 쪽이 넘긴다. (서버 전용)
    /// </summary>
    public sealed class PasswordAttemptLimiter
    {
        // 연속 실패가 이 횟수에 이르면 막는다.
        private const int MaxFailures = 5;

        // 막는 시간(초). 마지막 실패 후 이 시간이 지나면 실패 횟수도 잊는다.
        private const float LockSeconds = 60f;

        // 기억하는 접속자 수의 상한. 주소를 바꿔 가며 접속해 메모리를 채우는 것을 막는다.
        private const int MaxTracked = 256;

        private sealed class Entry
        {
            public int failures;
            public float lastFailureTime;
            public float lockedUntil;
        }

        private readonly Dictionary<string, Entry> entries = new();

        /// <summary>
        /// 지금 막혀 있는 접속자인지 확인한다.
        /// </summary>
        /// <param name="key">접속자 주소. (SteamID 또는 IP)</param>
        /// <param name="now">현재 시각(초).</param>
        public bool IsLocked(string key, float now)
        {
            return entries.TryGetValue(key ?? string.Empty, out Entry entry) && now < entry.lockedUntil;
        }

        /// <summary>
        /// 암호를 틀린 것을 기록한다. 횟수를 채우면 막고 횟수를 다시 센다.
        /// </summary>
        /// <param name="key">접속자 주소.</param>
        /// <param name="now">현재 시각(초).</param>
        public void RecordFailure(string key, float now)
        {
            key ??= string.Empty;
            if (!entries.TryGetValue(key, out Entry entry))
            {
                MakeRoom(now);
                entry = new Entry();
                entries[key] = entry;
            }
            else if (now - entry.lastFailureTime >= LockSeconds)
            {
                entry.failures = 0;
            }

            entry.lastFailureTime = now;
            entry.failures++;
            if (entry.failures >= MaxFailures)
            {
                entry.failures = 0;
                entry.lockedUntil = now + LockSeconds;
            }
        }

        /// <summary>
        /// 암호를 맞힌 접속자의 기록을 지운다.
        /// </summary>
        public void Clear(string key)
        {
            entries.Remove(key ?? string.Empty);
        }

        /// <summary>
        /// 상한에 이르면 기한이 지난 기록을 지우고, 그래도 가득 차면 가장 오래된 기록을 지운다.
        /// </summary>
        private void MakeRoom(float now)
        {
            if (entries.Count < MaxTracked)
                return;

            string oldestKey = null;
            float oldestTime = float.MaxValue;
            var expired = new List<string>();
            foreach (KeyValuePair<string, Entry> pair in entries)
            {
                if (now >= pair.Value.lockedUntil && now - pair.Value.lastFailureTime >= LockSeconds)
                    expired.Add(pair.Key);
                if (pair.Value.lastFailureTime < oldestTime)
                {
                    oldestTime = pair.Value.lastFailureTime;
                    oldestKey = pair.Key;
                }
            }

            foreach (string key in expired)
                entries.Remove(key);
            if (expired.Count == 0 && oldestKey != null)
                entries.Remove(oldestKey);
        }
    }
}
