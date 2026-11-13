using FishNet.Object;
using FishNet.Object.Synchronizing;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>관전과 고개 방향 표시를 위해 카메라 시선을 다른 플레이어에게 동기화한다.</summary>
    public class PlayerViewSync : NetworkBehaviour
    {
        /// <summary>매 프레임 보내면 대역폭이 아까워서 둔 최소 전송 간격(초).</summary>
        private const float SendInterval = 0.1f;

        /// <summary>미세한 손떨림까지 보내지 않도록 이 각도 이상 바뀌었을 때만 전송한다.</summary>
        private const float SendThreshold = 1f;

        [Tooltip("원격 피어에서 상하 시선을 적용할 피벗. (가드의 카메라 피벗, 없으면 적용하지 않음)")]
        [SerializeField] private Transform pitchPivot;
        [SerializeField] private float remoteSmoothing = 15f;

        /// <summary>x = Pitch, y = Yaw.</summary>
        private readonly SyncVar<Vector2> syncedView = new();

        private ICharacterMotor motor;
        private Vector2 lastSentView;
        private float nextSendTime;

        /// <summary>소유자는 전송 지연이 없도록 실제 카메라 값을, 원격은 동기화된 값을 쓴다.</summary>
        public float ViewPitch => IsOwner ? ReadLocalView().x : syncedView.Value.x;

        /// <summary>소유자는 전송 지연이 없도록 실제 카메라 값을, 원격은 동기화된 값을 쓴다.</summary>
        public float ViewYaw => IsOwner ? ReadLocalView().y : syncedView.Value.y;

        /// <summary>
        /// 매 프레임 GetComponent를 부르지 않도록 이동 구현을 미리 찾아 둔다.
        /// </summary>
        private void Awake()
        {
            motor = GetComponent<ICharacterMotor>();
        }

        /// <summary>
        /// 시선의 원본은 소유자에게만 있으므로 소유자는 보내고, 원격 피어는 받은 값을 손전등 피벗에 반영한다.
        /// </summary>
        private void Update()
        {
            if (!this.IsNetworkReady() || !IsSpawned)
                return;

            if (IsOwner)
                SendViewIfChanged();
            else if (pitchPivot != null)
                ApplyRemotePitch();
        }

        /// <summary>
        /// 카메라가 아직 연결되지 않은 순간에도 값이 비지 않도록 몸 방향으로 대신한다.
        /// </summary>
        private Vector2 ReadLocalView()
        {
            Transform cameraView = motor?.ViewTransform;
            Transform viewTransform = cameraView != null ? cameraView : transform;
            Vector3 euler = viewTransform.eulerAngles;
            // eulerAngles.x는 0~360이라 위를 보면 270 같은 값이 나오므로 -180~180으로 바꾼다.
            return new Vector2(Mathf.DeltaAngle(0f, euler.x), euler.y);
        }

        /// <summary>
        /// 대역폭을 아끼려고 간격과 변화량 두 조건을 모두 넘을 때만 보낸다.
        /// </summary>
        private void SendViewIfChanged()
        {
            if (Time.time < nextSendTime)
                return;

            Vector2 currentView = ReadLocalView();
            bool hasChanged = Mathf.Abs(currentView.x - lastSentView.x) >= SendThreshold
                              || Mathf.Abs(Mathf.DeltaAngle(currentView.y, lastSentView.y)) >= SendThreshold;
            if (!hasChanged)
                return;

            lastSentView = currentView;
            nextSendTime = Time.time + SendInterval;
            SetViewServerRpc(currentView);
        }

        /// <summary>
        /// 조작된 값이 다른 화면의 카메라를 망가뜨리지 않도록 NaN·무한대를 버리고 상하 각도를 제한한다.
        /// </summary>
        [ServerRpc]
        private void SetViewServerRpc(Vector2 view)
        {
            if (!float.IsFinite(view.x) || !float.IsFinite(view.y))
                return;
            syncedView.Value = new Vector2(Mathf.Clamp(view.x, -90f, 90f), view.y);
        }

        /// <summary>전송 간격 때문에 값이 계단식으로 오므로 부드럽게 따라가게 한다.</summary>
        private void ApplyRemotePitch()
        {
            Quaternion targetRotation = Quaternion.Euler(syncedView.Value.x, 0f, 0f);
            pitchPivot.localRotation = Quaternion.Slerp(pitchPivot.localRotation, targetRotation, remoteSmoothing * Time.deltaTime);
        }
    }
}
