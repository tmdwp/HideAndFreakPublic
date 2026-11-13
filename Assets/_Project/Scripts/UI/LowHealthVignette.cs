using GuardVsMannequin.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GuardVsMannequin.UI
{
    /// <summary>위험을 바로 느끼도록 체력이 낮을수록 본인 화면 가장자리를 어둡게 한다.</summary>
    public class LowHealthVignette : MonoBehaviour
    {
        [Tooltip("체력이 거의 없을 때의 비네트 세기(0~1).")]
        [SerializeField] private float maxVignetteIntensity = 0.6f;

        [Tooltip("체력이 거의 없을 때 낮출 노출(EV).")]
        [SerializeField] private float maxExposureDrop = 1.5f;

        [Tooltip("효과 변화 속도. 맞았을 때 서서히 어두워진다.")]
        [SerializeField] private float blendSpeed = 3f;

        private Volume volume;
        private VolumeProfile profile;
        private Vignette vignette;
        private ColorAdjustments colorAdjustments;
        private float currentDamage;

        /// <summary>씬마다 볼륨을 배치하지 않도록 실행 중에 만들고 다른 볼륨보다 우선순위를 높인다.</summary>
        private void Awake()
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();

            vignette = profile.Add<Vignette>(true);
            vignette.color.Override(Color.black);
            vignette.smoothness.Override(0.8f);
            vignette.intensity.Override(0f);

            colorAdjustments = profile.Add<ColorAdjustments>(true);
            colorAdjustments.postExposure.Override(0f);

            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            volume.sharedProfile = profile;
        }

        /// <summary>
        /// 런타임에 만든 프로필은 자동으로 해제되지 않으므로 직접 지운다.
        /// </summary>
        private void OnDestroy()
        {
            if (profile != null)
                Destroy(profile);
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
