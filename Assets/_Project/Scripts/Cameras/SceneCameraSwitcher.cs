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
        /// 이미 아바타가 생긴 뒤에 이 씬이 열렸을 수도 있어서, 구독과 함께 지금 상태로 한 번 맞춘다.
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
        /// 정적 이벤트라 해제하지 않으면 파괴된 뒤에도 불리므로 끊는다.
        /// </summary>
        private void OnDestroy()
        {
            LocalPlayerEvents.Spawned -= OnLocalPlayerSpawned;
            LocalPlayerEvents.Despawned -= OnLocalPlayerDespawned;
        }

        /// <summary>아바타 카메라와 겹쳐 두 화면이 그려지지 않도록 씬 카메라를 끈다.</summary>
        private void OnLocalPlayerSpawned(PlayerIdentity identity) => SetActive(false);

        /// <summary>화면이 검게 남지 않도록 아바타가 사라지면 씬 카메라를 켜되, 새 아바타가 이미 있으면 켜지 않는다.</summary>
        private void OnLocalPlayerDespawned(PlayerIdentity identity) => SetActive(LocalPlayerEvents.Current == null);

        /// <summary>
        /// 리스너가 둘이면 Unity가 경고를 내므로 카메라와 오디오 리스너를 함께 켜고 끈다.
        /// </summary>
        private void SetActive(bool active)
        {
            sceneCamera.enabled = active;
            if (sceneListener != null)
                sceneListener.enabled = active;
        }
    }
}
