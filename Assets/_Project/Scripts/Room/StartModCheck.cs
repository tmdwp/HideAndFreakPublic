using System.Collections.Generic;

namespace GuardVsMannequin.Room
{
    /// <summary>시작 전 모드 확인의 판정 결과.</summary>
    public enum StartModCheckResult : byte
    {
        /// <summary>아직 답하지 않은 게스트가 있어 기다린다.</summary>
        Waiting,

        /// <summary>전원이 모드를 가지고 있다.</summary>
        Passed,

        /// <summary>모드가 없거나 시간 안에 답하지 않은 게스트가 있다.</summary>
        Failed
    }

    /// <summary>네트워크 없이 시험하도록 시작 뒤 게스트들의 모드 확인 답을 기다리는 과정만 값으로 판정한다.</summary>
    public sealed class StartModCheck
    {
        private float deadline;

        public bool IsRunning { get; private set; }

        /// <summary>
        /// 답하지 않는 게스트 때문에 영원히 기다리지 않도록 기한을 정해 시작한다.
        /// </summary>
        /// <param name="now">현재 시각(초).</param>
        /// <param name="timeout">답을 기다릴 시간(초).</param>
        public void Begin(float now, float timeout)
        {
            IsRunning = true;
            deadline = now + timeout;
        }

        /// <summary>
        /// 시작 전에 맵을 바꾸면 이전 맵 기준 답이 의미가 없어지므로 확인을 취소한다.
        /// </summary>
        public void Cancel()
        {
            IsRunning = false;
        }

        /// <summary>없다고 답한 사람이 있으면 바로, 답하지 않은 사람이 있으면 기한 뒤 실패로 본다.</summary>
        /// <param name="members">호스트는 이미 맵을 가지고 있으므로 판정에서 뺀다.</param>
        /// <param name="now">현재 시각(초).</param>
        public StartModCheckResult Evaluate(IEnumerable<RoomMember> members, float now)
        {
            bool anyUnknown = false;
            foreach (RoomMember member in members)
            {
                if (member.isHost)
                    continue;
                if (member.modState == MemberModState.Missing)
                    return Finish(StartModCheckResult.Failed);
                if (member.modState == MemberModState.Unknown)
                    anyUnknown = true;
            }

            if (!anyUnknown)
                return Finish(StartModCheckResult.Passed);
            return now < deadline ? StartModCheckResult.Waiting : Finish(StartModCheckResult.Failed);
        }

        /// <summary>
        /// 결과가 나면 확인을 끝내야 다음 판정이 이어지지 않으므로, 끝내기와 결과 반환을 한 번에 한다.
        /// </summary>
        private StartModCheckResult Finish(StartModCheckResult result)
        {
            IsRunning = false;
            return result;
        }
    }
}
