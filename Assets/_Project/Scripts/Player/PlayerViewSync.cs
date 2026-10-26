using FishNet.Object;
using FishNet.Object.Synchronizing;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 플레이어 카메라의 시선(Pitch, Yaw)을 다른 플레이어에게 동기화한다.
    /// - 관전자가 대상 플레이어의 시야로 볼 수 있게 한다.
    /// - 가드는 상하 시선을 카메라 피벗에 적용해, 원격 화면의 손전등이 실제 보는 방향을 비추게 한다.
    /// </summary>
    public class PlayerViewSync : NetworkBehaviour
    {
        /// <summary>전송 주기(초).</summary>
        private const float SendInterval = 0.1f;

        /// <summary>이 각도 이상 바뀌었을 때만 전송한다.</summary>
        private const float SendThreshold = 1f;

        [Tooltip("원격 피어에서 상하 시선을 적용할 피벗. (가드의 카메라 피벗, 없으면 적용하지 않음)")]
        [SerializeField] private Transform pitchPivot;
        [SerializeField] private float remoteSmoothing = 15f;

        /// <summary>x = Pitch, y = Yaw.</summary>
        private readonly SyncVar<Vector2> syncedView = new();

        private ICharacterMotor motor;
        private Vector2 lastSentView;
        private float nextSendTime;

        /// <summary>현재 시선의 상하 각도. 소유자는 실제 카메라 값, 원격은 동기화된 값.</summary>
        public float ViewPitch => IsOwner ? ReadLocalView().x : syncedView.Value.x;

        /// <summary>현재 시선의 좌우 각도. 소유자는 실제 카메라 값, 원격은 동기화된 값.</summary>
        public float ViewYaw => IsOwner ? ReadLocalView().y : syncedView.Value.y;

        /// <summary>
        /// 같은 오브젝트의 캐릭터 이동 구현을 캐시한다.
        /// </summary>
        private void Awake()
        {
            motor = GetComponent<ICharacterMotor>();
        }

        /// <summary>
        /// 소유자는 시선을 주기적으로 보내고, 원격 피어는 받은 상하 시선을 피벗에 적용한다.
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
        /// 소유자 카메라의 시선을 읽는다. 카메라가 없으면 몸 방향을 쓴다.
        /// </summary>
        private Vector2 ReadLocalView()
        {
            Transform cameraView = motor?.ViewTransform;
            Transform view = cameraView != null ? cameraView : transform;
            Vector3 euler = view.eulerAngles;
            return new Vector2(Mathf.DeltaAngle(0f, euler.x), euler.y);
        }

        /// <summary>
        /// 시선이 충분히 바뀌었고 전송 주기가 지났으면 서버에 보낸다.
        /// </summary>
        private void SendViewIfChanged()
        {
            if (Time.time < nextSendTime)
                return;

            Vector2 current = ReadLocalView();
            bool changed = Mathf.Abs(current.x - lastSentView.x) >= SendThreshold
                           || Mathf.Abs(Mathf.DeltaAngle(current.y, lastSentView.y)) >= SendThreshold;
            if (!changed)
                return;

            lastSentView = current;
            nextSendTime = Time.time + SendInterval;
            SetViewServerRpc(current);
        }

        /// <summary>
        /// 서버에서 시선 값을 갱신해 다른 클라이언트로 동기화한다. NaN·무한대 값은 버린다.
        /// </summary>
        [ServerRpc]
        private void SetViewServerRpc(Vector2 view)
        {
            if (!float.IsFinite(view.x) || !float.IsFinite(view.y))
                return;
            syncedView.Value = new Vector2(Mathf.Clamp(view.x, -90f, 90f), view.y);
        }

        /// <summary>
        /// 동기화된 상하 시선을 피벗에 부드럽게 적용한다. (원격 가드는 GuardCharacter가 꺼져 있어 덮어쓰지 않음)
        /// </summary>
        private void ApplyRemotePitch()
        {
            Quaternion target = Quaternion.Euler(syncedView.Value.x, 0f, 0f);
            pitchPivot.localRotation = Quaternion.Slerp(pitchPivot.localRotation, target, remoteSmoothing * Time.deltaTime);
        }
    }
}
