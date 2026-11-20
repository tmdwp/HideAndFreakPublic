using GuardVsMannequin.Settings;
using UnityEngine;

namespace GuardVsMannequin.Cameras
{
    /// <summary>개인 설정의 시야각을 게임 카메라에 적용하고, 기본값이면 카메라에 정해 둔 값으로 되돌린다.</summary>
    [RequireComponent(typeof(Camera))]
    public class ViewFieldOfView : MonoBehaviour
    {
        private Camera viewCamera;
        private float authoredFieldOfView;

        /// <summary>프리팹·씬을 고치지 않아도 되도록 카메라를 켜는 쪽이 실행 중에 붙인다. 이미 있으면 그대로 쓴다.</summary>
        public static void Attach(Camera target)
        {
            if (target != null && !target.TryGetComponent(out ViewFieldOfView _))
                target.gameObject.AddComponent<ViewFieldOfView>();
        }

        /// <summary>기본값으로 되돌릴 수 있도록 바꾸기 전 카메라 값을 기억한다.</summary>
        private void Awake()
        {
            viewCamera = GetComponent<Camera>();
            authoredFieldOfView = viewCamera.fieldOfView;
            Apply();
        }

        /// <summary>설정 화면에서 바꾸면 바로 보이도록 변경 알림을 듣는다.</summary>
        private void OnEnable()
        {
            OptionsStore.OptionsChanged += Apply;
            Apply();
        }

        /// <summary>
        /// 꺼진 뒤 이벤트가 이 객체를 부르지 않도록 끊는다.
        /// </summary>
        private void OnDisable()
        {
            OptionsStore.OptionsChanged -= Apply;
        }

        /// <summary>
        /// 값을 바꾸지 않는 설정 변경도 오므로 다를 때만 넣는다.
        /// </summary>
        private void Apply()
        {
            if (viewCamera == null)
                return;
            float fieldOfView = FieldOfViewSettings.Resolve(OptionsStore.Current, authoredFieldOfView);
            if (!Mathf.Approximately(viewCamera.fieldOfView, fieldOfView))
                viewCamera.fieldOfView = fieldOfView;
        }
    }
}
