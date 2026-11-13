using ECM2;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>화면 가운데를 맞히도록 카메라 정면을 조준 광선으로 쓴다.</summary>
    [RequireComponent(typeof(Character))]
    public class CameraAimProvider : MonoBehaviour, IAimProvider
    {
        [Tooltip("카메라가 없을 때 사용할 눈 높이.")]
        [SerializeField] private float fallbackEyeHeight = 1.6f;

        private Character character;

        /// <summary>
        /// 쏠 때마다 GetComponent를 부르지 않도록 미리 찾아 둔다.
        /// </summary>
        private void Awake()
        {
            character = GetComponent<Character>();
        }

        /// <summary>원격 아바타는 카메라가 없어 몸 정면과 눈 높이로 광선을 대신한다.</summary>
        public Ray GetAimRay()
        {
            Transform cameraTransform = character.cameraTransform;
            if (cameraTransform != null)
                return new Ray(cameraTransform.position, cameraTransform.forward);

            return new Ray(transform.position + Vector3.up * fallbackEyeHeight, transform.forward);
        }
    }
}
