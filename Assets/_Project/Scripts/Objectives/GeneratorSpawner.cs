using System.Collections.Generic;
using FishNet.Object;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Objectives
{
    /// <summary>
    /// 준비 단계에 후보 위치 중 무작위로 전원 장치를 생성하고, 대기 상태로 돌아가면 제거한다.
    /// </summary>
    public class GeneratorSpawner : NetworkBehaviour
    {
        // 가장 먼 후보만 고르면 매 판 같은 배치가 되므로 상위 몇 개 안에서 무작위로 고른다.
        private const int TopChoices = 3;

        [SerializeField] private GameStateMachine stateMachine;
        [SerializeField] private ObjectiveTracker objectiveTracker;
        [SerializeField] private NetworkObject generatorPrefab;

        private readonly List<GeneratorSpawnPoint> spawnPoints = new();
        private readonly List<NetworkObject> spawnedGenerators = new();

        /// <summary>
        /// 스폰은 서버만 하므로 서버가 시작될 때 맵의 후보 위치를 모으고 단계 변경을 듣는다.
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();
            spawnPoints.Clear();
            spawnPoints.AddRange(FindObjectsByType<GeneratorSpawnPoint>());
            stateMachine.ServerStateChanged += OnServerStateChanged;
        }

        /// <summary>
        /// 서버가 멈춘 뒤 콜백이 남지 않도록 구독을 끊는다.
        /// </summary>
        public override void OnStopServer()
        {
            base.OnStopServer();
            stateMachine.ServerStateChanged -= OnServerStateChanged;
        }

        /// <summary>
        /// 장치 수명을 판 단계에 맞추려고 준비 단계에서 생성하고 대기 상태로 돌아가면 제거한다.
        /// </summary>
        private void OnServerStateChanged(GameState previousState, GameState nextState)
        {
            if (nextState == GameState.Preparation)
                SpawnGenerators(stateMachine.Settings.generatorCount);
            else if (nextState == GameState.Waiting)
                DespawnAll();
        }

        /// <summary>장치가 한곳에 몰리면 가드가 지키기 쉬우므로 서로 멀리 떨어진 위치에 생성한다.</summary>
        private void SpawnGenerators(int count)
        {
            DespawnAll();
            objectiveTracker.ResetCount();

            if (count > spawnPoints.Count)
            {
                Debug.LogWarning($"[GeneratorSpawner] 후보 위치({spawnPoints.Count})가 설정된 전원 장치 수({count})보다 적습니다.");
                count = spawnPoints.Count;
            }

            foreach (GeneratorSpawnPoint spawnPoint in PickSpread(count))
            {
                Transform pointTransform = spawnPoint.transform;
                NetworkObject instance = Instantiate(generatorPrefab, pointTransform.position, pointTransform.rotation);
                ServerManager.Spawn(instance);

                objectiveTracker.Track(instance.GetComponent<Generator>());
                spawnedGenerators.Add(instance);
            }
        }

        /// <summary>
        /// 다음 판에 이전 장치가 남지 않도록 이번 판에 생성한 것을 모두 지운다.
        /// </summary>
        private void DespawnAll()
        {
            foreach (NetworkObject generator in spawnedGenerators)
            {
                if (generator != null && generator.IsSpawned)
                    ServerManager.Despawn(generator);
            }
            spawnedGenerators.Clear();
        }

        /// <summary>넓게 퍼지되 매 판 같은 배치가 되지 않도록 가장 먼 후보 상위 몇 개 중에서 무작위로 고른다.</summary>
        private List<GeneratorSpawnPoint> PickSpread(int count)
        {
            var remaining = new List<GeneratorSpawnPoint>(spawnPoints);
            Shuffle(remaining);
            var chosen = new List<GeneratorSpawnPoint>();
            if (count <= 0 || remaining.Count == 0)
                return chosen;

            chosen.Add(remaining[0]);
            remaining.RemoveAt(0);
            while (chosen.Count < count && remaining.Count > 0)
            {
                remaining.Sort((first, second) => NearestChosenDistance(second, chosen).CompareTo(NearestChosenDistance(first, chosen)));
                int pickIndex = Random.Range(0, Mathf.Min(TopChoices, remaining.Count));
                chosen.Add(remaining[pickIndex]);
                remaining.RemoveAt(pickIndex);
            }
            return chosen;
        }

        /// <summary>
        /// 비교만 하면 되므로 제곱근을 생략한 거리 제곱으로 가장 가까운 선택 위치까지를 잰다.
        /// </summary>
        private static float NearestChosenDistance(GeneratorSpawnPoint candidate, List<GeneratorSpawnPoint> chosen)
        {
            float nearestSqrDistance = float.MaxValue;
            Vector3 candidatePosition = candidate.transform.position;
            foreach (GeneratorSpawnPoint point in chosen)
                nearestSqrDistance = Mathf.Min(nearestSqrDistance, (point.transform.position - candidatePosition).sqrMagnitude);
            return nearestSqrDistance;
        }

        /// <summary>
        /// 첫 위치를 무작위로 고르려고 Fisher-Yates로 섞는다.
        /// </summary>
        private static void Shuffle(List<GeneratorSpawnPoint> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
            }
        }
    }
}
