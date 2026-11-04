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

    /// <summary>
    /// 호스트가 시작을 누른 뒤 게스트들의 답을 기다리는 과정을 관리한다. 값만 판정하고 네트워크는 다루지 않는다. (서버 전용)
    /// </summary>
    public sealed class StartModCheck
    {
        private float deadline;

        /// <summary>확인이 진행 중인지 여부.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// 확인을 시작한다.
        /// </summary>
        /// <param name="now">현재 시각(초).</param>
        /// <param name="timeout">답을 기다릴 시간(초).</param>
        public void Begin(float now, float timeout)
        {
            IsRunning = true;
            deadline = now + timeout;
        }

        /// <summary>
        /// 확인을 취소한다. (시작 전에 맵을 바꿨을 때)
        /// </summary>
        public void Cancel()
        {
            IsRunning = false;
        }

        /// <summary>
        /// 게스트들의 답을 판정한다. 없다고 답한 사람이 있으면 바로, 답하지 않은 사람이 있으면 기한이 지난 뒤 실패다. 끝나면 확인을 마친다.
        /// </summary>
        /// <param name="members">참가자 목록. 호스트는 판정에서 뺀다.</param>
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
        /// 확인을 마치고 결과를 돌려준다.
        /// </summary>
        private StartModCheckResult Finish(StartModCheckResult result)
        {
            IsRunning = false;
            return result;
        }
    }
}
