using ECM2;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 캐릭터 카메라의 정면을 조준 광선으로 사용한다.
    /// 카메라가 없으면(원격 아바타) 몸체 정면을 사용한다.
    /// </summary>
    [RequireComponent(typeof(Character))]
    public class CameraAimProvider : MonoBehaviour, IAimProvider
    {
        [Tooltip("카메라가 없을 때 사용할 눈 높이.")]
        [SerializeField] private float fallbackEyeHeight = 1.6f;

        private Character character;

        /// <summary>
        /// 같은 오브젝트의 캐릭터를 캐시한다.
        /// </summary>
        private void Awake()
        {
            character = GetComponent<Character>();
        }

        /// <summary>
        /// 카메라 위치와 정면 방향으로 광선을 만든다.
        /// </summary>
        public Ray GetAimRay()
        {
            Transform cameraTransform = character.cameraTransform;
            if (cameraTransform != null)
                return new Ray(cameraTransform.position, cameraTransform.forward);

            return new Ray(transform.position + Vector3.up * fallbackEyeHeight, transform.forward);
        }
    }
}
