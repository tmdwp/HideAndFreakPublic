using GuardVsMannequin.Player;
using GuardVsMannequin.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GuardVsMannequin.UI
{
    /// <summary>위험을 바로 느끼도록 체력이 낮을수록 본인 화면 가장자리를 어둡게 한다.</summary>
    public class LowHealthVignette : MonoBehaviour
    {
        [Tooltip("비네트·노출 효과 프로필. 비우면 기본값으로 실행 중에 만든다.")]
        [SerializeField] private VolumeProfile profileTemplate;

        [Tooltip("체력이 거의 없을 때의 비네트 세기(0~1).")]
        [SerializeField] private float maxVignetteIntensity = 0.6f;

        [Tooltip("체력이 거의 없을 때 낮출 노출(EV).")]
        [SerializeField] private float maxExposureDrop = 1.5f;

        [Tooltip("효과 변화 속도. 맞았을 때 서서히 어두워진다.")]
        [SerializeField] private float blendSpeed = 3f;

        // 맵의 볼륨보다 나중에 섞여야 체력 효과가 덮이지 않는다.
        public const float VolumePriority = 100f;

        private Volume volume;
        private VolumeProfile profile;
        private Vignette vignette;
        private ColorAdjustments colorAdjustments;
        private float currentDamage;

        /// <summary>미리 둔 볼륨·프로필을 우선 쓰고, 없으면 다른 볼륨보다 우선순위가 높은 볼륨을 만든다.</summary>
        private void Awake()
        {
            if (!TryGetComponent(out volume))
            {
                volume = gameObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = VolumePriority;
            }

            profile = VolumeProfiles.Assign(volume, profileTemplate, CreateDefaultProfile);
            vignette = VolumeProfiles.GetOrAdd<Vignette>(profile);
            colorAdjustments = VolumeProfiles.GetOrAdd<ColorAdjustments>(profile);
            vignette.intensity.Override(0f);
            colorAdjustments.postExposure.Override(0f);
        }

        /// <summary>에디터 미리 굽기와 실행 중 대체가 같은 값을 쓰도록 기본 프로필을 한곳에서 만든다.</summary>
        public static VolumeProfile CreateDefaultProfile()
        {
            VolumeProfile defaultProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            defaultProfile.name = "VP_LowHealth";

            var defaultVignette = defaultProfile.Add<Vignette>(true);
            defaultVignette.color.Override(Color.black);
            defaultVignette.smoothness.Override(0.8f);
            defaultVignette.intensity.Override(0f);

            var defaultColorAdjustments = defaultProfile.Add<ColorAdjustments>(true);
            defaultColorAdjustments.postExposure.Override(0f);
            return defaultProfile;
        }

        /// <summary>
        /// 실행 중 프로필(에셋 복사본 포함)은 자동으로 해제되지 않으므로 직접 지운다.
        /// </summary>
        private void OnDestroy()
        {
            VolumeProfiles.Release(profile);
        }

        /// <summary>맞는 순간 화면이 뚝 바뀌지 않도록 목표 세기를 천천히 따라가고, 아바타가 없으면 끈다.</summary>
        private void Update()
        {
            float targetDamage = 0f;
            PlayerIdentity localPlayer = LocalPlayerEvents.Current;
            if (localPlayer != null && localPlayer.TryGetComponent(out PlayerHealth health) && health.MaxHealth > 0)
                targetDamage = 1f - Mathf.Clamp01((float)health.CurrentHealth / health.MaxHealth);

            currentDamage = Mathf.MoveTowards(currentDamage, targetDamage, blendSpeed * Time.deltaTime);
            vignette.intensity.value = currentDamage * maxVignetteIntensity;
            colorAdjustments.postExposure.value = -currentDamage * maxExposureDrop;
        }
    }
}
