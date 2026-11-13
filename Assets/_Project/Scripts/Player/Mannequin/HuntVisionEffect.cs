using GuardVsMannequin.Core;
using GuardVsMannequin.Rendering;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>사냥 시간 마네킹이 가까운 곳만 보도록 본인 화면에 개인 조명과 검은 안개를 건다.</summary>
    public class HuntVisionEffect : MonoBehaviour, IViewEffect
    {
        [Tooltip("본인 화면에서만 켜지는 개인 조명. 다른 플레이어에게는 꺼진 상태로 남는다.")]
        [SerializeField] private Light visionLight;

        [SerializeField] private Color fogColor = Color.black;

        private bool isActive;
        private FogSettings originalFog;

        /// <summary>
        /// 프리팹에서 켜져 있으면 다른 플레이어 화면도 밝아지므로, 시작할 때 개인 조명을 꺼 둔다.
        /// </summary>
        private void Awake()
        {
            if (visionLight != null)
                visionLight.enabled = false;
        }

        /// <summary>
        /// 안개는 씬 전역 설정이라, 사망·판 종료로 아바타가 사라질 때 되돌리지 않으면 다음 화면까지 남는다.
        /// </summary>
        private void OnDestroy()
        {
            Deactivate();
        }

        /// <summary>안개는 씬 전역 설정이라 되돌릴 수 있게 원래 값을 먼저 저장한다.</summary>
        public void Activate()
        {
            if (isActive)
                return;
            isActive = true;

            originalFog = FogSettings.Capture();
            FogSettings.Linear(fogColor, GameTuning.HuntVisionFogStart, GameTuning.HuntVisionFogEnd).Apply();

            if (visionLight != null)
            {
                visionLight.range = GameTuning.HuntVisionLightRange;
                visionLight.intensity = GameTuning.HuntVisionLightIntensity;
                visionLight.enabled = true;
            }
        }

        /// <summary>
        /// 켠 적이 없을 때 되돌리면 저장하지 않은 값으로 안개를 덮어쓰므로, 켜져 있을 때만 원래 설정을 복원한다.
        /// </summary>
        public void Deactivate()
        {
            if (!isActive)
                return;
            isActive = false;

            originalFog.Apply();

            if (visionLight != null)
                visionLight.enabled = false;
        }
    }
}
