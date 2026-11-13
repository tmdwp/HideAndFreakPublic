using System.Collections.Generic;

namespace GuardVsMannequin.Network
{
    /// <summary>공개방에서 암호를 무차별 대입하지 못하도록 반복해서 틀리는 접속자를 잠시 막는다.</summary>
    public sealed class PasswordAttemptLimiter
    {
        // 연속 실패가 이 횟수에 이르면 막는다.
        private const int MaxFailures = 5;

        // 막는 시간이자 실패 횟수를 잊는 시간(초).
        private const float LockSeconds = 60f;

        // 주소를 바꿔 가며 메모리를 채우지 못하도록 기억하는 접속자 수 상한.
        private const int MaxTracked = 256;

        private sealed class Entry
        {
            public int failures;
            public float lastFailureTime;
            public float lockedUntil;
        }

        private readonly Dictionary<string, Entry> entries = new();

        /// <summary>
        /// 막힌 동안에는 맞는 암호를 보내도 거부해야 대입을 끊을 수 있어서, 암호 비교 전에 먼저 확인한다.
        /// </summary>
        /// <param name="key">접속자 주소.</param>
        /// <param name="now">현재 시각(초).</param>
        public bool IsLocked(string key, float now)
        {
            return entries.TryGetValue(key ?? string.Empty, out Entry entry) && now < entry.lockedUntil;
        }

        /// <summary>가끔 틀린 사람까지 막지 않도록 오래전 실패는 잊고 짧은 시간에 여러 번 틀리면 막는다.</summary>
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
        /// 맞힌 사람이 이전 실패 때문에 나중에 막히지 않도록 기록을 지운다.
        /// </summary>
        public void Clear(string key)
        {
            entries.Remove(key ?? string.Empty);
        }

        /// <summary>
        /// 기록이 끝없이 늘지 않도록 상한에 이르면 기한이 지난 기록을 지우고, 그래도 가득 차면 가장 오래된 기록을 지운다.
        /// </summary>
        private void MakeRoom(float now)
        {
            if (entries.Count < MaxTracked)
                return;

            string oldestKey = null;
            float oldestTime = float.MaxValue;
            var expiredKeys = new List<string>();
            foreach (KeyValuePair<string, Entry> pair in entries)
            {
                if (now >= pair.Value.lockedUntil && now - pair.Value.lastFailureTime >= LockSeconds)
                    expiredKeys.Add(pair.Key);
                if (pair.Value.lastFailureTime < oldestTime)
                {
                    oldestTime = pair.Value.lastFailureTime;
                    oldestKey = pair.Key;
                }
            }

            foreach (string expiredKey in expiredKeys)
                entries.Remove(expiredKey);
            if (expiredKeys.Count == 0 && oldestKey != null)
                entries.Remove(oldestKey);
        }
    }
}
