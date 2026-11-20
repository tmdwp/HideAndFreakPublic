using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using GuardVsMannequin.Audio;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Objectives
{
    /// <summary>마네킹이 함께 붙어 파괴하는 전원 장치로, 모두 손을 떼면 진행도가 초기화된다.</summary>
    [RequireComponent(typeof(Collider))]
    public class Generator : NetworkBehaviour, IInteractable
    {
        /// <summary>상호작용 도중 조금 움직여도 끊기지 않도록 허용하는 추가 거리.</summary>
        private const float RangeTolerance = 0.5f;

        private readonly SyncVar<float> progress = new();
        private readonly SyncVar<bool> isDestroyed = new();

        private readonly Dictionary<int, IInteractor> participants = new();
        // 반복 중에 사전을 고칠 수 없어서 뺄 키를 모아 두는 버퍼
        private readonly List<int> removeBuffer = new();
        private readonly List<int> destroyerClientIds = new();
        private Collider interactCollider;
        private NoiseEmitter noiseEmitter;

        /// <summary>목표 집계가 장치를 몰라도 되도록 서버에서 파괴 완료를 알린다.</summary>
        public event Action<Generator> ServerDestroyed;

        /// <summary>0~1.</summary>
        public float Progress => progress.Value;

        /// <summary>파괴된 장치를 다시 부숴 목표 수가 올라가지 않도록 상호작용을 막는 데 쓴다.</summary>
        public bool IsDestroyed => isDestroyed.Value;

        /// <summary>서버 전용.</summary>
        public int ParticipantCount => participants.Count;

        /// <summary>판 기록에 누가 부쉈는지 남기도록 파괴 순간 붙어 있던 사람들. 서버 전용.</summary>
        public IReadOnlyList<int> DestroyerClientIds => destroyerClientIds;

        public float InteractRange => GameTuning.InteractRange;

        /// <summary>
        /// 장치 모양이 다양해서 중심 거리 대신 충돌체 표면 거리로 재려고 충돌체를 미리 찾아 둔다.
        /// </summary>
        private void Awake()
        {
            interactCollider = GetComponent<Collider>();
            noiseEmitter = GetComponent<NoiseEmitter>();
        }

        /// <summary>
        /// 클라이언트 조건은 믿지 않고 서버에서 파괴 여부·상태·거리를 다시 확인한 뒤 참여자로 넣는다.
        /// </summary>
        [Server]
        public bool TryBeginInteract(IInteractor interactor)
        {
            if (isDestroyed.Value || !interactor.CanInteract || !IsInRange(interactor, 0f))
                return false;

            participants[interactor.ClientId] = interactor;
            return true;
        }

        /// <summary>
        /// 손을 뗀 사람이 진행 속도에 계속 반영되지 않도록 뺀다.
        /// </summary>
        [Server]
        public void EndInteract(IInteractor interactor)
        {
            participants.Remove(interactor.ClientId);
        }

        /// <summary>
        /// 같은 클라이언트가 다시 스폰되면 ID는 같아도 다른 주체이므로 참조까지 같은지 확인한다.
        /// </summary>
        [Server]
        public bool IsInteracting(IInteractor interactor)
        {
            return participants.TryGetValue(interactor.ClientId, out IInteractor currentInteractor) && ReferenceEquals(currentInteractor, interactor);
        }

        /// <summary>조작을 막도록 서버에서 참여자를 검증해 진행도를 올리고, 모두 손을 떼면 진행도를 비운다.</summary>
        private void Update()
        {
            if (!this.IsServerReady() || isDestroyed.Value)
                return;

            RemoveInvalidParticipants();

            int participantCount = participants.Count;
            if (participantCount == 0)
            {
                if (progress.Value != 0f)
                    progress.Value = 0f;
                return;
            }

            float destroyTime = GameServices.CurrentSettingsOrDefault.generatorDestroyTime;
            float speed = CalculateTotalSpeed(participantCount);
            float nextProgress = progress.Value + speed / destroyTime * Time.deltaTime;

            if (nextProgress >= 1f)
                CompleteDestruction();
            else
                progress.Value = nextProgress;
        }

        /// <summary>함께하면 빨라지되 인원수만큼 곱절이 되지는 않도록 지수로 늘린다.</summary>
        public static float CalculateTotalSpeed(int participantCount)
        {
            return participantCount <= 0 ? 0f : Mathf.Pow(GameTuning.CoopDestroySpeedBase, participantCount - 1);
        }

        /// <summary>
        /// 죽거나 변신하거나 멀어진 참여자가 진행을 계속 올리지 않도록 매 프레임 걸러 낸다.
        /// </summary>
        private void RemoveInvalidParticipants()
        {
            removeBuffer.Clear();
            foreach (KeyValuePair<int, IInteractor> pair in participants)
            {
                IInteractor interactor = pair.Value;
                // 인터페이스로 들고 있으면 파괴된 Unity 오브젝트도 null 비교가 참이 안 되므로 따로 확인한다.
                bool isDestroyedObject = interactor is UnityEngine.Object unityObject && unityObject == null;
                if (isDestroyedObject || !interactor.CanInteract || !IsInRange(interactor, RangeTolerance))
                    removeBuffer.Add(pair.Key);
            }

            foreach (int clientId in removeBuffer)
                participants.Remove(clientId);
        }

        /// <summary>
        /// 장치 모양이 다양해서 중심 거리 대신 충돌체 표면에서 가장 가까운 점까지 잰다.
        /// </summary>
        private bool IsInRange(IInteractor interactor, float tolerance)
        {
            Vector3 closestPoint = interactCollider.ClosestPoint(interactor.Position);
            float range = InteractRange + tolerance;
            return (closestPoint - interactor.Position).sqrMagnitude <= range * range;
        }

        /// <summary>
        /// 가드가 파괴를 알아챌 수 있도록 소음을 낸 뒤, 목표 집계가 세도록 이벤트를 보낸다.
        /// </summary>
        private void CompleteDestruction()
        {
            progress.Value = 1f;
            isDestroyed.Value = true;
            destroyerClientIds.Clear();
            destroyerClientIds.AddRange(participants.Keys);
            participants.Clear();
            if (noiseEmitter != null)
                noiseEmitter.Emit(NoiseType.GeneratorDestroyed);
            ServerDestroyed?.Invoke(this);
        }
    }
}
