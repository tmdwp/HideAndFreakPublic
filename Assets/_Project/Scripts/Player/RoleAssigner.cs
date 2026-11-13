using System;
using System.Collections.Generic;
using GuardVsMannequin.Core;

namespace GuardVsMannequin.Player
{
    /// <summary>네트워크 없이 테스트하도록 참가자에게 역할을 무작위로 배정하는 순수 로직.</summary>
    public class RoleAssigner
    {
        private readonly Random random;

        /// <summary>테스트에서 결과를 재현하도록 난수 생성기를 밖에서 받는다.</summary>
        public RoleAssigner(Random random = null)
        {
            this.random = random ?? new Random();
        }

        /// <summary>
        /// 룸에서 정한 가드 수가 지금 인원에 맞지 않을 수 있어서, 1 ~ 인원의 절반으로 보정한 뒤 나머지를 마네킹으로 배정한다.
        /// </summary>
        /// <param name="guardCount">호스트가 원한 보정 전 가드 수.</param>
        public Dictionary<int, PlayerRole> Assign(IReadOnlyList<int> clientIds, int guardCount)
        {
            var result = new Dictionary<int, PlayerRole>(clientIds.Count);
            if (clientIds.Count == 0)
                return result;

            int maxGuardCount = GameSettingsValidator.GetMaxGuardCount(clientIds.Count);
            int clampedGuardCount = Math.Clamp(guardCount, 1, maxGuardCount);

            List<int> shuffledIds = new List<int>(clientIds);
            Shuffle(shuffledIds);

            for (int i = 0; i < shuffledIds.Count; i++)
                result[shuffledIds[i]] = i < clampedGuardCount ? PlayerRole.Guard : PlayerRole.Mannequin;

            return result;
        }

        /// <summary>
        /// 모든 순서가 같은 확률로 나와야 특정 사람이 가드에 몰리지 않으므로 Fisher-Yates로 섞는다.
        /// </summary>
        private void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
            }
        }
    }
}
