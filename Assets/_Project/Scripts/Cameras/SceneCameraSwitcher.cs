using GuardVsMannequin.Player;
using UnityEngine;

namespace GuardVsMannequin.Cameras
{
    /// <summary>
    /// 로컬 플레이어 아바타가 없을 때만 씬(대기용) 카메라를 켠다.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class SceneCameraSwitcher : MonoBehaviour
    {
        private UnityEngine.Camera sceneCamera;
        private AudioListener sceneListener;

        /// <summary>
        /// 카메라 컴포넌트를 캐시하고 로컬 플레이어 이벤트를 구독한다.
        /// </summary>
        private void Awake()
        {
            sceneCamera = GetComponent<UnityEngine.Camera>();
            sceneListener = GetComponent<AudioListener>();

            LocalPlayerEvents.Spawned += OnLocalPlayerSpawned;
            LocalPlayerEvents.Despawned += OnLocalPlayerDespawned;
            SetActive(LocalPlayerEvents.Current == null);
        }

        /// <summary>
        /// 이벤트 구독을 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            LocalPlayerEvents.Spawned -= OnLocalPlayerSpawned;
            LocalPlayerEvents.Despawned -= OnLocalPlayerDespawned;
        }

        /// <summary>로컬 플레이어가 생기면 씬 카메라를 끈다.</summary>
        private void OnLocalPlayerSpawned(PlayerIdentity identity) => SetActive(false);

        /// <summary>
        /// 로컬 플레이어가 사라지면 씬 카메라를 켠다.
        /// 다시 스폰(테스트 방 역할 전환 등)에서 새 아바타가 먼저 생긴 뒤 옛 아바타가 사라지면 켜지 않는다.
        /// </summary>
        private void OnLocalPlayerDespawned(PlayerIdentity identity) => SetActive(LocalPlayerEvents.Current == null);

        /// <summary>
        /// 카메라와 오디오 리스너를 함께 켜고 끈다.
        /// </summary>
        private void SetActive(bool active)
        {
            sceneCamera.enabled = active;
            if (sceneListener != null)
                sceneListener.enabled = active;
        }
    }
}
