using GuardVsMannequin.Core;
using GuardVsMannequin.Map;
using GuardVsMannequin.Player;
using GuardVsMannequin.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GuardVsMannequin.Cameras
{
    /// <summary>죽거나 참가하지 않아 아바타가 없을 때 생존자의 시야로 관전하게 한다.</summary>
    public class SpectatorController : MonoBehaviour
    {
        private const string PlayerMapName = "Player";

        [SerializeField] private InputActionAsset inputActions;

        private InputActionAsset runtimeActions;
        private InputAction previousAction;
        private InputAction nextAction;
        private Vector3 overviewPosition;
        private Quaternion overviewRotation;

        /// <summary>HUD가 보여 줄 관전 대상(관전 중이 아니면 null).</summary>
        public SpectatorViewPoint CurrentTarget { get; private set; }

        /// <summary>
        /// 대상의 시야 효과·몸 숨김이 관전 대상일 때만 걸리도록 이전·새 대상에게 관전 여부를 알린다.
        /// </summary>
        private void SetTarget(SpectatorViewPoint target)
        {
            if (CurrentTarget == target)
                return;

            if (CurrentTarget != null)
                CurrentTarget.SetSpectated(false);

            CurrentTarget = target;

            if (CurrentTarget != null)
                CurrentTarget.SetSpectated(true);
        }

        public bool IsSpectating => CurrentTarget != null;

        /// <summary>대기 화면 위치가 없을 때 돌아갈 곳으로 지금 위치를 기억하고, 원본 입력 에셋 대신 복제본에 저장된 키를 적용한다.</summary>
        private void Awake()
        {
            overviewPosition = transform.position;
            overviewRotation = transform.rotation;

            runtimeActions = Instantiate(inputActions);
            OptionsStore.ApplyBindings(runtimeActions);
            InputActionMap map = runtimeActions.FindActionMap(PlayerMapName, true);
            previousAction = map.FindAction("SpectatePrevious", true);
            nextAction = map.FindAction("SpectateNext", true);
        }

        /// <summary>
        /// 아바타가 없어 플레이어 입력이 꺼져 있어도 관전 대상을 바꿀 수 있도록 자체 입력을 켠다.
        /// </summary>
        private void OnEnable()
        {
            previousAction.Enable();
            nextAction.Enable();
        }

        /// <summary>
        /// 꺼진 동안 키 입력이 쌓이지 않도록 끈다.
        /// </summary>
        private void OnDisable()
        {
            previousAction.Disable();
            nextAction.Disable();
        }

        /// <summary>
        /// Instantiate로 만든 에셋은 씬과 함께 지워지지 않으므로 직접 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            if (runtimeActions != null)
                Destroy(runtimeActions);
        }

        /// <summary>대상이 죽으면 자동으로 넘어가도록 매 프레임 확인하고, 대상 위치가 갱신된 뒤 카메라를 옮긴다.</summary>
        private void Update()
        {
            if (!ShouldSpectate())
            {
                SetTarget(null);
                return;
            }

            int direction = previousAction.WasPressedThisFrame() ? -1 : nextAction.WasPressedThisFrame() ? 1 : 0;
            if (CurrentTarget == null || !IsAlive(CurrentTarget))
                SelectTarget(0, 0);
            else if (direction != 0)
                SelectTarget(IndexOf(CurrentTarget), direction);
        }

        /// <summary>
        /// 대상이 이번 프레임 움직인 뒤의 시야를 따라가도록 LateUpdate에서 카메라를 옮긴다.
        /// </summary>
        private void LateUpdate()
        {
            if (CurrentTarget != null)
            {
                CurrentTarget.GetPose(out Vector3 position, out Quaternion rotation);
                transform.SetPositionAndRotation(position, rotation);
            }
            else
            {
                // 카메라는 공용 씬에 있으므로 맵이 정한 대기 화면 위치를 쓴다.
                Transform mapOverview = MapInfo.Current != null ? MapInfo.Current.OverviewPoint : null;
                if (mapOverview != null)
                    transform.SetPositionAndRotation(mapOverview.position, mapOverview.rotation);
                else
                    transform.SetPositionAndRotation(overviewPosition, overviewRotation);
            }
        }

        /// <summary>
        /// 판 밖(대기)이나 내 아바타가 있을 때 관전하면 화면이 엉뚱해지므로, 판 중이고 내 아바타가 없으며 볼 생존자가 있을 때만 관전한다.
        /// </summary>
        private static bool ShouldSpectate()
        {
            GameState state = GameServices.CurrentStateOrWaiting;
            return state != GameState.Waiting && LocalPlayerEvents.Current == null && ClientPlayerList.Players.Count > 0;
        }

        /// <summary>관전 시점이 없는 아바타는 건너뛰도록 방향대로 넘기며 고를 수 있는 생존자를 찾는다.</summary>
        private void SelectTarget(int fromIndex, int direction)
        {
            int count = ClientPlayerList.Players.Count;
            for (int step = 0; step < count; step++)
            {
                // 음수 나머지를 피하려고 count를 더한 뒤 다시 나눈다.
                int index = ((fromIndex + direction * (step + 1)) % count + count) % count;
                if (direction == 0)
                    index = (fromIndex + step) % count;

                PlayerIdentity candidate = ClientPlayerList.Players[index];
                if (candidate != null && candidate.TryGetComponent(out SpectatorViewPoint viewPoint))
                {
                    SetTarget(viewPoint);
                    return;
                }
            }
            SetTarget(null);
        }

        /// <summary>
        /// 죽은 아바타는 생존자 목록에서 빠지므로 목록에 있는지로 생존을 판단한다.
        /// </summary>
        private static bool IsAlive(SpectatorViewPoint target)
        {
            return target != null && IndexOf(target) >= 0;
        }

        /// <summary>다음 대상을 고를 기준으로 생존자 목록에서 대상의 위치를 찾는다.</summary>
        private static int IndexOf(SpectatorViewPoint target)
        {
            for (int i = 0; i < ClientPlayerList.Players.Count; i++)
            {
                if (ClientPlayerList.Players[i] == target.Identity)
                    return i;
            }
            return -1;
        }
    }
}
