using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using GuardVsMannequin.Core;

namespace GuardVsMannequin.Objectives
{
    /// <summary>모든 PC가 같은 목표 진행을 보도록 이번 판에 파괴된 전원 장치 수를 세고 동기화한다.</summary>
    public class ObjectiveTracker : NetworkBehaviour
    {
        private readonly SyncVar<int> destroyedCount = new();

        public int DestroyedCount => destroyedCount.Value;

        /// <summary>판 기록이 장치를 몰라도 되도록 부순 사람들의 접속 ID만 서버에서 알린다.</summary>
        public event Action<IReadOnlyList<int>> ServerGeneratorDestroyed;

        /// <summary>테스트 방에서 설정을 바꾸면 바로 반영되도록 매번 설정에서 읽는다.</summary>
        public int RequiredCount => GameServices.State?.Settings.objectiveCount ?? 0;

        /// <summary>목표가 0회인 판은 시간으로 사냥에 들어가므로 완료로 치지 않는다.</summary>
        public bool IsComplete => RequiredCount > 0 && DestroyedCount >= RequiredCount;

        /// <summary>
        /// 지난 판 수가 이어지면 바로 목표가 달성되므로 새 판 시작 때 비운다.
        /// </summary>
        [Server]
        public void ResetCount()
        {
            destroyedCount.Value = 0;
        }

        /// <summary>
        /// 전원 장치는 판마다 새로 스폰되므로, 스폰한 쪽이 넘겨줄 때마다 파괴를 듣는다.
        /// </summary>
        [Server]
        public void Track(Generator generator)
        {
            generator.ServerDestroyed += OnGeneratorDestroyed;
        }

        /// <summary>
        /// 한 장치가 두 번 세지지 않도록 세면서 구독을 끊는다.
        /// </summary>
        private void OnGeneratorDestroyed(Generator generator)
        {
            generator.ServerDestroyed -= OnGeneratorDestroyed;
            destroyedCount.Value++;
            ServerGeneratorDestroyed?.Invoke(generator.DestroyerClientIds);
        }
    }
}
